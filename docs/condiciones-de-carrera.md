## Manejo de condiciones de carrera

La plataforma opera sobre colecciones con alta concurrencia potencial: múltiples usuarios pueden interactuar simultáneamente con las mismas figuritas, propuestas y subastas. A continuación se describen todos los escenarios de condición de carrera identificados y la solución implementada para cada uno.

### Mecanismos base utilizados

Antes de detallar cada caso, conviene entender los tres mecanismos atómicos sobre los que se construye toda la solución de concurrencia:

- **Reserva atómica de stock (`TryReserveOneUnitAsync`)**: ejecuta un `UpdateOne` de MongoDB condicionado a `Quantity > 0`. Si la cantidad ya era cero cuando llega la segunda operación, MongoDB no aplica el update y retorna `ModifiedCount = 0`, lo que se interpreta como una falla de reserva. Solo un hilo puede decrementar el último stock.

- **Transición atómica de estado (`TryTransitionFromPendingAsync` / `AcceptProposalAtomically`)**: ejecuta un `FindOneAndUpdate` de MongoDB condicionado al estado actual del documento (por ejemplo, `State == Pending`). Si el documento ya cambió de estado cuando llega la segunda operación, el filtro no matchea y MongoDB retorna `null`. Esto actúa como un candado de estado: solo la primera operación concurrente gana.

- **Optimistic locking por versión**: para entidades que soportan edición directa (por ejemplo, `UserSticker`), se mantiene un campo `Version` numérico. Al actualizar, el filtro incluye la versión conocida por el cliente. Si otro hilo modificó el documento primero, la versión no coincide, el update no aplica y se lanza `OptimisticConcurrencyException` → HTTP 409.

---

### RC-01: Dos aceptaciones simultáneas de la misma propuesta

**Causa potencial:** El usuario B recibe la propuesta P1 y presiona "Aceptar" dos veces rápidamente, o dos requests llegan al servidor a la vez.

**Solución — transición atómica de estado:** `AcceptProposalAtomically` ejecuta un `FindOneAndUpdate` con filtro `State == Pending → State = Accepted`. Solo el primer request que llegue a MongoDB puede realizar esa transición. El segundo encuentra el documento ya en `Accepted`, el filtro no matchea y recibe `null`, lo que dispara una `InvalidOperationException` → HTTP 409. En ningún momento se crea un Exchange duplicado ni se reserva stock dos veces.

---

### RC-02 / RC-03: Dos cancelaciones simultáneas de la misma propuesta

**Causa potencial:** El usuario presiona "Cancelar" dos veces, o dos requests se procesan en paralelo.

**Solución — transición atómica de estado:** `TryTransitionFromPendingAsync` ejecuta un `FindOneAndUpdate` condicionado a `State == Pending`. El primer request cancela la propuesta y libera los stickers ofrecidos exactamente una vez. El segundo encuentra el documento en `Cancelled`, el filtro no matchea, retorna `null` y se omite la liberación de stock. Esto evita que el stock del proponente sea devuelto el doble de veces.

---

### RC-04: Dos propuestas de intercambio simultáneas usando el mismo sticker del proponente (Qty = 1)

**Causa potencial:** El usuario A tiene una figurita con cantidad 1 e intenta crear dos propuestas de intercambio a la vez ofreciendo esa misma figurita, antes de que la primera se registre.

**Solución — reserva atómica de stock:** Al crear una propuesta, `TryReserveOneUnitAsync` ejecuta `UpdateOne` con filtro `Quantity > 0` y decrementa. Solo el primer request puede decrementar de 1 a 0. El segundo llega con `Quantity = 0`, el filtro no matchea, `ModifiedCount = 0`, y la creación se aborta con HTTP 400. No se crea ninguna propuesta con stock fantasma.

---

### RC-05 (GAP-A): Aceptación concurrente de dos propuestas distintas sobre el mismo sticker (Qty = 1)

**Causa potencial:** El usuario B tiene la figurita Y con cantidad 1. Otros usuarios A y C crearon por separado las propuestas P1 y P2 solicitando esa figurita. El usuario B acepta P1 y P2 en requests que llegan casi al mismo tiempo.

