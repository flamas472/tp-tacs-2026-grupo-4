using System.Collections.Concurrent;
using FiguritasApi.Model;

// Repo for in-memory persistence.
public class StickerRepository
{
    private readonly ConcurrentBag<Sticker> Stickers = new();
    private int nextId = 1;

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