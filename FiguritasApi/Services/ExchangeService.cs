using FiguritasApi.Shared.Model;

namespace FiguritasApi.Services;

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
            Id = 0,
            ProponentID = proposal.ProponentID,
            ProposedID = proposal.ProposedID,
            ProponentStickers = proposal.OfferedStickers,
            ProposedSticker = proposal.RequestedSticker,
            DateCompleted = DateTime.Now,
            ExchangeProposalID = proposal.Id
        };

        _exchangeRepo.Add(exchange);

        return exchange;
    }
}