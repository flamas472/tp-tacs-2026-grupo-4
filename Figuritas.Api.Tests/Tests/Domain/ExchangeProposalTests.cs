using Figuritas.Shared.Model;
using Xunit;
using System.Collections.Generic;

namespace Figuritas.Api.Tests.Domain;

public class ExchangeProposalTests
{
    private UserSticker CreateUserSticker(int userId, bool isActive, PublicationType mode)
    {
        return new UserSticker
        {
            Id = 1,
            UserId = userId,
            Sticker = new Sticker { Number = 1, NationalTeam = "Arg", Team = "AFA", Description = "Jugador", Category = "Base" },
            Active = isActive,
            PublicationMode = mode,
            Quantity = 1
        };
    }

    [Fact]
    public void IsValid_SameProponentAndProposed_ShouldReturnFalse_CP7_1()
    {
        // Arrange
        var proposal = new ExchangeProposal
        {
            ProponentID = 1,
            ProposedID = 1, // ID duplicado
            RequestedSticker = CreateUserSticker(1, true, PublicationType.DirectExchange),
            OfferedStickers = new List<UserSticker> { CreateUserSticker(1, true, PublicationType.DirectExchange) }
        };

        // Act & Assert
        Assert.False(proposal.IsValid());
    }

    [Fact]
    public void IsValid_EmptyOfferedStickers_ShouldReturnFalse_CP7_2()
    {
        // Arrange
        var proposal = new ExchangeProposal
        {
            ProponentID = 1,
            ProposedID = 2,
            RequestedSticker = CreateUserSticker(2, true, PublicationType.DirectExchange),
            OfferedStickers = new List<UserSticker>() // Lista vacía
        };

        // Act & Assert
        Assert.False(proposal.IsValid());
    }

    [Fact]
    public void IsValid_OfferedStickerNotActiveOrNotExchangeable_ShouldReturnFalse_CP7_3()
    {
        // Arrange
        var proposal = new ExchangeProposal
        {
            ProponentID = 1,
            ProposedID = 2,
            RequestedSticker = CreateUserSticker(2, true, PublicationType.DirectExchange),
            // Sticker ofrecido está inactivo y es solo para subasta
            OfferedStickers = new List<UserSticker> { CreateUserSticker(1, false, PublicationType.Auction) } 
        };

        // Act & Assert
        Assert.False(proposal.IsValid());
    }

    [Fact]
    public void IsValid_OfferedStickerBelongsToAnotherUser_ShouldReturnFalse_CP7_4()
    {
        // Arrange
        var proposal = new ExchangeProposal
        {
            ProponentID = 1,
            ProposedID = 2,
            RequestedSticker = CreateUserSticker(2, true, PublicationType.DirectExchange),
            // El sticker ofrecido le pertenece al usuario 3, no al proponente (1)
            OfferedStickers = new List<UserSticker> { CreateUserSticker(3, true, PublicationType.DirectExchange) } 
        };

        // Act & Assert
        Assert.False(proposal.IsValid());
    }

    [Fact]
    public void IsValid_RequestedStickerBelongsToAnotherUser_ShouldReturnFalse_CP7_5()
    {
        // Arrange
        var proposal = new ExchangeProposal
        {
            ProponentID = 1,
            ProposedID = 2,
            // Se le pide un sticker al usuario 2, pero el sticker dice ser del usuario 4
            RequestedSticker = CreateUserSticker(4, true, PublicationType.DirectExchange), 
            OfferedStickers = new List<UserSticker> { CreateUserSticker(1, true, PublicationType.DirectExchange) }
        };

        // Act & Assert
        Assert.False(proposal.IsValid());
    }

    [Fact]
    public void IsValid_AllConditionsMet_ShouldReturnTrue_CP7_6()
    {
        // Arrange
        var proposal = new ExchangeProposal
        {
            ProponentID = 1,
            ProposedID = 2,
            RequestedSticker = CreateUserSticker(2, true, PublicationType.DirectExchange), // Le pertenece al ProposedID y está activo
            OfferedStickers = new List<UserSticker> { CreateUserSticker(1, true, PublicationType.Both) } // Le pertenece al Proponent y está activo
        };

        // Act & Assert
        Assert.True(proposal.IsValid());
    }
}