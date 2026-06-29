namespace Figuritas.Api.Exceptions;

/// <summary>
/// Thrown when a version-conditioned ReplaceOne finds no matching document,
/// indicating that another process modified the document between the read and the write.
/// Callers should re-fetch the document and retry the operation.
/// </summary>
public class OptimisticConcurrencyException : Exception
{
    public OptimisticConcurrencyException(string message) : base(message) { }
    public OptimisticConcurrencyException(string message, Exception inner) : base(message, inner) { }
}
