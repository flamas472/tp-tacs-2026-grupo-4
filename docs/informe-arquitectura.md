# Informe de Arquitectura — FiguriTACS

---

## Estructura general

La solución está dividida en cuatro proyectos .NET 10:

| Proyecto | Tipo | Propósito |
|---|---|---|
| `Figuritas.Shared` | Class Library | Contratos compartidos entre API y cliente: modelos de dominio, DTOs, enums, utilidades |
| `Figuritas.Api` | ASP.NET Core Web API | Servidor HTTP, lógica de negocio, persistencia, workers, hubs |
| `Figuritas.Client` | Blazor WebAssembly | Interfaz de usuario SPA |
| `Figuritas.Api.Tests` | xUnit | Tests del backend (~205 métodos `[Fact]`/`[Theory]` en 24 archivos) |

La comunicación entre cliente y servidor es **HTTP/JSON** para todas las operaciones de datos, y **WebSocket (SignalR)** para notificaciones en tiempo real. El proyecto `Figuritas.Shared` evita duplicar tipos: tanto la API como el cliente referencian los mismos DTOs y modelos.

---

## Parte 1 — Dominio y datos (Figuritas.Shared + capa de datos en Api)

### Modelos de dominio

Ubicados en `Figuritas.Shared/Model/`, organizados en subdirectorios por contexto:

**Figuritas (catálogo — inmutable):**
- `Sticker` — figurita del catálogo (número, equipo, imagen, descripción, etc.)
- `Category` — categoría de sticker
- `NationalTeam` — selección nacional
- `Team` — equipo dentro de una selección

**Usuarios:**
- `User` — usuario con `Id`, `Username`, `HashedPassword`, `Role` (User/Admin/SuperAdmin), `Banned`, `TokenValidFrom`, `Ratings` (subdocumento), tres flags de preferencias de notificación, y `Reputation` calculada (promedio de estrellas de sus `Ratings`)
- `UserSticker` — sticker que un usuario tiene en inventario: `Quantity`, `CanBeDirectlyExchanged`, `CanBeAuctioned`, `Active`, `Version` (control de concurrencia optimista)
- `MissingSticker` — sticker marcado como faltante por un usuario
- `Rate` — calificación embebida dentro de `User.Ratings`

**Intercambios:**
- `ExchangeProposal` — propuesta de intercambio entre dos usuarios. Estados: `Pending`, `Accepted`, `Rejected`, `Cancelled`
- `Exchange` — registro del intercambio concretado (se crea al aceptar una propuesta)

**Subastas:**
- `Auction` — subasta publicada por un usuario. Campos de coordinación de worker: `AuctionEndingNotificationSent`, `AutoClosureClaimedAt`, `FinalizationCompleted`, `BestCurrentOfferId`, `UserSelectedBestOfferId`
- `AuctionOffer` — puja realizada por un postor sobre una subasta. Estados: `Pending`, `Won`, `Lost`, `Cancelled`
- `AuctionStatus` — enum: `Active`, `Closed`, `Cancelled`
- `AuctionWatchlist` — entrada de seguimiento de una subasta por un usuario

**Notificaciones:**
- `Notification` — alerta persistida con `Type`, `Title`, `Message`, `IsRead`, `ExpiresAt`, `ReferenceId` (para idempotencia)
- `NotificationType` — enum: `NewProposal`, `AuctionEnding`, `MissingStickerAvailable`

**Autenticación:**
- `RefreshToken` — token de refresco con `UserId`, `Token`, `ExpiresAt`, `IsRevoked`

### DTOs

Los DTOs están en `Figuritas.Shared/DTO/` y se dividen en:
- **Request DTOs** (`PostUserDTO`, `PostAuctionRequestDTO`, `PostExchangeProposalRequestDTO`, etc.): definen exactamente qué campos acepta cada endpoint. Incluyen anotaciones de validación (`[Required]`, `[MinLength]`, `[RegularExpression]`).
- **Response DTOs** (`AuctionResponseDTO`, `ExchangeProposalResponseDTO`, `MyBidResponseDTO`, etc.): contienen datos enriquecidos y desnormalizados calculados en el servidor para evitar N+1 desde el cliente.

### Persistencia — MongoDB

