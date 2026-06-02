namespace Figuritas.Shared.DTO.request;

public class GetMyAuctionsDTO : PagedQueryDTO
{
    /// <summary>
    /// Optional filter by auction status. Accepted values: "Active", "Closed", "Cancelled".
    /// When null or empty, all statuses are returned.
    /// </summary>
    public string? Status { get; set; }
}
