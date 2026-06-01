using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.response;

namespace Figuritas.Api.Services;

/// <summary>
/// Orchestrates analytics queries and constructs the platform summary DTO.
/// Calculates derived metrics such as AcceptanceRatePercent.
/// Controllers remain thin — no computation logic belongs there.
/// </summary>
public class AdminAnalyticsService
{
    private readonly IAnalyticsRepository _analyticsRepo;

    public AdminAnalyticsService(IAnalyticsRepository analyticsRepo)
    {
        _analyticsRepo = analyticsRepo;
    }

    public async Task<PlatformSummaryResponseDTO> GetPlatformSummaryAsync()
    {
        var userActivity = await BuildUserActivityAsync();
        var exchangeMetrics = await BuildExchangeMetricsAsync();
        var auctionVolume = await BuildAuctionVolumeAsync();
        var notificationTraffic = await BuildNotificationTrafficAsync();

        return new PlatformSummaryResponseDTO
        {
            UserActivity = userActivity,
            ExchangeMetrics = exchangeMetrics,
            AuctionVolume = auctionVolume,
            NotificationTraffic = notificationTraffic
        };
    }

    private async Task<UserActivitySummaryDTO> BuildUserActivityAsync()
    {
        var total = await _analyticsRepo.CountTotalUsersAsync();
        var active = await _analyticsRepo.CountActiveUsersWithPublishedStickersAsync();
        var avgRep = await _analyticsRepo.GetAverageCommunityReputationAsync();

        return new UserActivitySummaryDTO
        {
            TotalRegisteredUsers = total,
            ActiveUsersWithPublishedStickers = active,
            AverageCommunityReputation = avgRep
        };
    }

    private async Task<ExchangeMetricsSummaryDTO> BuildExchangeMetricsAsync()
    {
        var total = await _analyticsRepo.CountTotalProposalsAsync();
        var accepted = await _analyticsRepo.CountProposalsByStateAsync("Accepted");
        var rejected = await _analyticsRepo.CountProposalsByStateAsync("Rejected");
        var cancelled = await _analyticsRepo.CountProposalsByStateAsync("Cancelled");

        double acceptanceRate = total > 0
            ? Math.Round((double)accepted / total * 100, 2)
            : 0.0;

        return new ExchangeMetricsSummaryDTO
        {
            TotalProposalsCreated = total,
            AcceptedProposals = accepted,
            RejectedProposals = rejected,
            CancelledProposals = cancelled,
            AcceptanceRatePercent = acceptanceRate
        };
    }

    private async Task<AuctionVolumeSummaryDTO> BuildAuctionVolumeAsync()
    {
        var total = await _analyticsRepo.CountTotalAuctionsAsync();
        var active = await _analyticsRepo.CountAuctionsByStatusAsync("Active");
        var cancelled = await _analyticsRepo.CountAuctionsByStatusAsync("Cancelled");
        var withWinner = await _analyticsRepo.CountClosedAuctionsWithWinnerAsync();
        var withoutWinner = await _analyticsRepo.CountClosedAuctionsWithoutWinnerAsync();

        return new AuctionVolumeSummaryDTO
        {
            TotalAuctionsCreated = total,
            ActiveAuctions = active,
            ClosedAuctionsWithWinner = withWinner,
            ClosedAuctionsWithoutWinner = withoutWinner,
            CancelledAuctions = cancelled
        };
    }

    private async Task<NotificationTrafficSummaryDTO> BuildNotificationTrafficAsync()
    {
        var total = await _analyticsRepo.CountTotalNotificationsAsync();
        var newProposal = await _analyticsRepo.CountNotificationsByTypeAsync("NewProposal");
        var auctionEnding = await _analyticsRepo.CountNotificationsByTypeAsync("AuctionEnding");
        var missingStickerAvailable = await _analyticsRepo.CountNotificationsByTypeAsync("MissingStickerAvailable");

        return new NotificationTrafficSummaryDTO
        {
            TotalNotificationsSent = total,
            NewProposalNotifications = newProposal,
            AuctionEndingNotifications = auctionEnding,
            MissingStickerAvailableNotifications = missingStickerAvailable
        };
    }
}
