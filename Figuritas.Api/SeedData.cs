using Figuritas.Api.Repositories;
using Figuritas.Shared.Enums;
using Figuritas.Shared.Model;
using Microsoft.Extensions.DependencyInjection;

public static class SeedData
{
    public static void EnsureSeedData(IServiceProvider services)
    {
        var categoryRepo = services.GetRequiredService<ICategoryRepository>();
        var teamRepo = services.GetRequiredService<ITeamRepository>();
        var nationalTeamRepo = services.GetRequiredService<INationalTeamRepository>();
        var stickerRepo = services.GetRequiredService<IStickerRepository>();
        var userRepo = services.GetRequiredService<IUserRepository>();

        categoryRepo.CreateIfNonExistent(new Category { Description = "Jugador" });
        categoryRepo.CreateIfNonExistent(new Category { Description = "Escudo" });
        categoryRepo.CreateIfNonExistent(new Category { Description = "Estadio" });

        teamRepo.CreateIfNonExistent(new Team { Description = string.Empty });
        teamRepo.CreateIfNonExistent(new Team { Description = "Boca" });
        teamRepo.CreateIfNonExistent(new Team { Description = "River" });
        teamRepo.CreateIfNonExistent(new Team { Description = "Morón" });

        nationalTeamRepo.CreateIfNonExistent(new NationalTeam { Description = "Argentina" });
        nationalTeamRepo.CreateIfNonExistent(new NationalTeam { Description = "Francia" });
        nationalTeamRepo.CreateIfNonExistent(new NationalTeam { Description = "Brasil" });

        // Seed initial SuperAdmin — academic project, password security intentionally relaxed
        const string superAdminUsername = "Ivanabete";
        if (userRepo.GetByUsername(superAdminUsername) == null)
        {
            userRepo.Add(new User
            {
                Username = superAdminUsername,
                HashedPassword = BCrypt.Net.BCrypt.HashPassword("figuritacs"),
                Role = UserRole.SuperAdmin,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!stickerRepo.GetAll().Any())
        {
            // Argentina
            stickerRepo.Add(new Sticker { Number = 1, Description = "Escudo", Team = "-", NationalTeam = "Argentina", Category = "Escudo", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG01.png" });
            stickerRepo.Add(new Sticker { Number = 2, Description = "Emiliano Martinez", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG02.png" });
            stickerRepo.Add(new Sticker { Number = 3, Description = "Lionel Messi", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG03.png" });
            stickerRepo.Add(new Sticker { Number = 4, Description = "Nico González", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG04.png" });
            stickerRepo.Add(new Sticker { Number = 5, Description = "Giuliano Simeone", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG05.png" });
            stickerRepo.Add(new Sticker { Number = 6, Description = "Julián Álvarez", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG06.png" });
            stickerRepo.Add(new Sticker { Number = 7, Description = "Lautaro Martínez", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG07.png" });
            stickerRepo.Add(new Sticker { Number = 8, Description = "Franco Mastantuono", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG08.png" });
            stickerRepo.Add(new Sticker { Number = 9, Description = "Enzo Fernández", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG09.png" });
            stickerRepo.Add(new Sticker { Number = 10, Description = "Alexis Mac Allister", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG10.png" });
            stickerRepo.Add(new Sticker { Number = 11, Description = "Nico Paz", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG11.png" });
            stickerRepo.Add(new Sticker { Number = 12, Description = "Leandro Paredes", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG12.png" });
            stickerRepo.Add(new Sticker { Number = 13, Description = "Rodrigo De Paul", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG13.png" });
            stickerRepo.Add(new Sticker { Number = 14, Description = "Exequiel Palacios", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG14.png" });
            stickerRepo.Add(new Sticker { Number = 15, Description = "Cristian Romero", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG15.png" });
            stickerRepo.Add(new Sticker { Number = 16, Description = "Leonardo Balerdi", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG16.png" });
            stickerRepo.Add(new Sticker { Number = 17, Description = "Nicolás Otamendi", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG17.png" });
            stickerRepo.Add(new Sticker { Number = 18, Description = "Nahuel Molina", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG18.png" });
            stickerRepo.Add(new Sticker { Number = 19, Description = "Nicolás Tagliafico", Team = "-", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG19.png" });
            stickerRepo.Add(new Sticker { Number = 20, Description = "Formación", Team = "-", NationalTeam = "Argentina", Category = "formacion", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Argentina/ARG20.png" });

            // Brasil
            stickerRepo.Add(new Sticker { Number = 21, Description = "Escudo", Team = "-", NationalTeam = "Brasil", Category = "Escudo", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA01.png" });
            stickerRepo.Add(new Sticker { Number = 22, Description = "Alisson Becker", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA02.png" });
            stickerRepo.Add(new Sticker { Number = 23, Description = "Bento", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA03.png" });
            stickerRepo.Add(new Sticker { Number = 24, Description = "Marquinhos", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA04.png" });
            stickerRepo.Add(new Sticker { Number = 25, Description = "Wesley", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA05.png" });
            stickerRepo.Add(new Sticker { Number = 26, Description = "Éder Militão", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA06.png" });
            stickerRepo.Add(new Sticker { Number = 27, Description = "Gabriel Magalhães", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA07.png" });
            stickerRepo.Add(new Sticker { Number = 28, Description = "Danilo", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA08.png" });
            stickerRepo.Add(new Sticker { Number = 29, Description = "Bruno Guimarães", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA09.png" });
            stickerRepo.Add(new Sticker { Number = 30, Description = "Lucas Paquetá", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA10.png" });
            stickerRepo.Add(new Sticker { Number = 31, Description = "Casemiro", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA11.png" });
            stickerRepo.Add(new Sticker { Number = 32, Description = "Luis Henrique", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA12.png" });
            stickerRepo.Add(new Sticker { Number = 33, Description = "Vinícius Júnior", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA13.png" });
            stickerRepo.Add(new Sticker { Number = 34, Description = "Gabriel Martinelli", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA14.png" });
            stickerRepo.Add(new Sticker { Number = 35, Description = "Raphinha", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA15.png" });
            stickerRepo.Add(new Sticker { Number = 36, Description = "Estêvão", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA16.png" });
            stickerRepo.Add(new Sticker { Number = 37, Description = "Matheus Cunha", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA17.png" });
            stickerRepo.Add(new Sticker { Number = 38, Description = "Rodrygo", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA18.png" });
            stickerRepo.Add(new Sticker { Number = 39, Description = "João Pedro", Team = "-", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA19.png" });
            stickerRepo.Add(new Sticker { Number = 40, Description = "Formación", Team = "-", NationalTeam = "Brasil", Category = "formacion", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/Brasil/BRA20.png" });
        }
    }
}
