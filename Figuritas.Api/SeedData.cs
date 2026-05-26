using System.Linq;
using Figuritas.Shared.Model;
using Microsoft.Extensions.DependencyInjection;

public static class SeedData
{
    public static void EnsureSeedData(IServiceProvider services)
    {
        var categoryRepo = services.GetRequiredService<CategoryRepository>();
        var teamRepo = services.GetRequiredService<TeamRepository>();
        var nationalTeamRepo = services.GetRequiredService<NationalTeamRepository>();
        var stickerRepo = services.GetRequiredService<StickerRepository>();

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

        if (!stickerRepo.GetAll().Any())
        {
            stickerRepo.Add(new Sticker { Number = 5, Description = "Escudo Argentina", Team = "Boca", NationalTeam = "Argentina", Category = "Escudo", ImageUrl = "https://a.espncdn.com/photo/2023/0417/r1160276_1000x1000_1-1.png" });
            stickerRepo.Add(new Sticker { Number = 11, Description = "Angel Di Maria", Team = "River", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://i.pinimg.com/474x/d4/2c/d4/d42cd4c9fd6cb46a4983233064887fa8.jpg" });
            stickerRepo.Add(new Sticker { Number = 7, Description = "Cristiano Ronaldo", Team = "Boca", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://www.clarin.com/img/2021/09/09/-XSm8HJjP_340x340__1.jpg" });
            stickerRepo.Add(new Sticker { Number = 10, Description = "Neymar Jr", Team = "River", NationalTeam = "Brasil", Category = "Jugador", ImageUrl = "https://i.pinimg.com/474x/96/d5/7a/96d57a7d924b625f9cc357fae88bfb9e.jpg" });
            stickerRepo.Add(new Sticker { Number = 5, Description = "Alexis Mac Allister", Team = "Boca", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQ92U1nqgfzPGlHxBTKEYnf8PI0FceHNEyXBXxtlfKmTLn4XWyHAycC41owGYqqlryLBMnBX1V3ZZhzqK26MiP2G2f8U3n6cy2GKWBP-q8U&s=10" });
            stickerRepo.Add(new Sticker { Number = 10, Description = "Lionel Messi", Team = "Boca", NationalTeam = "Argentina", Category = "Jugador", ImageUrl = "https://i.pinimg.com/736x/8e/28/dc/8e28dca199d2c529e710f2fc7550fc85.jpg" });
        }
    }
}
