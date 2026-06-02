using Figuritas.Shared.Model;

namespace Figuritas.Client.Models;

public record UserStickerVm(
    int UserStickerId,
    Sticker Sticker,
    int Quantity,
    bool CanBeDirectlyExchanged,
    bool CanBeAuctioned
);
