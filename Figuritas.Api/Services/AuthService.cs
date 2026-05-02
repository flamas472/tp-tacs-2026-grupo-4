using Figuritas.Shared.Model;
namespace Figuritas.Api.Services;

using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class AuthService
{
    private readonly IConfiguration _config;

    public AuthService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        // 1. Definir la Llave Secreta 
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // 2. Crear los "Claims" (Datos que viajan DENTRO del token)
        // Acá metemos el ID para identificar quién hace las requests después.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), //estándar de la industria para guardar el ID del usuario
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("IsAdmin", user.isAdmin.ToString())
        };

        // 3. Crear el objeto del Token
        var token = new JwtSecurityToken(
            //issuer: _config["Jwt:Issuer"],
            //audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(2), // El token vence en 2 horas
            signingCredentials: credentials);

        // 4. Convertirlo a un String
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int GetUserIdFromToken(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null) 
            throw new Exception("User ID claim not found");
        return int.Parse(userIdClaim);
    }
}