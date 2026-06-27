using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Api.Repositories;

public interface IExchangeProposalRepository
{
    List<ExchangeProposal> GetAll();
    void Add(ExchangeProposal proposal);
    ExchangeProposal? GetById(int proposalId);
    List<ExchangeProposal> GetAllUserSentProposals(int userId, ExchangeProposalState? state = null, int page = 1, int pageSize = 20);
    List<ExchangeProposal> GetAllUserReceivedProposals(int userId, ExchangeProposalState? state = null, int page = 1, int pageSize = 20);
    void Update(ExchangeProposal proposal);
    ExchangeProposal? AcceptAtomically(int proposalId);

    /// <summary>
    /// Returns true if the given sender submitted any proposal within the last <paramref name="windowSeconds"/> seconds.
    /// Used by the rate-limiting guard to prevent automated burst submissions.
    /// </summary>
    bool HasRecentProposal(int senderUserId, int windowSeconds);

    /// <summary>
    /// Atomically transitions the proposal from <see cref="ExchangeProposalState.Pending"/> to
    /// <paramref name="targetState"/> using a MongoDB FindOneAndUpdate conditioned on State == Pending.
    /// Returns the updated document (ReturnDocument.After) on success.
    /// Returns null if the proposal was not found or was not in Pending state, preventing
    /// double-cancel / double-reject race conditions.
    /// </summary>
    Task<ExchangeProposal?> TryTransitionFromPendingAsync(int proposalId, ExchangeProposalState targetState);

    /// <summary>
    /// Returns true if there is at least one Pending proposal that includes
    /// <paramref name="userStickerId"/> in its <c>OfferedUserStickerIds</c>.
    /// Used as a guard before allowing the owner to modify or delete a sticker
    /// that is currently reserved for a pending exchange proposal.
    /// </summary>
    Task<bool> HasActivePendingProposalForOfferedStickerAsync(int userStickerId);

    /// <summary>
    /// Atomically transitions the proposal from <see cref="ExchangeProposalState.Accepted"/> to
    /// <see cref="ExchangeProposalState.Rejected"/> using a MongoDB FindOneAndUpdate conditioned on
    /// State == Accepted. No-op if the proposal is not found or is not in Accepted state.
    /// Called during rollback when CreateExchange fails after AcceptAtomically succeeded.
    /// </summary>
    Task RevertToRejectedAsync(int proposalId);

    /// <summary>
    /// Returns all proposals currently in <see cref="ExchangeProposalState.Pending"/> state
    /// that request <paramref name="requestedUserStickerId"/>.
    /// Used to identify competing proposals that must be auto-rejected after a successful exchange.
    /// </summary>
    Task<List<ExchangeProposal>> GetAllPendingForRequestedStickerAsync(int requestedUserStickerId);
}
