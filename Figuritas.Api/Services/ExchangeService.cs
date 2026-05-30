using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Api.Services;

public class ExchangeService
{
    private readonly UserStickerRepository _inventoryRepo;
    private readonly ExchangeRepository _exchangeRepo;

    public ExchangeService(UserStickerRepository inventoryRepo, ExchangeRepository exchangeRepo)
    {
        _inventoryRepo = inventoryRepo;
        _exchangeRepo = exchangeRepo;
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

        var offeredStickers = _inventoryRepo.GetMultipleById(proposal.OfferedUserStickerIds);
        foreach (var userSticker in offeredStickers)
        {
            userSticker.Quantity -= 1;
            if (userSticker.Quantity <= 0)
                _inventoryRepo.Delete(userSticker.Id);
            else
                _inventoryRepo.Update(userSticker);
        }

        var requestedSticker = _inventoryRepo.GetById(proposal.RequestedUserStickerId);
        if (requestedSticker != null)
        {
            requestedSticker.Quantity -= 1;
            if (requestedSticker.Quantity <= 0)
                _inventoryRepo.Delete(requestedSticker.Id);
            else
                _inventoryRepo.Update(requestedSticker);
        }

        return exchange;
    }
}
