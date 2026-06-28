# Seguridad de la Aplicación Figuritas

**Proyecto:** Figuritas — Plataforma de Intercambio de Figuritas del Mundial  
**Fecha de última actualización:** 2026-06-28  
**Auditor:** security-auditor agent  
**Rama auditada:** `seguridad-concurrencia-backend`  
**Alcance:** Autenticación · Autorización · Validación de Input · Seguridad de API  

---

## Introducción

Este documento consolida el estado de seguridad de la API backend de Figuritas tras tres rondas de auditoría (dos de análisis y una de correcciones). Su propósito es servir como referencia rápida para el equipo de desarrollo y como punto de partida para auditores futuros: describe qué está implementado, cómo funciona, y qué decisiones se tomaron deliberadamente.

El documento refleja el código tal como existe en la rama `seguridad-concurrencia-backend` al 2026-06-28. Cualquier cambio posterior debe ser reflejado aquí manualmente.

---

## 1. Autenticación y Gestión de Sesión

### 1.1 Mecanismo principal — JWT Bearer

La API utiliza autenticación JWT con firma HMAC-SHA256. La clave secreta **no existe** en `appsettings.json` (el campo está seteado a `null`); debe proveerse en producción exclusivamente mediante la variable de entorno `Jwt__Key`. Si la clave no está configurada, la aplicación lanza una excepción al iniciar y no arranca.

| Parámetro | Valor |
|-----------|-------|
| Algoritmo | HMAC-SHA256 |
| Duración del access token | 15 minutos |
| Claims incluidos | `NameIdentifier` (userId), `Name` (username), `Role` |
| ValidateIssuer | `false` (decisión de equipo — ver Sección 6) |
| ValidateAudience | `false` (decisión de equipo — ver Sección 6) |

Código relevante: `Figuritas.Api/Services/AuthService.cs` → `GenerateToken()`, `Figuritas.Api/Program.cs` líneas 21–51.

### 1.2 Refresh Tokens

Se implementó un sistema de refresh tokens almacenados en MongoDB (colección `refresh_tokens`).

| Aspecto | Implementación |
|---------|---------------|
| Generación | `RandomNumberGenerator.GetBytes(64)` → Base64, criptográficamente seguro |
| Duración | 7 días |
| Almacenamiento | MongoDB con índice único sobre el campo `Token` |
| Rotación | En cada uso se revoca el token consumido y se emite uno nuevo (token rotation) |
| TTL automático | Índice de expiración en MongoDB — documentos eliminados automáticamente al vencer |
| Revocación por ban | `BanUserAsync` invoca `RevokeAllForUserAsync(userId)` antes de retornar |
| Ownership en logout | `RevokeRefreshTokenAsync` verifica que `stored.UserId == requestingUserId` antes de revocar |

Código relevante: `Figuritas.Api/Services/AuthService.cs` → `RefreshTokensAsync()`, `RevokeRefreshTokenAsync()`. `Figuritas.Api/Services/AdminService.cs` → `BanUserAsync()`. `Figuritas.Api/Repositories/RefreshTokenRepository.cs`.

### 1.3 Flujo de validación de credenciales

`UserService.ValidateCredentials()` realiza las verificaciones en este orden:

1. Busca el usuario por username. Si no existe → `null`
2. Verifica el password con BCrypt. Si no coincide → `null`
3. Verifica si el usuario está baneado. Si está baneado → `null`

En los tres casos la respuesta al cliente es idéntica (`401 Unauthorized: "Invalid credentials."`), por lo que no se filtran diferencias entre "usuario no existe", "contraseña incorrecta" y "baneado".

Código relevante: `Figuritas.Api/Services/UserService.cs` líneas 81–89.

### 1.4 Hashing de contraseñas

Se usa `BCrypt.Net.BCrypt.HashPassword()` con factor de costo por defecto (12 rondas). El hash se almacena en `User.HashedPassword`. Nunca se devuelve en ninguna respuesta.

