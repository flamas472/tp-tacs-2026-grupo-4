# Informe de Arquitectura — FiguriTACS

> Snapshot del estado actual de la solución. Destinado a comparar con la planificación inicial y mostrar el alcance implementado.

---

## Estructura general

La solución está dividida en tres proyectos .NET 10:

| Proyecto | Tipo | Propósito |
|---|---|---|
| `Figuritas.Shared` | Class Library | Contratos compartidos entre API y cliente |
| `Figuritas.Api` | ASP.NET Core Web API | Servidor HTTP, lógica de negocio, persistencia |
| `Figuritas.Client` | Blazor WebAssembly | Interfaz de usuario SPA |
| `Figuritas.Api.Tests` | xUnit | Tests del backend |

La comunicación entre cliente y servidor es **exclusivamente HTTP/JSON**. El proyecto `Figuritas.Shared` evita duplicar tipos: tanto la API como el cliente referencian los mismos DTOs y modelos.

---

## Parte 1 — Dominio y datos (Figuritas.Shared + capa de datos en Api)

### ¿Qué es un DTO?

Un **DTO (Data Transfer Object)** es una clase cuyo único propósito es transportar datos entre capas o sistemas. No tiene lógica de negocio. En este proyecto cumplen dos roles:

- **Request DTOs** (`PostUserDTO`, `PostAuctionRequestDTO`, etc.): definen exactamente qué campos acepta cada endpoint. Permiten validar el input antes de que llegue al modelo de dominio y evitan que el cliente envíe campos que no debería modificar.
- **Response DTOs** (`AuctionResponseDTO`, `ExchangeProposalResponseDTO`, etc.): definen qué datos devuelve la API. Son más ricos que los Request DTOs porque incluyen datos calculados o enriquecidos (por ejemplo, `AuctionResponseDTO` incluye imagen y descripción del sticker subastado, aunque ese dato viva en otra entidad).

La separación DTO / Modelo de dominio es intencional: el modelo interno puede cambiar sin romper el contrato HTTP.

### Modelos de dominio

Ubicados en `Figuritas.Shared/Model/`, son las entidades del negocio:

- **Sticker** — figurita del catálogo (número, equipo, imagen, etc.)
- **UserSticker** — sticker que un usuario tiene en su inventario (quantity, flags de intercambio/subasta, referencia al Sticker)
- **MissingSticker** — sticker que un usuario marcó como faltante
- **ExchangeProposal** — propuesta de intercambio entre dos usuarios (estados: Pending, Accepted, Rejected, Cancelled)
- **Exchange** — registro de un intercambio concretado (se crea cuando se acepta una propuesta)
- **Auction / AuctionOffer / AuctionWatchlist** — subasta, ofertas recibidas, lista de seguimiento
- **User / Rate** — usuario y calificaciones
- **Notification** — alertas del sistema

Los modelos usan propiedades `required` de C# para forzar inicialización correcta.

### Persistencia — MongoDB

La API usa **MongoDB** como base de datos a través del driver oficial de C# (`MongoDB.Driver`). La arquitectura sigue el **patrón Repository**:

- Cada entidad tiene su interfaz (`IUserStickerRepository`, `IAuctionRepository`, etc.) y su implementación concreta.
- Los repositorios encapsulan todas las queries LINQ/Filter a MongoDB, dejando los servicios limpios de detalles de persistencia.
- Los documentos se guardan directamente como objetos C# (ODM implícito). Las referencias entre entidades se manejan por ID (sin joins), o como documentos embebidos (ej. `Sticker` embebido dentro de `UserSticker`).

### Capa de servicios

Entre los controllers y los repositorios existe una **capa de servicios** (`UserService`, `AuctionService`, `ExchangeProposalService`, `SuggestionService`, etc.) que contiene toda la lógica de negocio: validaciones, reserva de stock, enriquecimiento de DTOs de respuesta, notificaciones. Los servicios son la única capa que combina múltiples repositorios en una operación.

### Utilidades compartidas

`Figuritas.Shared/Utils/DtoExtensions.cs` define extensiones `ToPredicate()` sobre los DTOs de query (`GetUserStickersDTO`, `GetMarketStickersDTO`, etc.) que devuelven `Expression<Func<T, bool>>` compatibles con LINQ/MongoDB. Esto permite pasar filtros desde el cliente hasta la base de datos sin código repetido.

---

## Parte 2 — API HTTP (Figuritas.Api)

### Tecnología

La API es un **ASP.NET Core Web API** (no MVC con vistas). Define endpoints mediante **Controllers** con atributos de routing (`[Route]`, `[HttpGet]`, `[HttpPost]`, etc.). No utiliza Minimal APIs — cada controlador hereda de `ControllerBase`.

### Autenticación

Usa **JWT (JSON Web Tokens)**. El `AuthService` genera tokens con claims del usuario (id, username, rol). Los endpoints protegidos usan el atributo `[Authorize]`; los públicos llevan `[AllowAnonymous]`. En el cliente, los tokens se almacenan en `localStorage` y se adjuntan automáticamente a cada request HTTP mediante un `BearerTokenHandler` (DelegatingHandler).

### Organización de endpoints

Los controllers agrupan funcionalidad por dominio: `AuthController`, `UsersController`, `StickersController`, `MarketController`, `ExchangeProposalsController`, `AuctionsController`, `DashboardController`, `AdminController`, `RatingsController`. El `DashboardController` agrega datos del usuario autenticado (mis stickers, mis propuestas, mis subastas, notificaciones) evitando N+1 requests desde el cliente.

