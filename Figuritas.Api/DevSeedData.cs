using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Api.Infrastructure.Persistence;

/// <summary>
/// Seeds development-only test data for local testing of inventory and exchange features.
/// Must only be invoked inside an IsDevelopment() guard in Program.cs.
/// Idempotent: guarded by checking for the "flamas" user before inserting.
/// </summary>
public static class DevSeedData
{
    public static void SeedDevData(IServiceProvider services)
    {
        var userRepo = services.GetRequiredService<IUserRepository>();

        if (userRepo.GetByUsername("flamas") != null)
            return;

        var stickerRepo = services.GetRequiredService<IStickerRepository>();
        var userStickerRepo = services.GetRequiredService<IUserStickerRepository>();
        var missingStickerRepo = services.GetRequiredService<IMissingStickerRepository>();
        var exchangeProposalRepo = services.GetRequiredService<IExchangeProposalRepository>();
        var exchangeRepo = services.GetRequiredService<IExchangeRepository>();

        // Resolve catalog stickers by number + nationalTeam to avoid hardcoding IDs.
        // The base seeder inserts stickers in a fixed order so these are stable.
        var stickerIvanabete = stickerRepo.FindOne(s => s.Number == 3 && s.NationalTeam == "argentina")!;  // Lionel Messi
        var stickerFlamasA   = stickerRepo.FindOne(s => s.Number == 7 && s.NationalTeam == "argentina")!;  // Lautaro Martínez
        var stickerFlamasB   = stickerRepo.FindOne(s => s.Number == 10 && s.NationalTeam == "argentina")!; // Alexis Mac Allister
        var stickerFlamasC   = stickerRepo.FindOne(s => s.Number == 13 && s.NationalTeam == "argentina")!; // Rodrigo De Paul
        var stickerFlamasD   = stickerRepo.FindOne(s => s.Number == 17 && s.NationalTeam == "argentina")!; // Nicolás Otamendi
        var stickerMissing1  = stickerRepo.FindOne(s => s.Number == 22 && s.NationalTeam == "brasil")!;    // Alisson Becker
        var stickerMissing2  = stickerRepo.FindOne(s => s.Number == 33 && s.NationalTeam == "brasil")!;    // Vinícius Júnior
        var stickerMissing3  = stickerRepo.FindOne(s => s.Number == 35 && s.NationalTeam == "brasil")!;    // Raphinha

        // --- A. Historical UserSticker for "Ivanabete" ---
        var ivanabete = userRepo.GetByUsername("Ivanabete")
            ?? throw new InvalidOperationException(
                "Dev seed requires the 'Ivanabete' SuperAdmin to exist. " +
                "Ensure SeedData.EnsureSeedData() runs before DevSeedData.SeedDevData().");

        var ivanabeteHistoricSticker = new UserSticker
        {
            Sticker = stickerIvanabete,
            UserId = ivanabete.Id,
            Quantity = 0,
            Active = false,
            CanBeAuctioned = false,
            CanBeDirectlyExchanged = true
        };
        userStickerRepo.Add(ivanabeteHistoricSticker);

        // --- B. New user "flamas" ---
        var flamas = new User
        {
            Username = "flamas",
            HashedPassword = BCrypt.Net.BCrypt.HashPassword("figuritacs"),
            CreatedAt = DateTime.UtcNow
        };
        userRepo.Add(flamas);

        // B.1 — Active sticker, direct exchange only
        var flamasStickerA = new UserSticker
        {
            Sticker = stickerFlamasA,
            UserId = flamas.Id,
            Quantity = 1,
            Active = true,
            CanBeAuctioned = false,
            CanBeDirectlyExchanged = true
        };
        userStickerRepo.Add(flamasStickerA);

        // B.2 — Active sticker, auction only
        var flamasStickerB = new UserSticker
        {
            Sticker = stickerFlamasB,
            UserId = flamas.Id,
            Quantity = 1,
            Active = true,
            CanBeAuctioned = true,
            CanBeDirectlyExchanged = false
        };
        userStickerRepo.Add(flamasStickerB);

        // B.3 — Active sticker, both exchange and auction
        var flamasStickerC = new UserSticker
        {
            Sticker = stickerFlamasC,
            UserId = flamas.Id,
            Quantity = 2,
            Active = true,
            CanBeAuctioned = true,
            CanBeDirectlyExchanged = true
        };
        userStickerRepo.Add(flamasStickerC);

        // B.4 — Historical sticker (delivered in a past exchange)
        var flamasStickerD = new UserSticker
        {
            Sticker = stickerFlamasD,
            UserId = flamas.Id,
            Quantity = 0,
            Active = false,
            CanBeAuctioned = false,
            CanBeDirectlyExchanged = true
        };
        userStickerRepo.Add(flamasStickerD);

        // B. MissingStickers for "flamas"
        missingStickerRepo.AddAsync(new MissingSticker { UserId = flamas.Id, StickerId = stickerMissing1.Id }).GetAwaiter().GetResult();
        missingStickerRepo.AddAsync(new MissingSticker { UserId = flamas.Id, StickerId = stickerMissing2.Id }).GetAwaiter().GetResult();
        missingStickerRepo.AddAsync(new MissingSticker { UserId = flamas.Id, StickerId = stickerMissing3.Id }).GetAwaiter().GetResult();

        // --- C. Completed ExchangeProposal ---
        // Proponent = flamas, Proposed = Ivanabete
        // flamas offered stickerFlamasD (B.4), Ivanabete gave ivanabeteHistoricSticker (A)
        var completedProposal = new ExchangeProposal
        {
            ProponentID = flamas.Id,
            ProposedID = ivanabete.Id,
            RequestedUserStickerId = ivanabeteHistoricSticker.Id,
            OfferedUserStickerIds = [flamasStickerD.Id],
            State = ExchangeProposalState.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };
        exchangeProposalRepo.Add(completedProposal);

        // --- D. Exchange linked to the completed proposal ---
        // Created after the proposal is persisted so that completedProposal.Id is already assigned.
        var completedExchange = new Exchange
        {
            ExchangeProposalID = completedProposal.Id,
            ProponentID = completedProposal.ProponentID,
            ProposedID = completedProposal.ProposedID,
            ProponentUserStickerIds = completedProposal.OfferedUserStickerIds,
            ProposedUserStickerId = completedProposal.RequestedUserStickerId,
            DateCompleted = completedProposal.CreatedAt
        };
        exchangeRepo.Add(completedExchange);
    }
}
