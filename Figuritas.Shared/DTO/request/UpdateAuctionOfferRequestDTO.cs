namespace Figuritas.Shared.DTO.request;

/// <summary>
/// Request body for <c>PATCH /api/auctions/{auctionId}/offers/{offerId}</c>.
/// Accumulative modification: only adds new sticker IDs to an existing offer.
/// Existing committed stickers are never removed or replaced.
/// </summary>
public class UpdateAuctionOfferRequestDTO
{
    /// <summary>
    /// The user-sticker IDs to append to the offer's existing committed list.
    /// Must not be empty, must not contain duplicates within itself, and must not
    /// overlap with sticker IDs already committed to the offer.
    /// </summary>
    public List<int> AdditionalStickerIds { get; set; } = new();
}
