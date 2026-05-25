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
        // ARGENTINA
        Add(new Sticker { Number = 1, Description = "Escudo", Team = "???", NationalTeam = "argentina", Category = "escudo", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG01.png" });
        Add(new Sticker { Number = 2, Description = "Emiliano Martinez", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG02.png" });
        Add(new Sticker { Number = 3, Description = "Lionel Messi", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG03.png" });
        Add(new Sticker { Number = 4, Description = "Nico González", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG04.png" });
        Add(new Sticker { Number = 5, Description = "Giuliano Simeone", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG05.png" });
        Add(new Sticker { Number = 6, Description = "Julián Álvarez", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG06.png" });
        Add(new Sticker { Number = 7, Description = "Lautaro Martínez", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG07.png" });
        Add(new Sticker { Number = 8, Description = "Franco Mastantuono", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG08.png" });
        Add(new Sticker { Number = 9, Description = "Enzo Fernández", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG09.png" });
        Add(new Sticker { Number = 10, Description = "Alexis Mac Allister", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG10.png" });
        Add(new Sticker { Number = 11, Description = "Nico Paz", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG11.png" });
        Add(new Sticker { Number = 12, Description = "Leandro Paredes", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG12.png" });
        Add(new Sticker { Number = 13, Description = "Rodrigo De Paul", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG13.png" });
        Add(new Sticker { Number = 14, Description = "Exequiel Palacios", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG14.png" });
        Add(new Sticker { Number = 15, Description = "Cristian Romero", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG15.png" });
        Add(new Sticker { Number = 16, Description = "Leonardo Balerdi", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG16.png" });
        Add(new Sticker { Number = 17, Description = "Nicolás Otamendi", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG17.png" });
        Add(new Sticker { Number = 18, Description = "Nahuel Molina", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG18.png" });
        Add(new Sticker { Number = 19, Description = "Nicolás Tagliafico", Team = "???", NationalTeam = "argentina", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG19.png" });
        Add(new Sticker { Number = 20, Description = "Formación", Team = "???", NationalTeam = "argentina", Category = "formacion", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/argentina/ARG20.png" });
        // BRASIL
        Add(new Sticker { Number = 21, Description = "Escudo", Team = "???", NationalTeam = "brasil", Category = "escudo", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA01.png" });
        Add(new Sticker { Number = 22, Description = "Alisson Becker", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA02.png" });
        Add(new Sticker { Number = 23, Description = "Bento", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA03.png" });
        Add(new Sticker { Number = 24, Description = "Marquinhos", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA04.png" });
        Add(new Sticker { Number = 25, Description = "Wesley", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA05.png" });
        Add(new Sticker { Number = 26, Description = "Éder Militão", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA06.png" });
        Add(new Sticker { Number = 27, Description = "Gabriel Magalhães", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA07.png" });
        Add(new Sticker { Number = 28, Description = "Danilo", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA08.png" });
        Add(new Sticker { Number = 29, Description = "Bruno Guimarães", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA09.png" });
        Add(new Sticker { Number = 30, Description = "Lucas Paquetá", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA10.png" });
        Add(new Sticker { Number = 31, Description = "Casemiro", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA11.png" });
        Add(new Sticker { Number = 32, Description = "Luis Henrique", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA12.png" });
        Add(new Sticker { Number = 33, Description = "Vinícius Júnior", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA13.png" });
        Add(new Sticker { Number = 34, Description = "Gabriel Martinelli", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA14.png" });
        Add(new Sticker { Number = 35, Description = "Raphinha", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA15.png" });
        Add(new Sticker { Number = 36, Description = "Estêvão", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA16.png" });
        Add(new Sticker { Number = 37, Description = "Matheus Cunha", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA17.png" });
        Add(new Sticker { Number = 38, Description = "Rodrygo", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA18.png" });
        Add(new Sticker { Number = 39, Description = "João Pedro", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA19.png" });
        Add(new Sticker { Number = 40, Description = "Formación", Team = "???", NationalTeam = "brasil", Category = "formacion", ImageUrl = "https://pub-18a1adb3ec5640e99ffd1ab4042bb2e4.r2.dev/brasil/BRA20.png" });
        // TEMPLATE
        // Add(new Sticker { Number = 5, Description = "Escudo", Team = "???", NationalTeam = "brasil", Category = "escudo", ImageUrl = "ENLACE" });
        // Add(new Sticker { Number = 5, Description = "NOMBRE", Team = "???", NationalTeam = "brasil", Category = "jugador", ImageUrl = "ENLACE" });
        // Add(new Sticker { Number = 5, Description = "Formación", Team = "???", NationalTeam = "brasil", Category = "formacion", ImageUrl = "ENLACE" });
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