### 1.5 SignalR — Token por query string

Para conexiones WebSocket al hub `/api/notification-hub`, el token JWT se acepta como query parameter `access_token`. Esto es un comportamiento estándar documentado de SignalR: los WebSockets del navegador no permiten enviar headers HTTP durante el handshake de upgrade. El token se extrae en `Program.cs` líneas 38–51 y se procesa por el mismo middleware JWT Bearer.

**Consecuencia operativa conocida:** el token puede quedar registrado en logs del servidor web / proxies inversos. El equipo acepta este tradeoff dado el contexto académico de la aplicación.

---

## 2. Autorización y Control de Acceso

### 2.1 Roles del sistema

| Rol | Descripción |
|-----|-------------|
| `User` | Usuario regular. Rol por defecto al registrarse. |
| `Admin` | Puede ver analíticas y gestionar usuarios (ban/unban). |
| `SuperAdmin` | Puede todo lo anterior más crear/promover/degradar administradores. |

El rol se incluye como claim `ClaimTypes.Role` en el JWT. No es modificable por el propio usuario en ningún endpoint.

### 2.2 Protección por controlador

| Controlador | Mecanismo |
|-------------|-----------|
| `AuthController` | Sin `[Authorize]`; los endpoints son públicos por diseño (login/register/refresh). Solo `logout` requiere `[Authorize]`. |
| `UsersController` | `[Authorize]` a nivel de controlador; endpoints de consulta pública usan `[AllowAnonymous]`. |
| `AdminController` | `[Authorize]` a nivel de controlador + `[Authorize(Roles = "Admin,SuperAdmin")]` o `[Authorize(Roles = "SuperAdmin")]` por endpoint. |
| `AuctionsController` | `[Authorize]` a nivel de controlador; `GET /{id}` y `GET /{id}/offers` usan `[AllowAnonymous]`. |
| `ExchangeProposalsController` | `[Authorize]` a nivel de controlador. |
| `DashboardController` | `[Authorize]` a nivel de controlador. |
| `MarketController` | `[Authorize]` a nivel de controlador. |
| `RatingsController` | `[Authorize]` a nivel de controlador. |
| `StickersController` | Sin `[Authorize]`; catálogo público de solo lectura. |
| `CategoryController`, `TeamController`, `NationalTeamController` | Sin `[Authorize]`; catálogo público de solo lectura. |

### 2.3 Checks de ownership (prevención de IDOR)

Todos los endpoints que operan sobre recursos de un usuario verifican que el recurso pertenece al caller autenticado. La verificación se realiza en el controlador o en el servicio:

| Endpoint | Verificación |
|----------|-------------|
| `POST /api/users/{userId}/stickers` | `authenticatedUserId != userId` → 403 |
| `PATCH /api/users/{userId}/stickers/{stickerId}` | `sticker.UserId != authenticatedUserId` → 403 (en `UserService`) |
| `DELETE /api/users/{userId}/stickers/{stickerId}` | `sticker.UserId != authenticatedUserId` → 403 (en `UserService`) |
| `POST /api/users/{userId}/missing-stickers` | `authenticatedUserId != userId` → 403 |
| `GET /api/users/{userId}/missing-stickers` | `authenticatedUserId != userId` → 403 |
| `DELETE /api/users/{userId}/missing-stickers/{stickerId}` | `authenticatedUserId != userId` → 403 |
| `PATCH /api/users/{id}` | `authenticatedUserId != id` → 403 |
| `GET /api/exchange-proposals/{id}` | caller debe ser proponent o proposed → 403 |
| `POST /api/exchange-proposals/{id}/accept` | `proposal.ProposedID != userId` → 400 |
| `POST /api/exchange-proposals/{id}/reject` | `proposal.ProposedID != userId` → 403 |
| `POST /api/exchange-proposals/{id}/cancel` | `proposal.ProponentID != userId` → 403 |
| `POST /api/auctions/{id}/close` | ownership verificado en `AuctionService.CloseAuction`/`AcceptOfferAsync` |
| `DELETE /api/auctions/{auctionId}/offers/{offerId}` | ownership verificado en `AuctionService.CancelOfferAsync` |
| `PATCH /api/auctions/{auctionId}/offers/{offerId}` | ownership verificado en `AuctionService.UpdateOfferAsync` |
| `PATCH /api/dashboard/notifications/{id}/read` | `UnauthorizedAccessException` si la notificación no pertenece al caller → 403 |
| `POST /api/admin/users/{userId}/ban` | No puede banear su propia cuenta; no puede banear admins |
| `DELETE /api/admin/admins/{id}/role` | No puede revocar su propio rol; no puede revocar SuperAdmins |

