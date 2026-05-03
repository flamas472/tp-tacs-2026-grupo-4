// Archivo: Figuritas.Client/Extensions/HttpExtensions.cs
using System.Net.Http.Json;

namespace Figuritas.Client.Extensions
{
    public static class HttpExtensions
    {
        public static async Task<T?> ProcesarRespuesta<T>(this HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                try 
                {
                    return await response.Content.ReadFromJsonAsync<T>();
                }
                catch
                {
                    // Error de deserialización
                    return default;
                }
            }

            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error del servidor: {response.StatusCode} - {error}");

            return default;
        }
    }
}