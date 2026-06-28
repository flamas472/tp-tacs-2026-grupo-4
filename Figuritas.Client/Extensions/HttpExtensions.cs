// Archivo: Figuritas.Client/Extensions/HttpExtensions.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Figuritas.Client.Extensions
{
    public static class HttpExtensions
    {
        /// <summary>
        /// Shared JSON options that mirror the backend's serializer configuration:
        /// enums are serialized/deserialized as strings (e.g. "NewProposal" instead of 0).
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            // Case-insensitive matching ensures camelCase JSON keys from the backend
            // (e.g. "title", "createdAt") are correctly mapped to PascalCase C# properties.
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static async Task<T?> ProcesarRespuesta<T>(this HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ProcesarRespuesta<{typeof(T).Name}>] {ex.GetType().Name}: {ex.Message}");
                    return default;
                }
            }

            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"HTTP {(int)response.StatusCode} en {response.RequestMessage?.RequestUri?.PathAndQuery}");

            return default;
        }

        /// <summary>
        /// Translates an HTTP error response into a user-friendly Spanish message,
        /// without leaking internal server details or technical information.
        /// </summary>
        public static string GetFriendlyErrorMessage(HttpStatusCode statusCode, string rawBody)
        {
            // Check known body patterns first (case-insensitive).
            // This covers specific backend error strings before falling back to status codes.
            var body = rawBody.Trim().ToLowerInvariant();

            if (body.Contains("ratelimit") || body.Contains("rate limit") || body.Contains("too many requests"))
                return "Excedió la cantidad de intentos. Por favor, aguarde unos minutos y vuelva a intentarlo.";

            if (body.Contains("invalid credentials") || body.Contains("invalid password"))
                return "Las credenciales son incorrectas, o puede que el usuario no exista.";

            if (body.Contains("already exists") || body.Contains("username already") || body.Contains("ya existe"))
                return "Ya existe un usuario con ese nombre. Por favor, elija otro.";

            if (body.Contains("banned") || body.Contains("baneado"))
                return "Esta cuenta se encuentra baneada.";

            // Fall back to status-code translation.
            return statusCode switch
            {
                HttpStatusCode.Unauthorized
                    => "La sesión expiró o no está autenticado. Por favor, inicie sesión nuevamente.",
                HttpStatusCode.Forbidden
                    => "No tiene permiso para realizar esta acción.",
                HttpStatusCode.NotFound
                    => "El recurso solicitado no fue encontrado.",
                HttpStatusCode.Conflict
                    => "La operación no pudo completarse porque el recurso ya existe o está en un estado incompatible.",
                HttpStatusCode.UnprocessableEntity
                    => "Los datos enviados no son válidos. Verifique los campos e intente nuevamente.",
                HttpStatusCode.TooManyRequests
                    => "Excedió la cantidad de intentos. Por favor, aguarde unos minutos y vuelva a intentarlo.",
                _ when (int)statusCode >= 500
                    => "Ocurrió un problema inesperado. Por favor, intente nuevamente más tarde.",
                _ => "No se pudo completar la operación. Por favor, intente nuevamente."
            };
        }

        /// <summary>
        /// Returns a user-friendly message for network/connection errors.
        /// Does not expose the internal exception message.
        /// </summary>
        public static string GetFriendlyConnectionError()
            => "No se pudo conectar al servidor. Verifique su conexión a internet e intente nuevamente.";
    }
}