using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;
using MongoDB.Driver;

namespace Figuritas.Api.Services;

public class ExchangeService
{
    private readonly IUserStickerRepository _inventoryRepo;
    private readonly IExchangeRepository _exchangeRepo;
    private readonly IMissingStickerRepository _missingStickerRepo;
    private readonly IMongoClient _mongoClient;

    public ExchangeService(
        IUserStickerRepository inventoryRepo,
        IExchangeRepository exchangeRepo,
        IMissingStickerRepository missingStickerRepo,
        MongoDbContext mongoDbContext)
    {
        _inventoryRepo = inventoryRepo;
        _exchangeRepo = exchangeRepo;
        _missingStickerRepo = missingStickerRepo;
        _mongoClient = mongoDbContext.GetClient();
    }

    public Exchange? GetByProposalId(int proposalId)
    {
        return _exchangeRepo.GetByProposalId(proposalId);
    }

    public Exchange CreateExchange(ExchangeProposal proposal)
    {
        // Attempt a multi-document ACID transaction when the server supports it (replica set / sharded cluster).
        // If the topology is a standalone (e.g., local dev / test), fall back to the non-transactional path
        // so that the service remains functional in those environments.
        using var session = _mongoClient.StartSession();

        bool transactionSupported = false;
        try
        {
            session.StartTransaction();
            transactionSupported = true;
        }
        catch (NotSupportedException)
        {
            // Standalone MongoDB: transactions are not supported.
        }
        catch (MongoDB.Driver.MongoCommandException)
        {
            // Server does not have the replication setup required for transactions.
        }

        try
        {
            var exchange = ExecuteExchangeOperations(proposal, transactionSupported ? session : null);

            if (transactionSupported)
                session.CommitTransaction();

            return exchange;
        }
        catch
        {
            if (transactionSupported)
                session.AbortTransaction();
            throw;
        }
    }

    private Exchange ExecuteExchangeOperations(ExchangeProposal proposal, IClientSessionHandle? session)
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

        if (session != null)
            _exchangeRepo.Add(exchange, session);
        else
            _exchangeRepo.Add(exchange);

        // The offered stickers were already reserved (decremented) at proposal creation time.
        // Use inclusive query because stickers may be inactive (Qty=0, Active=false) due to reservation.
        // Now transfer ownership: give offered stickers to the receiver (ProposedID).
        var offeredStickers = session != null
            ? _inventoryRepo.GetMultipleByIdIncludingInactive(proposal.OfferedUserStickerIds, session)
            : _inventoryRepo.GetMultipleByIdIncludingInactive(proposal.OfferedUserStickerIds);

        foreach (var userSticker in offeredStickers)
        {
            userSticker.Quantity++;
            userSticker.Active = true;
            if (session != null)
                _inventoryRepo.Update(userSticker, session);
            else
                _inventoryRepo.Update(userSticker);
        }

        // Transfer requested sticker: decrement from receiver, add to proponent
        var requestedSticker = session != null
            ? _inventoryRepo.GetByIdIncludingInactive(proposal.RequestedUserStickerId, session)
            : _inventoryRepo.GetByIdIncludingInactive(proposal.RequestedUserStickerId);

        if (requestedSticker != null)
        {
            requestedSticker.Quantity--;
            if (requestedSticker.Quantity <= 0)
            {
                requestedSticker.Active = false;
            }
            if (session != null)
                _inventoryRepo.Update(requestedSticker, session);
            else
                _inventoryRepo.Update(requestedSticker);

            // Increment proponent's inventory for the sticker type they requested
            var proponentInventory = session != null
                ? _inventoryRepo.GetByUserId(proposal.ProponentID, session)
                : _inventoryRepo.GetByUserId(proposal.ProponentID);

            var proponentExistingSticker = proponentInventory
                .FirstOrDefault(s => s.Sticker.Id == requestedSticker.Sticker.Id);

            if (proponentExistingSticker != null)
            {
                proponentExistingSticker.Quantity++;
                proponentExistingSticker.Active = true;
                if (session != null)
                    _inventoryRepo.Update(proponentExistingSticker, session);
                else
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
                if (session != null)
                    _inventoryRepo.Add(newProponentSticker, session);
                else
                    _inventoryRepo.Add(newProponentSticker);
            }
        }

        // Automation: clean up MissingStickers for both participants
        // Proponent received the requested sticker — remove from their missing list
        var requestedCatalogStickerId = requestedSticker?.Sticker.Id ?? 0;
        if (requestedCatalogStickerId > 0)
        {
            if (session != null)
                _missingStickerRepo.DeleteAsync(proposal.ProponentID, requestedCatalogStickerId, session).GetAwaiter().GetResult();
            else
                _missingStickerRepo.DeleteAsync(proposal.ProponentID, requestedCatalogStickerId).GetAwaiter().GetResult();
        }

        // Receiver received the offered stickers — remove matching entries from their missing list
        foreach (var offeredSticker in offeredStickers)
        {
            if (session != null)
                _missingStickerRepo.DeleteAsync(proposal.ProposedID, offeredSticker.Sticker.Id, session).GetAwaiter().GetResult();
            else
                _missingStickerRepo.DeleteAsync(proposal.ProposedID, offeredSticker.Sticker.Id).GetAwaiter().GetResult();
        }

        return exchange;
    }
}
