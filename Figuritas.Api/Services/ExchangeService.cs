using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class ExchangeService
{
    private readonly IUserStickerRepository _inventoryRepo;
    private readonly IExchangeRepository _exchangeRepo;

    public ExchangeService(IUserStickerRepository inventoryRepo, IExchangeRepository exchangeRepo)
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
            ProponentStickers = proposal.OfferedStickers,
            ProposedSticker = proposal.RequestedSticker,
            DateCompleted = DateTime.UtcNow,
            ExchangeProposalID = proposal.Id
        };

        _exchangeRepo.Add(exchange);

        foreach (var userSticker in proposal.OfferedStickers)
        {
            userSticker.Quantity -= 1;
            if (userSticker.Quantity <= 0)
                _inventoryRepo.Delete(userSticker.Id);
            else
                _inventoryRepo.Update(userSticker);
        }

        var requested = proposal.RequestedSticker;
        requested.Quantity -= 1;
        if (requested.Quantity <= 0)
            _inventoryRepo.Delete(requested.Id);
        else
            _inventoryRepo.Update(requested);

        return exchange;
    }
}