### Patrones notables

- **Enriquecimiento de DTOs en el servidor**: Los DTOs de respuesta incluyen campos desnormalizados (imagen del sticker en `AuctionResponseDTO`, username del proponente en `ExchangeProposalResponseDTO`) calculados con batch queries al momento de leer, para evitar que el cliente haga N+1 llamadas.
- **Rate limiting**: `ExchangeProposalService` tiene una ventana de tiempo configurable para evitar spam de propuestas.
- **Worker de cierre automático de subastas**: Un background service cierra subastas expiradas automáticamente.

---

## Parte 3 — Frontend (Figuritas.Client)

### Tecnología principal — Blazor WebAssembly

El cliente es una **SPA (Single Page Application)** construida con **Blazor WebAssembly** (.NET 10). Se compila a WebAssembly y corre íntegramente en el browser; no requiere server-side rendering. La navegación es client-side (sin recargas de página), gestionada por el `Router` de Blazor.

Blazor permite escribir la UI y la lógica en C#, lo que en este proyecto significa que el mismo lenguaje se usa de extremo a extremo (DTOs compartidos, modelos, lógica de filtros). Los componentes son archivos `.razor` que mezclan HTML con directivas Razor y bloques `@code { }`.

### Componentes — MudBlazor 9

La biblioteca de UI es **MudBlazor 9**, que provee componentes Material Design listos para usar: `MudCard`, `MudChip`, `MudTabs`, `MudDialog`, `MudNavMenu`, `MudGrid`, `MudHidden` (responsive breakpoints), entre muchos otros. Esto permitió construir una interfaz consistente y responsiva sin escribir CSS de componentes desde cero.

Los estilos globales y las clases personalizadas viven en `wwwroot/css/app.css`.

### Estructura de componentes

El frontend está fuertemente **componentizado**:

- **Layout**: `MainLayout` (AppBar + Drawer + NavMenu) y `NavMenu` con grupos colapsables.
- **Páginas** (`Pages/`): una por ruta. Mayoría delega la UI a componentes reutilizables.
- **Componentes de UI** (`Components/`): tarjetas de stickers (`StickerComponent`), diálogos de acción (`StickerActionDialog`, `MarketStickerActionDialog`, `AuctionDetailDialog`, `ProposalDetailDialog`, `SuggestionDetailDialog`), formularios (`NewAuctionRequestComponent`, `NewExchangeProposalComponent`, `NewAuctionOfferComponent`), selectores reutilizables (`StickerSelectionComponent`, `UserStickerSelectionComponent`, `SelectedStickerListComponent`, `SelectedUserStickerListComponent`), filas de listado (`ProposalRowComponent`, `SuggestionRowComponent`, `AuctionCardComponent`).
- **Componentes de infraestructura**: `RedirectToLogin`, `UnderConstruction`.

### HTTP Clients

Cada dominio tiene su propio cliente HTTP (`StickerHttpClient`, `AuctionHttpClient`, `DashboardHttpClient`, `ExchangeProposalHttpClient`, `AdminHttpClient`, etc.) que encapsula las llamadas a la API. Los métodos devuelven `ApiResponse<T>` (wrapper con `Success`, `Data`, `ErrorMessage`). El `BearerTokenHandler` inyectado en el `HttpClient` base agrega automáticamente el header `Authorization: Bearer {token}` a todos los requests.

### Autenticación en el cliente

`AuthStateProvider` (hereda de `AuthenticationStateProvider` de ASP.NET Core Identity) lee el token de `localStorage` y parsea los claims JWT para determinar el estado de autenticación. `AuthStateService` lo complementa exponiendo `UserId`, `Username` e `IsAdmin` al resto de la app. La directiva `[Authorize]` en las páginas y `<AuthorizeView>` en los componentes usan estos providers para mostrar/ocultar contenido.

---

## Estado vs planificación inicial

| Área | Planificado | Estado actual |
|---|---|---|
| Catálogo con filtros | ✅ | Implementado con filtros NT/Categoría/Número/Descripción |
| Gestión de repetidas | ✅ | CRUD completo desde catálogo (add/update/delete) |
| Gestión de faltantes | ✅ | CRUD completo desde catálogo |
| Mercado (repetidas de otros) | ✅ | Listado con filtros, propuesta directa desde tarjeta |
| Propuestas de intercambio | ✅ | Listado enviadas/recibidas con filtros de estado, aceptar/rechazar/cancelar |
| Intercambios finalizados | ✅ | Página dedicada con propuestas aceptadas |
| Sugerencias automáticas | ✅ | Algoritmo de match bilateral, pre-carga en propuesta |
| Subastas activas (ver/pujar) | ✅ | Listado público, modal con detalles y formulario de puja |
| Mis subastas | ✅ | Dashboard propio, re-usa componentes de subastas |
| Mis pujas | 🔲 | Página placeholder (falta endpoint de API) |
| Notificaciones | 🔲 | Endpoint implementado, UI pendiente |
| Administración | 🔲 | Endpoint de estadísticas implementado, UI pendiente |
| Calificaciones | 🔲 | Endpoint implementado, UI pendiente |

---

*Generado el 2 de junio de 2026.*
