namespace Figuritas.Shared.Model.Subastas;

/// <summary>
/// Represents the lifecycle outcome of an <see cref="AuctionOffer"/>.
/// Every offer starts as <see cref="Pending"/> and transitions to either
/// <see cref="Won"/> (accepted winner), <see cref="Lost"/> (all others when the
/// auction closes), or <see cref="Cancelled"/> (voluntarily withdrawn by the bidder
/// before auction closure).
/// </summary>
public enum AuctionOfferState
{
    Pending,
    Won,
    Lost,
    Cancelled
}
