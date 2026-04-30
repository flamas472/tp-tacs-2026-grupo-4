namespace FiguritasApi.Model;

public class Exchange
{
    public int Id { get; set; }

    public DateTime DateCompleted { get; set; }

    public required List<UserSticker> User1Figuritas { get; set; }

    public required List<UserSticker> User2Figuritas { get; set; }

    public required ExchangeProposal ExchangeProposal { get; set; }
}
