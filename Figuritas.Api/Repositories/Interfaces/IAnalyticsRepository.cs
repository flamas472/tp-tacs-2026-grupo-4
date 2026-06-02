namespace Figuritas.Api.Repositories;

/// <summary>
/// Repository exclusively responsible for read-only aggregated queries
/// used by the analytics/admin layer. No CRUD operations belong here.
/// </summary>
public interface IAnalyticsRepository
{
    // User metrics
    Task<int> CountTotalUsersAsync();
    Task<int> CountActiveUsersWithPublishedStickersAsync();
    Task<double> GetAverageCommunityReputationAsync();

    // Exchange proposal metrics
    Task<int> CountProposalsByStateAsync(string state);
    Task<int> CountTotalProposalsAsync();

    // Auction metrics
    Task<int> CountAuctionsByStatusAsync(string status);
    Task<int> CountTotalAuctionsAsync();
    Task<int> CountClosedAuctionsWithWinnerAsync();
    Task<int> CountClosedAuctionsWithoutWinnerAsync();

    // Notification metrics
    Task<int> CountNotificationsByTypeAsync(string type);
    Task<int> CountTotalNotificationsAsync();
}
