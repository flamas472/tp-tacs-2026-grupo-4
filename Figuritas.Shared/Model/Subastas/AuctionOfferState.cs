namespace Figuritas.Shared.Model.Subastas;

/// <summary>
/// Represents the lifecycle outcome of an <see cref="AuctionOffer"/>.
/// Every offer starts as <see cref="Pending"/> and transitions to either
/// <see cref="Won"/> (accepted winner) or <see cref="Lost"/> (all others) when the
/// auction closes, whether manually or by the expiration worker.
/// </summary>
public enum AuctionOfferState
{
    Pending,
    Won,
    Lost
}