### 2.4 Ratings — verificación de participación

`UserService.CreateUserRate()` verifica:
- El rater no puede calificarse a sí mismo
- El exchange debe existir
- El rater debe ser participante del exchange (proponent o proposed)
- El target debe ser el otro participante
- Un rater no puede calificar más de una vez por exchange (`alreadyRated` check)

---

## 3. Rate Limiting

Las políticas están configuradas en `Program.cs` y son configurables por `appsettings.json` (o variables de entorno), lo que permite deshabilitarlas en tests de integración sin recompilar.

### 3.1 Endpoints con rate limit

| Endpoint | Policy | Límite por defecto | Ventana | Partition key |
|----------|--------|--------------------|---------|---------------|
| `POST /api/auth/login` | `login` | 5 req | 60 s | IP del cliente |
| `POST /api/auth/register` | `register` | 5 req | 60 s | IP del cliente |
| `POST /api/auth/refresh` | `refresh` | 20 req | 60 s | IP del cliente |

Respuesta al exceder el límite: HTTP 429.

### 3.2 Endpoints sin rate limit (con justificación)

| Endpoint | Justificación |
|----------|--------------|
| `POST /api/auth/logout` | Acción única por sesión; requiere JWT válido |
| `GET /api/users`, `GET /api/users/{id}` | Datos públicos, sin costo computacional significativo; IDs no secuenciales (MongoDB ObjectId-inspired) |
| `PATCH /api/users/{id}` | Requiere autenticación + ownership; el abuso solo afecta la propia cuenta |
| `POST /api/users/{userId}/stickers` | Requiere autenticación + ownership; catálogo finito, duplicados rechazados por negocio |
| `POST /api/users/{userId}/missing-stickers` | Igual que el anterior |
| `POST /api/exchange-proposals` | Requiere figuritas propias como colateral real |
| `POST /api/auctions` | Requiere figuritas propias como colateral real |
| `POST /api/auctions/{auctionId}/offers` | Consume inventario real del usuario |
| `POST /api/ratings` | Unicidad por `(raterId, exchangeId)` impuesta por lógica de negocio |
| `GET /api/stickers`, `/categories`, `/teams` | Catálogo estático de solo lectura |

---

## 4. Validación de Input

### 4.1 Registro de usuario — `PostUserDTO`

| Campo | Validación |
|-------|-----------|
| `Username` | Requerido; unicidad verificada en `UserService.CreateUser` |
| `Password` | `[MinLength(8)]` + regex `^(?=.*[A-Z])(?=.*\d).+$` (mínimo una mayúscula y un dígito) |

El incumplimiento de cualquier anotación es rechazado por el pipeline de validación de modelo de ASP.NET Core con HTTP 400 antes de llegar al controlador.

### 4.2 Actualización de usuario — `PatchUserDTO`

| Campo | Validación |
|-------|-----------|
| `Username` | Sin anotaciones de formato; verificación de unicidad en base de datos |
| `Password` | `[MinLength(8)]` + regex `^(?=.*[A-Z])(?=.*\d).+$` (igual que `PostUserDTO`) — corregido en NUEVO-001 |

