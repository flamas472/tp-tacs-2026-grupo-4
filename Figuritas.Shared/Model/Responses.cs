namespace Figuritas.Shared.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }

        private ApiResponse() { }// Constructor privado para forzar el uso de Fail y Ok como métodos de fábrica

        public static ApiResponse<T> Fail(string message) => new() { Success = false, ErrorMessage = message };
        public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    }
}