La API usa **MongoDB** con el driver oficial de C#. La arquitectura sigue el **patrón Repository**:

- Cada entidad tiene su interfaz (`IUserStickerRepository`, `IAuctionRepository`, etc.) y su implementación concreta bajo `Figuritas.Api/Repositories/`.
- Los repositorios encapsulan todas las queries y operaciones atómicas sobre MongoDB.
- Los IDs son enteros autogenerados mediante `MongoIdGenerator` (contador por colección), no ObjectId.
- Las referencias entre entidades se manejan por ID (sin joins). El `Sticker` del catálogo se embebe directamente en `UserSticker` para evitar lookups frecuentes.

Índices creados al iniciar la aplicación:
- `ExchangeProposals`: `ProponentID`, `ProposedID`, `State`, `CreatedAt`
- `Auctions`: `Status`, `EndsAt`, índice compuesto `{Status, EndsAt}`

### Capa de servicios

Entre los controllers y los repositorios existe una **capa de servicios** que contiene toda la lógica de negocio:

| Servicio | Responsabilidad principal |
|---|---|
| `AuthService` | Generación y validación de JWT, rotación de refresh tokens |
| `UserService` | CRUD de usuarios, inventario (UserSticker), faltantes, calificaciones |
| `StickerService` | Consulta del catálogo |
| `ExchangeProposalService` | Creación, aceptación, rechazo, cancelación de propuestas; reserva atómica de stock; auto-rechazo de competidoras |
| `ExchangeService` | Creación del Exchange al concretar un intercambio |
| `AuctionService` | Lifecycle de subastas: crear, ofertar, modificar oferta, seleccionar ganador, cerrar, cancelar, cierre automático |
| `AuctionWatchlistService` | Seguimiento de subastas (add/remove/get watchlist) |
| `SuggestionService` | Algoritmo de sugerencias bilaterales (batch queries, evita N+1) |
| `NotificationService` | Envío de notificaciones: persiste en BD + entrega en tiempo real vía SignalR (best-effort); respeta preferencias del usuario; idempotencia por `ReferenceId` |
| `AdminService` | Gestión de administradores, ban/unban de usuarios, revocación de tokens |
| `AdminAnalyticsService` | Estadísticas de plataforma: usuarios, intercambios, subastas, tráfico de notificaciones |
| `CategoryService`, `NationalTeamService`, `TeamService` | Catálogo de selecciones, equipos y categorías |

### Utilidades compartidas

`Figuritas.Shared/Utils/DtoExtensions.cs` define extensiones `ToPredicate()` sobre los DTOs de query que devuelven `Expression<Func<T, bool>>` compatibles con LINQ/MongoDB, permitiendo trasladar filtros del cliente a la base de datos sin código repetido.

---

## Parte 2 — API HTTP (Figuritas.Api)

### Tecnología

La API es un **ASP.NET Core Web API** (.NET 10). Los endpoints se definen mediante Controllers con atributos de routing. No utiliza Minimal APIs. Cada controller hereda de `ControllerBase`.

### Endpoints por controller

#### `AuthController` — `/api/auth`

| Método | Ruta | Acceso | Detalles |
|---|---|---|---|
| POST | `/login` | Anónimo | Rate limit: 5 req/60 s por IP |
| POST | `/register` | Anónimo | Rate limit: 5 req/60 s por IP |
| POST | `/refresh` | Anónimo | Rate limit: 20 req/60 s por IP |
| POST | `/logout` | Autenticado | Revoca el refresh token |

#### `UsersController` — `/api/users`

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/` (`?username=`) | Anónimo |
| GET | `/{id}` | Anónimo |
| GET | `/{id}/reputation` | Anónimo |
| GET | `/{id}/completed-exchanges` | Anónimo |
| PATCH | `/{id}` | Autenticado, owner |
| GET | `/{userId}/stickers` | Anónimo |
| POST | `/{userId}/stickers` | Autenticado, owner |
| GET | `/{userId}/stickers/{stickerId}` | Autenticado |
| PATCH | `/{userId}/stickers/{stickerId}` | Autenticado, owner (concurrencia optimista) |
| DELETE | `/{userId}/stickers/{stickerId}` | Autenticado, owner |
| GET | `/{userId}/missing-stickers` | Autenticado, owner |
| POST | `/{userId}/missing-stickers` | Autenticado, owner |
| DELETE | `/{userId}/missing-stickers/{stickerId}` | Autenticado, owner |
| GET | `/{userId}/ratings` | Autenticado |

#### `StickersController` — `/api/stickers`

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/` (con filtros) | Anónimo |
| GET | `/{id}` | Anónimo |

