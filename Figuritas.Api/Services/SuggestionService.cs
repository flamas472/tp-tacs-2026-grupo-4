using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class SuggestionService(
    IMissingStickerRepository missingStickerRepo,
    IUserStickerRepository userStickerRepo,
    IUserRepository userRepo)
{
    private readonly IMissingStickerRepository _missingStickerRepo = missingStickerRepo;
    private readonly IUserStickerRepository _userStickerRepo = userStickerRepo;
    private readonly IUserRepository _userRepo = userRepo;

    public async Task<List<ExchangeSuggestionResponseDTO>> GetSuggestionsAsync(int callerId, int page, int pageSize)
    {
        // Paso 1: obtener StickerIds faltantes del caller
        var missingIds = await _missingStickerRepo.GetStickerIdsByUserIdAsync(callerId);
        if (!missingIds.Any()) return new List<ExchangeSuggestionResponseDTO>();

        // Paso 2: buscar listings de la comunidad con esos stickers
        var communityListings = _userStickerRepo.GetByStickerIds(missingIds, excludeUserId: callerId);
        if (!communityListings.Any()) return new List<ExchangeSuggestionResponseDTO>();

        // Paso 3: agrupar por usuario candidato
        var candidatesByUser = communityListings.GroupBy(us => us.UserId).ToList();

        // Paso 4: obtener stickers del caller disponibles para intercambio (una sola llamada a la BD)
        var callerStickers = _userStickerRepo.GetByUserId(callerId)
            .Where(us => us.Quantity > 0 && us.Active && us.CanBeDirectlyExchanged)
            .ToList();

        var callerOwned = callerStickers.Select(us => us.Sticker.Id).ToHashSet();

        // Paso 5: batch query de faltantes de todos los candidatos (evita N+1)
        var candidateUserIds = candidatesByUser.Select(g => g.Key).ToList();
        var allCandidateMissing = await _missingStickerRepo.GetByUserIdsAsync(candidateUserIds);

        // Keep full MissingSticker objects ordered by RegisteredAt to preserve priority
        var missingByUser = allCandidateMissing
            .GroupBy(ms => ms.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(ms => ms.RegisteredAt).ToList());

        // Paso 6: construir sugerencias
        // Una sola query batch para obtener todos los usuarios candidatos
        var candidateUsers = _userRepo.GetByIds(candidateUserIds).ToDictionary(u => u.Id, u => u);

        var results = new List<ExchangeSuggestionResponseDTO>();
        foreach (var group in candidatesByUser)
        {
            var candidateUserId = group.Key;
            var candidateMissingOrdered = missingByUser.GetValueOrDefault(candidateUserId, new List<MissingSticker>());
            var candidateMissingIds = candidateMissingOrdered.Select(ms => ms.StickerId).ToHashSet();

            // A perfect match means the candidate needs at least one sticker the caller can offer.
            // Simple matches (A needs from B but B needs nothing from A) are excluded.
            var isPerfectMatch = callerOwned.Any(sId => candidateMissingIds.Contains(sId));
            if (!isPerfectMatch) continue;

            candidateUsers.TryGetValue(candidateUserId, out var candidateUser);

            // All caller stickers the candidate needs, ordered by candidate's RegisteredAt (oldest first).
            var callerMatchedStickers = isPerfectMatch
                ? candidateMissingOrdered
                    .Where(ms => callerOwned.Contains(ms.StickerId))
                    .Select(ms => callerStickers.FirstOrDefault(us => us.Sticker.Id == ms.StickerId))
                    .Where(us => us != null)
                    .Cast<UserSticker>()
                    .Select(us => new StickerPreviewDTO
                    {
                        UserStickerId = us.Id,
                        Number        = us.Sticker.Number,
                        ImageUrl      = us.Sticker.ImageUrl ?? string.Empty,
                        Description   = us.Sticker.Description ?? string.Empty
                    })
                    .ToList()
                : new List<StickerPreviewDTO>();

            foreach (var listing in group)
            {
                results.Add(new ExchangeSuggestionResponseDTO
                {
                    SuggestedUserId       = candidateUserId,
                    SuggestedUsername     = candidateUser?.Username ?? string.Empty,
                    TheirUserStickerId    = listing.Id,
                    StickerId             = listing.Sticker.Id,
                    StickerNumber         = listing.Sticker.Number,
                    StickerNationalTeam   = listing.Sticker.NationalTeam ?? string.Empty,
                    StickerTeam           = listing.Sticker.Team ?? string.Empty,
                    StickerCategory       = listing.Sticker.Category ?? string.Empty,
                    StickerDescription    = listing.Sticker.Description ?? string.Empty,
                    StickerImageUrl       = listing.Sticker.ImageUrl ?? string.Empty,
                    TheirQuantity         = listing.Quantity,
                    IsPerfectMatch        = isPerfectMatch,
                    CallerMatchedStickers = callerMatchedStickers
                });
            }
        }

        // Paso 7: ordenar (perfectos primero) y paginar.
        return results
            .OrderByDescending(r => r.IsPerfectMatch)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }
}
