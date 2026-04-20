using FiguritasApi.Model;

namespace FiguritasApi.Repositories;

public class UsuarioRepository
{
    private readonly List<Usuario> usuarios = [];

    public List<Usuario> GetAll()
    {
        return usuarios;
    }

    public void Add(Usuario usuario)
    {
        usuarios.Add(usuario);
    }

    public Usuario? GetByID(int usuarioID)
    {
         return usuarios.FirstOrDefault(u => u.ID == usuarioID);
    }
}