#### `MarketController` — `/api/market`

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/stickers` | Autenticado |
| GET | `/suggestions` | Autenticado |

#### `ExchangeProposalsController` — `/api/exchange-proposals`

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/{id}` | Autenticado, participante |
| POST | `/` | Autenticado |
| POST | `/{id}/accept` | Autenticado, receptor |
| POST | `/{id}/reject` | Autenticado, receptor |
| POST | `/{id}/cancel` | Autenticado, proponente |
| GET | `/{id}/exchange` | Autenticado, participante |

#### `AuctionsController` — `/api/auctions`

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Anónimo |
| POST | `/` | Autenticado |
| POST | `/{id}/close` | Autenticado, dueño (legacy) |
| GET | `/{auctionId}/offers` | Anónimo |
| POST | `/{auctionId}/offers` | Autenticado |
| DELETE | `/{auctionId}/offers/{offerId}` | Autenticado, dueño de la oferta |
| PATCH | `/{auctionId}/offers/{offerId}` | Autenticado, dueño de la oferta |
| PATCH | `/{auctionId}/selected-offer` | Autenticado, dueño de la subasta |
| DELETE | `/{auctionId}/selected-offer` | Autenticado, dueño de la subasta |
| POST | `/{auctionId}/offers/{offerId}/accept` | Autenticado, dueño de la subasta |
| POST | `/{id}/watch` | Autenticado |
| DELETE | `/{id}/watch` | Autenticado |
| GET | `/watchlist` | Autenticado |

