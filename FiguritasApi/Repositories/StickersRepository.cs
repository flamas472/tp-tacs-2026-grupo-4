using System.Collections.Concurrent;
using FiguritasApi.Shared.Model;

// Repo for in-memory persistence.
public class StickerRepository
{
    private readonly ConcurrentBag<Sticker> Stickers = new();
    private int nextId = 1;

    public StickerRepository()// TODO BORRAR CUANDO IMPLEMENTEMOS DB
    {
        // =========================================================================
        // TODO BORRAR CUANDO IMPLEMENTEMOS DB
        // =========================================================================
        Add(new Sticker { Number = 10, Player = "Lionel Messi", Team = Team.Boca, NationalTeam = NationalTeam.Argentina, Category = Category.Player });
        Add(new Sticker { Number = 11, Player = "Angel Di Maria", Team = Team.River, NationalTeam = NationalTeam.Brasil, Category = Category.Player });
        Add(new Sticker { Number = 7, Player = "Cristiano Ronaldo", Team = Team.Boca, NationalTeam = NationalTeam.Argentina, Category = Category.Player });
        Add(new Sticker { Number = 10, Player = "Neymar Jr", Team = Team.River, NationalTeam = NationalTeam.Brasil, Category = Category.Player });
        Add(new Sticker { Number = 5, Player = "Alexis Mac Allister", Team = Team.Boca, NationalTeam = NationalTeam.Argentina, Category = Category.Player });
        // =========================================================================
    }// TODO BORRAR CUANDO IMPLEMENTEMOS DB

    public List<Sticker> GetAll()
    {
        return Stickers.ToList();
    }

    public void Add(Sticker sticker)
    {
        sticker.Id = Interlocked.Increment(ref nextId) - 1;
        Stickers.Add(sticker);
    }

    public Sticker? GetById(int id) => Stickers.FirstOrDefault(a => a.Id == id);

    public Sticker? GetByNumber(int number) => Stickers.FirstOrDefault(a => a.Number == number);

    public List<Sticker> GetAllByTeam(Team team) => Stickers.Where(a => a.Team == team).ToList();

    public List<Sticker> GetAllByNationalTeam(NationalTeam nationalTeam) => Stickers.Where(a => a.NationalTeam == nationalTeam).ToList();

    public List<Sticker> GetAllByCategory(Category category) => Stickers.Where(a => a.Category == category).ToList();

    public List<Sticker> GetAllByPlayer(string playerName) => Stickers.Where(a => a.Player == playerName).ToList();

    public bool Update(int id, Sticker updated)
    {
        Sticker existing = GetById(id);

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