**Solución — combinación de transición atómica + reserva atómica + rollback:**
1. Ambos requests ejecutan `AcceptProposalAtomically` sobre documentos distintos (P1 y P2), por lo que ambos pueden pasar a `Accepted` en paralelo.
2. Al intentar crear el Exchange, ambos ejecutan `TryReserveOneUnitAsync` sobre la figurita Y (Qty = 1). Solo uno puede decrementar; el otro obtiene `ModifiedCount = 0` → `InvalidOperationException`.
3. El request perdedor entra al `catch` y ejecuta `RollbackAcceptedProposalAsync`: revierte la propuesta de `Accepted` → `Rejected` (usando un update condicionado a `State == Accepted`, idempotente) y devuelve los stickers ofrecidos por el proponente perdedor.
4. El request ganador, tras crear el Exchange, ejecuta `AutoRejectPendingProposalsForRequestedStickerAsync`, que rechaza atómicamente todas las propuestas aún en `Pending` que soliciten la misma figurita Y, devolviendo sus stickers ofrecidos.

Resultado: exactamente un Exchange queda creado; la propuesta perdedora queda en `Rejected` (indistinguible de un rechazo normal para el proponente); no hay stock fantasma ni propuestas atascadas en `Accepted`.

---

### RC-06: Cancelación de propuesta por el proponente mientras el destinatario la acepta

**Causa potencial:** El usuario A cancela P1 al mismo tiempo que el usuario B acepta P1.

**Solución — transición atómica de estado:** El resultado depende de cuál request llega primero a MongoDB:
- Si **cancela** gana: `TryTransitionFromPendingAsync` pone P1 en `Cancelled`. Cuando `AcceptProposalAtomically` ejecuta su `FindOneAndUpdate` con filtro `State == Pending`, no matchea → retorna `null` → `InvalidOperationException` → HTTP 409. No se crea Exchange.
- Si **acepta** gana: `AcceptProposalAtomically` pone P1 en `Accepted`. Cuando la cancelación ejecuta `TryTransitionFromPendingAsync` con filtro `State == Pending`, no matchea → retorna `null` → la cancelación se ignora silenciosamente. El Exchange se crea normalmente.

En ningún caso el estado queda inconsistente.

---

### RC-07: Dos ofertas simultáneas del mismo usuario en la misma subasta

**Causa potencial:** Un usuario presiona "Ofertar" dos veces seguidas en la misma subasta antes de recibir respuesta del servidor.

**Solución — índice único parcial en MongoDB + manejo de `DuplicateKeyException`:** La colección de ofertas tiene un índice único parcial sobre `(AuctionId, BidderId)` filtrado a `Status == Active`. Si dos requests intentan insertar la oferta a la vez, MongoDB garantiza que solo uno logra la inserción; el segundo recibe `DuplicateKeyException`, que el repositorio convierte en `InvalidOperationException` → HTTP 409. El stock reservado por el request fallido se libera en el `catch`.

---

### RC-08: Edición concurrente de una figurita del usuario (PATCH)

**Causa potencial:** Dos pestañas o dispositivos del mismo usuario editan los detalles de una `UserSticker` simultáneamente.

**Solución — Optimistic Locking por campo `Version`:** El documento `UserSticker` tiene un campo `Version` numérico. Al leer, el cliente recibe la versión actual. Al actualizar, el repositorio ejecuta `ReplaceOne` condicionado a que `Version` coincida con la conocida. Si otro request ya actualizó el documento (incrementando `Version`), el filtro no matchea, `ModifiedCount = 0`, y se lanza `OptimisticConcurrencyException` → HTTP 409. El cliente debe releer el estado actualizado antes de reintentar.

---

### RC-09: Finalización concurrente de una subasta (aceptación manual vs. worker de expiración)

**Causa potencial:** El dueño de la subasta acepta una oferta manualmente al mismo tiempo que el worker automático de expiración intenta cerrar la subasta por vencimiento de tiempo.

**Solución — transición atómica de estado:** Tanto `TryMarkFinalizationCompletedAsync` (worker) como `TryCloseAuctionAtomicallyAsync` (manual) ejecutan `UpdateOne` con filtros condicionados al estado actual de la subasta. Solo el primero en ejecutarse logra la transición; el segundo encuentra el documento ya cerrado, no matchea y aborta. Esto garantiza que la subasta no se adjudica dos veces ni se cierra en un estado parcialmente procesado.

