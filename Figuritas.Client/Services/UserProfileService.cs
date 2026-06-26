using Figuritas.Client.Models;
using Figuritas.Client.Requests;

namespace Figuritas.Client.Services;

/// <summary>
/// Servicio que obtiene el perfil público de un usuario desde el backend.
/// Consume GET /api/users?username={username}, GET /api/users/{id}/ratings
/// y GET /api/users/{id}/completed-exchanges.
/// </summary>
public class UserProfileService
{
    private readonly UserHttpClient _userHttp;

    public UserProfileService(UserHttpClient userHttp)
    {
        _userHttp = userHttp;
    }

    /// <summary>
    /// Devuelve el perfil de un usuario dado su nombre de usuario.
    /// Retorna null si el usuario no existe o hay error de red.
    /// </summary>
    public async Task<UserProfileViewModel?> GetProfileAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        // 1. Obtener datos básicos del usuario
        var userResult = await _userHttp.GetUserByUsernameAsync(username);
        if (!userResult.Success || userResult.Data is null)
            return null;

        var user = userResult.Data;

        // 2. Obtener reseñas e intercambios completados en paralelo
        var ratingsTask = _userHttp.GetUserRatingsAsync(user.Id, page: 1, pageSize: 100);
        var completedExchangesTask = _userHttp.GetCompletedExchangesAsync(user.Id);
        await Task.WhenAll(ratingsTask, completedExchangesTask);

        var ratingsResult = ratingsTask.Result;
        var ratings = ratingsResult.Success && ratingsResult.Data is not null
            ? ratingsResult.Data
            : new List<Figuritas.Shared.DTO.response.RatingResponseDTO>();

        var completedExchanges = completedExchangesTask.Result.Success && completedExchangesTask.Result.Data is not null
            ? completedExchangesTask.Result.Data.CompletedExchanges
            : 0L;

        // 3. Mapear reseñas al ViewModel
        var reviews = ratings
            .Select(r => new UserReviewViewModel
            {
                Id               = r.Id,
                ReviewerUsername = r.EvaluatorUsername,
                Stars            = r.Stars,
                Comment          = r.Comment,
                CreatedAt        = r.CreatedAt,
            })
            .ToList();

        // 4. Calcular distribución de estrellas
        var starCounts = new int[5];
        foreach (var r in reviews)
            if (r.Stars >= 1 && r.Stars <= 5)
                starCounts[r.Stars - 1]++;

        return new UserProfileViewModel
        {
            Username           = user.Username,
            RegisteredAt       = user.CreatedAt,
            // Usar la reputación calculada por el backend (promedio ponderado persistido)
            AverageReputation  = user.Reputation,
            TotalReviews       = reviews.Count,
            CompletedExchanges = (int)completedExchanges,
            Reviews            = reviews,
            StarCounts         = starCounts,
        };
    }
}
