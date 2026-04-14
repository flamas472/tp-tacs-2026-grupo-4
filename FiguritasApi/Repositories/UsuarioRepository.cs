using FiguritasApi.Model;

// Repo para persistencia en memoria. 
public class UsuarioRepository
{
    private readonly List<Usuario> usuarios = new();

    public List<Usuario> GetAll()
    {
        return usuarios;
    }

    public void Add(Usuario usuario)
    {
        usuarios.Add(usuario);
    }

    public Usuario GetByID(int usuarioID)
    {
         return usuarios.FirstOrDefault(u => u.id == usuarioID);
    }
}