---

### RC-10: Double-spend — mismo sticker publicado simultáneamente en propuesta y subasta

**Causa potencial:** El usuario tiene una figurita con Qty = 1 e intenta crear simultáneamente una propuesta de intercambio y una subasta ofreciendo esa misma figurita.

**Solución — reserva atómica de stock compartida:** Tanto `CreateExchangeProposalAsync` como `CreateAuction` llaman a `TryReserveOneUnitAsync` como primer paso. Ambos compiten por el mismo `UpdateOne` con filtro `Quantity > 0`. Solo uno puede decrementar de 1 a 0; el segundo recibe `ModifiedCount = 0` → fallo → HTTP 400/409. No existe forma de publicar la misma figurita dos veces si el stock no lo permite.

---

### RC-11: Oferta colocada justo cuando la subasta cierra

**Causa potencial:** El usuario intenta colocar una oferta en el mismo instante en que la subasta expira o el dueño la cierra manualmente.

**Solución — re-validación de estado post-reserva:** Al crear una oferta (`CreateOfferAsync`), el flujo primero reserva el stock del bidder via `TryReserveOneUnitAsync` y luego re-verifica el estado de la subasta en MongoDB. Si la subasta ya cerró, el stock reservado se libera inmediatamente y se retorna un error. El cierre de subasta (`TryCloseAuctionAtomicallyAsync`) usa transición atómica, por lo que si la oferta se insertó primero, el cierre la considerará.

---

### RC-12: Múltiples ofertas de distintos usuarios en la misma subasta

**Causa potencial:** Muchos usuarios colocan ofertas simultáneamente en una subasta popular.

**Solución — sin contención cross-usuario:** Cada oferta reserva stock del `UserSticker` del bidder correspondiente, que pertenece a un único usuario. Los documentos son independientes entre sí, por lo que `TryReserveOneUnitAsync` opera sobre documentos distintos en paralelo sin contención. La inserción de las ofertas en la colección tampoco genera conflictos porque el índice único parcial es sobre `(AuctionId, BidderId)` — combinaciones distintas para distintos usuarios.

---

### RC-13: Cancelación de oferta mientras la subasta se adjudica

**Causa potencial:** Un bidder cancela su oferta en el mismo momento en que el dueño de la subasta acepta esa misma oferta como ganadora.

**Solución — transición atómica de estado:** `TryCancelOfferAtomicallyAsync` ejecuta `UpdateOne` condicionado a `State == Pending`. Si la adjudicación ya cambió el estado de la oferta a `Won`, el filtro no matchea y la cancelación retorna `false` → HTTP 409. El stock no se libera indebidamente. Si la cancelación llega primero, la adjudicación encuentra la oferta en `Cancelled` y falla de forma controlada.

---

### RC-14: Recalculación concurrente de la mejor oferta activa en una subasta

**Causa potencial:** Múltiples ofertas se insertan o cancelan simultáneamente, disparando varios recalculas del ranking de mejor oferta al mismo tiempo.

**Solución — operación de escritura acotada:** `SetBestCurrentOfferIdAsync` ejecuta un `$set` parcial que solo modifica el campo `BestCurrentOfferId`, sin tocar ningún otro campo del documento de subasta. Dos recalculaciones concurrentes pueden ejecutarse en paralelo sin corromperse mutuamente: la última en ejecutarse simplemente sobrescribe con el mismo o con un valor más actualizado, pero nunca deja el documento en un estado inválido.

---

### RC-15: Dos calificaciones simultáneas para el mismo intercambio

**Causa potencial:** Un usuario intenta calificar dos veces el mismo intercambio, o dos requests de calificación llegan al servidor a la vez.

**Solución — `$push` condicional atómico con `$not $elemMatch`:** `TryAddRatingAsync` ejecuta un `UpdateOne` que agrega el rating al array de calificaciones solo si no existe ya un elemento con el mismo `(RaterId, ExchangeId)`. La condición se evalúa y aplica de forma atómica en MongoDB. Si el mismo usuario intenta calificar dos veces, el segundo update no modifica el documento y retorna `ModifiedCount = 0` → error controlado. No es posible que queden duplicados de calificaciones en el array.
