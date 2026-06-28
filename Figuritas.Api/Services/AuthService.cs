using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Figuritas.Api.Services;

public class AuthService
{
    private readonly IConfiguration _config;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IUserRepository _userRepo;

    public AuthService(
        IConfiguration config,
        IRefreshTokenRepository refreshTokenRepo,
        IUserRepository userRepo)
    {
        _config = config;
        _refreshTokenRepo = refreshTokenRepo;
        _userRepo = userRepo;
    }

    public string GenerateToken(User user)
    {
        var jwtKey = _config["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT secret key is not configured. " +
                "Set the 'Jwt:Key' value via the 'Jwt__Key' environment variable before starting the application.");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<LoginResponseDTO> LoginAsync(User user)
    {
        var accessToken = GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id.ToString());

        return new LoginResponseDTO
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        };
    }

    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var tokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = tokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepo.CreateAsync(refreshToken);
        return tokenValue;
    }

    public async Task<LoginResponseDTO?> RefreshTokensAsync(string refreshToken)
    {
        var stored = await _refreshTokenRepo.GetByTokenAsync(refreshToken);
        if (stored == null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
            return null;

        var user = _userRepo.GetById(int.Parse(stored.UserId));
        if (user == null || user.Banned)
            return null;

        // Rotate: revoke old token and issue a new pair
        await _refreshTokenRepo.RevokeAsync(refreshToken);

        var accessToken = GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        var newRefreshToken = await GenerateRefreshTokenAsync(stored.UserId);

        return new LoginResponseDTO
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, string? requestingUserId)
    {
        var stored = await _refreshTokenRepo.GetByTokenAsync(refreshToken);
        if (stored == null || stored.UserId != requestingUserId)
            return;

        await _refreshTokenRepo.RevokeAsync(refreshToken);
    }

    public int GetUserIdFromToken(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null)
            throw new Exception("User ID claim not found");
        return int.Parse(userIdClaim);
    }
}
