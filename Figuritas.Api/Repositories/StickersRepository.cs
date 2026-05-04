using System.Collections.Concurrent;
using Figuritas.Shared.Model;

public class StickerRepository
{
    private readonly ConcurrentBag<Sticker> Stickers = [];
    private int nextId = 1;

    public StickerRepository( 
        NationalTeamRepository nationalTeamRepository, 
        TeamRepository teamRepository, 
        CategoryRepository categoryRepository
    )// TODO BORRAR CUANDO IMPLEMENTEMOS DB
    {
        Add(new Sticker { Number = 5, Description = "Escudo Argentina", Team = "boca", NationalTeam = "argentina", Category = "escudo", ImageUrl = "https://a.espncdn.com/photo/2023/0417/r1160276_1000x1000_1-1.png" });
        Add(new Sticker { Number = 11, Description = "Angel Di Maria", Team = "river", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://i.pinimg.com/474x/d4/2c/d4/d42cd4c9fd6cb46a4983233064887fa8.jpg" });
        Add(new Sticker { Number = 7, Description = "Cristiano Ronaldo", Team = "boca", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://www.clarin.com/img/2021/09/09/-XSm8HJjP_340x340__1.jpg" });
        Add(new Sticker { Number = 10, Description = "Neymar Jr", Team = "river", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://i.pinimg.com/474x/96/d5/7a/96d57a7d924b625f9cc357fae88bfb9e.jpg" });
        Add(new Sticker { Number = 5, Description = "Alexis Mac Allister", Team = "boca", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQ92U1nqgfzPGlHxBTKEYnf8PI0FceHNEyXBXxtlfKmTLn4XWyHAycC41owGYqqlryLBMnBX1V3ZZhzqK26MiP2G2f8U3n6cy2GKWBP-q8U&s=10" });
        Add(new Sticker { Number = 10, Description = "Lionel Messi", Team = "boca", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://i.pinimg.com/736x/8e/28/dc/8e28dca199d2c529e710f2fc7550fc85.jpg" });
        // =========================================================================
    }// TODO BORRAR CUANDO IMPLEMENTEMOS DB

    public List<Sticker> GetAll() => Stickers.ToList();
    public IEnumerable<Sticker> Get(Func<Sticker, bool> predicate, int page, int pageSize) => Stickers.Where(predicate).Skip((page - 1) * pageSize).Take(pageSize);
    public Sticker? GetById(int id) => Stickers.FirstOrDefault(s => s.Id == id);

    public Sticker? FindOne(Func<Sticker, bool> predicate) => Stickers.FirstOrDefault(predicate);

    public void Add(Sticker sticker)
    {
        sticker.Id = Interlocked.Increment(ref nextId) - 1;
        Stickers.Add(sticker);
    }

    public bool Exists(Sticker sticker)
    {
        return Stickers.Any(s => 
            s.Equals(sticker)
        );
    }

    public bool Update(int id, Sticker updated)
    {
        Sticker? existing = GetById(id);

        if (existing == null)
        {
           return false; 
        }

        existing.NationalTeam = updated.NationalTeam;
        existing.Category = updated.Category;
        existing.Number = updated.Number;
        existing.Team = updated.Team;
        return true;
    }

}