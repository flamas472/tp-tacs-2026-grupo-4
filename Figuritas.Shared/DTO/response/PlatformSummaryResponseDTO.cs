namespace Figuritas.Shared.DTO.response;

/// <summary>
/// Top-level analytics summary for the platform.
/// Accessible by Admin and SuperAdmin roles only.
/// </summary>
public class PlatformSummaryResponseDTO
{
    public UserActivitySummaryDTO UserActivity { get; set; } = new();
    public ExchangeMetricsSummaryDTO ExchangeMetrics { get; set; } = new();
    public AuctionVolumeSummaryDTO AuctionVolume { get; set; } = new();
    public NotificationTrafficSummaryDTO NotificationTraffic { get; set; } = new();
}

/// <summary>
/// User-related activity metrics.
/// Active user definition: a user who has at least one published sticker (Active = true, Quantity > 0).
/// </summary>
public class UserActivitySummaryDTO
{
    public int TotalRegisteredUsers { get; set; }
    public int ActiveUsersWithPublishedStickers { get; set; }
    public double AverageCommunityReputation { get; set; }
}

/// <summary>
/// Exchange proposal lifecycle metrics.
/// </summary>
public class ExchangeMetricsSummaryDTO
{
    public int TotalProposalsCreated { get; set; }
    public int AcceptedProposals { get; set; }
    public int RejectedProposals { get; set; }
    public int CancelledProposals { get; set; }
    public double AcceptanceRatePercent { get; set; }
}

/// <summary>
/// Auction lifecycle metrics.
/// </summary>
public class AuctionVolumeSummaryDTO
{
    public int TotalAuctionsCreated { get; set; }
    public int ActiveAuctions { get; set; }
    public int ClosedAuctionsWithWinner { get; set; }
    public int ClosedAuctionsWithoutWinner { get; set; }
    public int CancelledAuctions { get; set; }
}

/// <summary>
/// Notification traffic metrics broken down by type.
/// </summary>
public class NotificationTrafficSummaryDTO
{
    public int TotalNotificationsSent { get; set; }
    public int NewProposalNotifications { get; set; }
    public int AuctionEndingNotifications { get; set; }
    public int MissingStickerAvailableNotifications { get; set; }
}
