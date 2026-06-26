namespace Figuritas.Client.Models;

/// <summary>ViewModel para el perfil público de un usuario en la página /perfil/{username}.</summary>
public class UserProfileViewModel
{
    public string Username { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public double AverageReputation { get; set; }
    public int TotalReviews { get; set; }
    public int CompletedExchanges { get; set; }
    public List<UserReviewViewModel> Reviews { get; set; } = new();

    /// <summary>
    /// Cantidad de reseñas por estrella.
    /// Índice 0 = 1 estrella, índice 4 = 5 estrellas.
    /// </summary>
    public int[] StarCounts { get; set; } = new int[5];

    /// <summary>Porcentaje (0-100) de reseñas con la cantidad de estrellas indicada (1–5).</summary>
    public double GetStarPercentage(int stars) =>
        TotalReviews == 0 ? 0 : Math.Round((double)StarCounts[stars - 1] / TotalReviews * 100, 1);
}

/// <summary>ViewModel para una reseña individual de usuario.</summary>
public class UserReviewViewModel
{
    public int Id { get; set; }
    public string ReviewerUsername { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