### 4.3 Creación de administrador — `CreateAdminRequestDTO`

| Campo | Validación |
|-------|-----------|
| `Username` | Requerido; unicidad verificada en `AdminService.CreateAdmin` |
| `Password` | `[MinLength(8)]` + regex `^(?=.*[A-Z])(?=.*\d).+$` (igual que `PostUserDTO`) — corregido en NUEVO-002 |

### 4.4 Otros DTOs

| DTO | Validaciones destacadas |
|-----|------------------------|
| `PostRatingRequestDTO` | `Stars`: `[Range(1, 5)]`; `Comment`: `[MaxLength(500)]` |
| `GetAdmins`, `GetUsers` | `page`: `[Range(1, int.MaxValue)]`; `pageSize`: `[Range(1, 100)]` |
| `PostAuctionRequestDTO` | Sin anotaciones de validación en el DTO; validación de negocio en el servicio |

---

## 5. Protección de Datos

### 5.1 Campos sensibles en respuestas

Ningún endpoint devuelve el campo `HashedPassword`. La proyección a DTOs es manual (sin AutoMapper), lo que elimina el riesgo de mass assignment inverso.

`UserResponseDTO` incluye el campo `Role` pero con `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. Los endpoints públicos no populan este campo, por lo que no aparece en el JSON de respuesta.

### 5.2 Mass Assignment

No hay AutoMapper ni model binding directo de entidades. Cada endpoint usa DTOs explícitos con campos predefinidos. Un cliente no puede enviar campos como `Role`, `Banned`, `HashedPassword` o `Reputation` y tener efecto en la base de datos.

### 5.3 Swagger

Swagger y la redirección a `/swagger/index.html` están habilitados **únicamente en el entorno de desarrollo** (`if (app.Environment.IsDevelopment())`). En producción no se expone.

### 5.4 CORS

Los orígenes permitidos se configuran en `AllowedOrigins` (appsettings / variables de entorno). Por defecto en desarrollo: `http://localhost:5280,http://localhost:5048`. No se usa wildcard (`*`). La policy `BlazorLocalPolicy` requiere `AllowCredentials()` que es incompatible con orígenes wildcard.

---

## 6. Hallazgos Descartados (decisión del equipo)

Estos ítems fueron identificados como vulnerabilidades técnicas en auditorías anteriores, pero el equipo tomó la decisión explícita de no corregirlos. Deben ser revisados si el contexto de despliegue cambia.

### DESCARTADO-001 — JWT secret key en `appsettings.Development.json`

**Descripción:** La clave secreta JWT estaba hardcodeada en el archivo de configuración de desarrollo, que podría ser incluido en el repositorio git.  
**Estado actual:** `appsettings.json` tiene `"Key": null`. La clave debe proveerse vía variable de entorno `Jwt__Key`. El archivo `appsettings.Development.json` puede existir localmente con valores de desarrollo.  
**Justificación del equipo:** Contexto académico. El secret de producción se maneja mediante variables de entorno en el despliegue en la nube. El valor en development es desechable.  
**Riesgo residual:** Bajo. Aplica solo al entorno de desarrollo local; no afecta producción si la variable de entorno está correctamente configurada.

### DESCARTADO-002 — JWT sin validación de Issuer ni Audience

**Descripción:** `ValidateIssuer = false` y `ValidateAudience = false` en `TokenValidationParameters`. Esto significa que un token firmado con la misma clave para otro servicio hipotético sería aceptado.  
**Justificación del equipo:** La aplicación es un sistema monolítico con un único emisor y un único consumidor. No existe infraestructura multi-servicio que justifique estas validaciones en el contexto actual.  
**Riesgo residual:** Bajo en la arquitectura actual. Si se introduce un segundo servicio que comparta la clave JWT, este riesgo escalaría.

---