#### `DashboardController` — `/api/dashboard`

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/stickers` | Mis stickers publicados |
| GET | `/proposals/sent` | Mis propuestas enviadas |
| GET | `/proposals/received` | Mis propuestas recibidas |
| GET | `/auctions` | Mis subastas |
| GET | `/bids` | Mis pujas en subastas ajenas |
| PUT | `/preferences` | Actualizar preferencias de notificación |
| GET | `/notifications` | Mis notificaciones (paginadas) |
| PATCH | `/notifications/{id}/read` | Marcar notificación como leída |

#### `AdminController` — `/api/admin`

| Método | Ruta | Roles requeridos |
|---|---|---|
| GET | `/analytics/summary` | Admin, SuperAdmin |
| POST | `/admins` | SuperAdmin |
| GET | `/admins` | SuperAdmin |
| PATCH | `/admins/{id}/role` | SuperAdmin |
| DELETE | `/admins/{id}/role` | SuperAdmin |
| GET | `/users` | Admin, SuperAdmin |
| POST | `/users/{userId}/ban` | Admin, SuperAdmin |
| POST | `/users/{userId}/unban` | Admin, SuperAdmin |

#### `RatingsController` — `/api/ratings`

| Método | Ruta | Acceso |
|---|---|---|
| POST | `/` | Autenticado (debe ser participante del intercambio) |

#### Otros controllers de catálogo

`CategoryController`, `NationalTeamController`, `TeamController` exponen endpoints de solo lectura para el catálogo de selecciones, equipos y categorías.

### SignalR Hub

`NotificationHub` en `/api/notification-hub` — requiere autenticación. El token JWT se pasa como query param `access_token` porque las conexiones WebSocket no soportan headers HTTP en la fase de upgrade. Método del servidor hacia el cliente: `ReceiveNotification(NotificationResponseDTO)`.

### Patrones notables

- **Enriquecimiento de DTOs en el servidor**: los DTOs de respuesta incluyen campos desnormalizados (imagen del sticker en `AuctionResponseDTO`, username del proponente en `ExchangeProposalResponseDTO`, info de calificación emitida en propuestas aceptadas) calculados con batch queries, para evitar N+1 desde el cliente.
- **Rate limiting por propuesta**: `ExchangeProposalService` rechaza una nueva propuesta si el mismo usuario ya envió una en los últimos N segundos (configurable via `RateLimit:ExchangeProposalWindowSeconds`, por defecto 3 s).
- **Worker de gestión de subastas**: `AuctionEndingWorker` (background service) corre cada 5 minutos, detecta subastas próximas a vencer (en los próximos 30 min) y envía notificaciones a sus seguidores, y cierra automáticamente las vencidas.

---

## Parte 3 — Seguridad

### Autenticación JWT

- **Access token**: expira a los **15 minutos**. Incluye claims `NameIdentifier`, `Name`, `Role`, e `iat` (issued-at Unix timestamp explícito).
- **Refresh token**: expira a los **7 días**, almacenado en MongoDB. Rotación al refrescar: el token viejo se revoca y se emite un par nuevo.
- **Revocación inmediata al banear**: al bannear un usuario se actualiza `User.TokenValidFrom = DateTime.UtcNow` y se revocan todos sus refresh tokens activos. El handler `OnTokenValidated` compara el `iat` del JWT contra `TokenValidFrom` y rechaza tokens emitidos antes del baneo con HTTP 401.
- **Logout**: revoca el refresh token indicado, dejando el access token expirar naturalmente (15 min).

### Hashing de contraseñas

BCrypt.Net (`BCrypt.Net.BCrypt.HashPassword`/`Verify`). Las contraseñas en texto plano nunca se persisten.

### Política de contraseñas

Validada en `PostUserDTO` y `CreateAdminRequestDTO` vía data annotations:
- Mínimo 8 caracteres (`[MinLength(8)]`)
- Al menos una mayúscula y un dígito (`[RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$")]`)

### Rate limiting (ASP.NET Core RateLimiter)

Política de ventana fija, particionada por IP remota. Configurable por variables de entorno:

| Política | Default | Ventana |
|---|---|---|
| `login` | 5 requests | 60 s |
| `register` | 5 requests | 60 s |
| `refresh` | 20 requests | 60 s |

Las políticas se desactivan fácilmente en tests de integración vía `RateLimit:*Enabled = false`. El código de rechazo es HTTP 429.

### Control de acceso

- `[Authorize]` por defecto en todos los controllers salvo `StickersController`.
- `[AllowAnonymous]` selectivo para endpoints públicos (perfil de usuario, catálogo, detalle de subasta, listado de stickers de un usuario).
- Roles: `User`, `Admin`, `SuperAdmin` via `[Authorize(Roles = "...")]`.
- Verificación de ownership manual en cada acción que modifica recursos ajenos (retorna HTTP 403 con mensaje descriptivo en inglés).

### CORS

Configurado dinámicamente mediante la variable de entorno `AllowedOrigins` (lista separada por comas). El origen no está hardcodeado en el código.

---

## Parte 4 — Concurrencia

El sistema gestiona varios escenarios de alta concurrencia mediante operaciones atómicas en MongoDB:

### Concurrencia optimista en UserSticker

El campo `Version` (entero) actúa como token de versión. En `UserStickerRepository.Update`:
1. El filtro de `ReplaceOne` incluye `Version == expectedVersion`.
2. Si ningún documento matchea (`MatchedCount == 0`), otro proceso lo modificó primero → se lanza `OptimisticConcurrencyException`.
3. El controller de `PATCH /{userId}/stickers/{stickerId}` retorna **HTTP 409 Conflict**.
4. `UserService.UpdateUserSticker` reintenta automáticamente hasta 3 veces con re-lectura fresh antes de lanzar la excepción al controller.

### Reserva atómica de stock

`UserStickerRepository.TryReserveOneUnitAsync` ejecuta un `UpdateOneAsync` con filtro `{ Id, Quantity > 0, Active = true }` e incremento `-1`. Solo retorna `true` si `ModifiedCount == 1`. Esto garantiza que si dos propuestas intentan reservar la última unidad de un sticker en paralelo, solo una tiene éxito.

`ExchangeProposalService` usa este mecanismo al crear propuestas: reserva cada sticker ofrecido de forma secuencial; si alguna reserva falla, revierte las anteriores (`IncrementQuantityAndActivateAsync`) antes de lanzar excepción.

### Aceptación atómica de propuestas

`ExchangeProposalRepository.AcceptAtomically` usa `FindOneAndUpdate` con filtro `{ Id, State == Pending }`. Garantiza que aunque dos usuarios intenten aceptar la misma propuesta en paralelo, solo una transición a `Accepted` tiene efecto. Si el documento ya no está en `Pending` (fue aceptado, rechazado o cancelado por otra operación concurrente), retorna `null` y el controller devuelve **HTTP 409 Conflict**.

### Transición atómica de estado (rechazo/cancelación)

`TryTransitionFromPendingAsync` usa el mismo patrón `FindOneAndUpdate` con filtro `State == Pending`. Si la propuesta ya cambió de estado, la operación es no-op y se retorna `null`, lo que eleva un `InvalidOperationException` al controller (HTTP 409).

### Auto-rechazo de propuestas competidoras

Cuando se acepta una propuesta, `ExchangeProposalService.AutoRejectPendingProposalsForRequestedStickerAsync` rechaza atómicamente todas las demás propuestas pendientes que apuntaban al mismo `RequestedUserStickerId`, y devuelve el stock reservado a sus proponentes.

### Coordinación del worker de subastas

`AuctionEndingWorker` usa dos flags atómicos para garantizar idempotencia frente a múltiples instancias del worker:
- `TryClaimEndingNotificationAsync`: marca `AuctionEndingNotificationSent = true` condicionalmente (solo si era `false`). El primer worker que ejecuta el `UpdateOne` es el único que envía la notificación.
- `TryClaimAutomaticClosureAsync`: marca `AutoClosureClaimedAt = now` condicionalmente (solo si era `null`). El primer worker que ejecuta la operación es el único que cierra la subasta.

Adicionalmente, `AuctionService.FinalizeClosedAuctionAsync` está guardado por `FinalizationCompleted = true`, seteado atómicamente (`TryMarkFinalizationCompletedAsync`) antes de transferir inventario, para evitar doble-finalización.

---

## Parte 5 — Frontend (Figuritas.Client)

### Tecnología

SPA en **Blazor WebAssembly** (.NET 10) con **MudBlazor 9** (Material Design). Corre íntegramente en el browser. La navegación es client-side sin recargas, gestionada por el `Router` de Blazor.

### Páginas disponibles

| Ruta | Página | Acceso |
|---|---|---|
| `/` | `Home` | Público |
| `/ingresar` | `Ingresar` (login/registro) | Público |
| `/catalogo` | `Catalogo` | Autenticado |
| `/repetidas` | `Repetidas` (mi inventario) | Autenticado |
| `/faltantes` | `Faltantes` | Autenticado |
| `/mercado` | `Mercado` | Autenticado |
| `/sugerencias` | `Sugerencias` | Autenticado |
| `/propuestas` | `Propuestas` | Autenticado |
| `/intercambios` | `Intercambios` | Autenticado |
| `/subastas` | `Subastas` | Autenticado |
| `/mis-subastas` | `MisSubastas` | Autenticado |
| `/mis-pujas` | `MisPujas` | Autenticado |
| `/notificaciones` | `Notificaciones` | Autenticado |
| `/perfil/{Username}` | `Perfil` | Autenticado |
| `/admin/dashboard` | `Admin/Dashboard` | Admin, SuperAdmin |

### Componentes principales

**Diálogos de acción:**
`StickerActionDialog`, `UserStickerActionDialog`, `MarketStickerActionDialog`, `AuctionDetailDialog`, `AuctionOwnerDetailDialog`, `ProposalDetailDialog`, `SuggestionDetailDialog`, `ExchangeDetailDialog`, `NewRatingDialog`, `MissingStickerActionDialog`, `PromoteAdminDialog`

**Formularios/flujos:**
`NewAuctionRequestComponent`, `NewAuctionOfferComponent`, `NewExchangeProposalComponent`

**Selectores reutilizables:**
`StickerSelectionComponent`, `UserStickerSelectionComponent`, `SelectedStickerListComponent`, `SelectedUserStickerListComponent`

**Listados:**
`StickerListComponent`, `UserStickerListComponent`, `AuctionCardComponent`, `ProposalRowComponent`, `SuggestionRowComponent`, `ProposalListPanel`, `OfferPreviewComponent`

**Notificaciones:**
`NotificationDropdownComponent` (campana en el header con badge de no leídas y dropdown), página `Notificaciones` (listado completo con filtros por estado de lectura)

**Infraestructura:**
`RedirectToLogin`, `UnderConstruction`

### HTTP Clients

Cada dominio tiene su propio cliente HTTP tipado (`StickerHttpClient`, `AuctionHttpClient`, `DashboardHttpClient`, `ExchangeProposalHttpClient`, `ExchangeHttpClient`, `AdminHttpClient`, etc.) que encapsulan las llamadas a la API y retornan `ApiResponse<T>`.

### Autenticación en el cliente

- `AuthStateProvider` (hereda de `AuthenticationStateProvider`): lee los tokens de `localStorage`, parsea los claims JWT para determinar el estado de autenticación, y detecta expiración del access token en el startup. Expone `OnTokenRemoved` para que los servicios limpien estado en memoria.
- `AuthStateService`: expone `UserId`, `Username`, `IsAdmin`, `GetTokenAsync()` al resto de la app.
- `BearerTokenHandler` (`DelegatingHandler`): adjunta automáticamente `Authorization: Bearer {token}` a todos los requests HTTP. Si el servidor responde con HTTP 401, silencia el error y borra los tokens del `localStorage`, forzando al usuario a la pantalla de login en la próxima navegación.

### Notificaciones en tiempo real

`NotificationHubService` abre una conexión SignalR al hub `/api/notification-hub` con reconexión automática (`WithAutomaticReconnect`). El token JWT se provee via `AccessTokenProvider`. Cuando se recibe el evento `ReceiveNotification`, el servicio lo dispara a todos los suscriptores (campana del header, página de notificaciones). La entrega por HTTP polling (`GET /api/dashboard/notifications`) actúa como fallback si SignalR no está disponible.

---

## Parte 6 — Workers y tareas en segundo plano

### AuctionEndingWorker

`BackgroundService` registrado en el DI container. Ciclo de 5 minutos:

1. **ProcessEndingAuctionsAsync**: busca subastas activas cuya `EndsAt` sea dentro de los próximos 30 minutos y que aún no tengan `AuctionEndingNotificationSent = true`. Reclama atómicamente la notificación, luego envía alertas a todos los seguidores (watchlist) de esa subasta.

2. **ProcessExpiredAuctionsAsync**: busca subastas activas cuya `EndsAt` ya pasó. Reclama atómicamente el cierre via `TryClaimAutomaticClosureAsync`. Delega el cierre completo a `AuctionService.CloseAuctionAutomatically`, que selecciona el ganador, transfiere el inventario y actualiza estados.

El worker tiene un parámetro inyectable `TimeProvider` que permite controlar el tiempo en tests de integración.

---

## Estado vs planificación inicial

| User Story | Descripción | Estado |
|---|---|---|
| US01 | Publicar figurita repetida | Implementado — CRUD completo, notifica usuarios con esa figurita como faltante |
| US02 | Registrar faltantes | Implementado — CRUD completo desde catálogo |
| US03 | Buscar figuritas disponibles | Implementado — filtros por NT/Categoría/Número/Descripción |
| US04 | Sugerencias automáticas | Implementado — algoritmo bilateral, evita N+1 con batch queries |
| US05 | Propuesta de intercambio | Implementado — creación, aceptar/rechazar/cancelar, reserva atómica de stock |
| US06 | Publicar subasta | Implementado — duración configurable, cierre automático por worker |
| US07 | Ofertar en subasta | Implementado — crear/modificar/cancelar puja, selección de ganador |
| US08 | Ver mis publicaciones y estado | Implementado — Dashboard (mis stickers, propuestas, subastas, mis pujas) |
| US09 | Aceptar o rechazar propuestas | Implementado — con auto-rechazo de competidoras y rollback en caso de error |
| US10 | Calificar tras intercambio | Implementado — backend + diálogo `NewRatingDialog` en `Intercambios`, vista de calificación ya emitida |
| US11 | Alertas al usuario | Implementado — notificaciones persistidas, entrega real-time vía SignalR, campana en header, página completa, preferencias configurables |
| US12 | Estadísticas de administrador | Implementado — endpoint analytics + panel `/admin/dashboard` con estadísticas, gestión de admins y ban/unban de usuarios |

---
