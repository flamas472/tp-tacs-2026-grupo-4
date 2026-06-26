// Archivo: Figuritas.Client/Extensions/HttpExtensions.cs
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
            Console.WriteLine($"Error del servidor: {response.StatusCode} - {error}");

            return default;
        }
    }
}