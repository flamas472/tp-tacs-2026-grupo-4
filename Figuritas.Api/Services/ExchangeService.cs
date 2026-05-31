using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Api.Services;

public class ExchangeService
{
    private readonly IUserStickerRepository _inventoryRepo;
    private readonly IExchangeRepository _exchangeRepo;
    private readonly IMissingStickerRepository _missingStickerRepo;

    public ExchangeService(
        IUserStickerRepository inventoryRepo,
        IExchangeRepository exchangeRepo,
        IMissingStickerRepository missingStickerRepo)
    {
        _inventoryRepo = inventoryRepo;
        _exchangeRepo = exchangeRepo;
        _missingStickerRepo = missingStickerRepo;
    }

    public Exchange CreateExchange(ExchangeProposal proposal)
    {
        var exchange = new Exchange
        {
            ProponentID = proposal.ProponentID,
            ProposedID = proposal.ProposedID,
            ProponentUserStickerIds = proposal.OfferedUserStickerIds,
            ProposedUserStickerId = proposal.RequestedUserStickerId,
            DateCompleted = DateTime.UtcNow,
            ExchangeProposalID = proposal.Id
        };

        _exchangeRepo.Add(exchange);

        // The offered stickers were already reserved (decremented) at proposal creation time.
        // Use inclusive query because stickers may be inactive (Qty=0, Active=false) due to reservation.
        // Now transfer ownership: give offered stickers to the receiver (ProposedID).
        var offeredStickers = _inventoryRepo.GetMultipleByIdIncludingInactive(proposal.OfferedUserStickerIds);
        foreach (var userSticker in offeredStickers)
        {
            userSticker.Quantity++;
            userSticker.Active = true;
            _inventoryRepo.Update(userSticker);
        }

        // Transfer requested sticker: decrement from receiver, add to proponent
        var requestedSticker = _inventoryRepo.GetById(proposal.RequestedUserStickerId);
        if (requestedSticker != null)
        {
            requestedSticker.Quantity--;
            if (requestedSticker.Quantity <= 0)
            {
                requestedSticker.Active = false;
            }
            _inventoryRepo.Update(requestedSticker);

            // Increment proponent's inventory for the sticker type they requested
            var proponentExistingSticker = _inventoryRepo.GetByUserId(proposal.ProponentID)
                .FirstOrDefault(s => s.Sticker.Id == requestedSticker.Sticker.Id);
            if (proponentExistingSticker != null)
            {
                proponentExistingSticker.Quantity++;
                proponentExistingSticker.Active = true;
                _inventoryRepo.Update(proponentExistingSticker);
            }
            else
            {
                var newProponentSticker = new UserSticker
                {
                    Sticker = requestedSticker.Sticker,
                    UserId = proposal.ProponentID,
                    Quantity = 1,
                    Active = true,
                    CanBeDirectlyExchanged = false,
                    CanBeAuctioned = false
                };
                _inventoryRepo.Add(newProponentSticker);
            }
        }

        // Automation: clean up MissingStickers for both participants
        // Proponent received the requested sticker — remove from their missing list
        var requestedCatalogStickerId = requestedSticker?.Sticker.Id ?? 0;
        if (requestedCatalogStickerId > 0)
        {
            _missingStickerRepo.DeleteAsync(proposal.ProponentID, requestedCatalogStickerId).GetAwaiter().GetResult();
        }

        // Receiver received the offered stickers — remove matching entries from their missing list
        foreach (var offeredSticker in offeredStickers)
        {
            _missingStickerRepo.DeleteAsync(proposal.ProposedID, offeredSticker.Sticker.Id).GetAwaiter().GetResult();
        }

        return exchange;
    }
}
