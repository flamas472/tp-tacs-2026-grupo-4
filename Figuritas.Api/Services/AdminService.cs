using BCrypt.Net;
using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Enums;
using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Services;

/// <summary>
/// Handles admin-level user management: creating admins, listing admins,
/// and promoting/demoting roles. Restricted to SuperAdmin callers via controller.
/// </summary>
public class AdminService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;

    public AdminService(IUserRepository userRepo, IRefreshTokenRepository refreshTokenRepo)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
    }

    public User CreateAdmin(CreateAdminRequestDTO dto)
    {
        var username = dto.Username;
        var password = dto.Password;

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required");
        if (_userRepo.GetByUsername(username) != null)
            throw new ArgumentException("Username already exists");
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required");

        var admin = new User
        {
            Username = username,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Admin
        };

        try
        {
            _userRepo.Add(admin);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new ArgumentException("Username already exists");
        }

        return admin;
    }

    public List<AdminUserResponseDTO> GetAllAdmins(int page = 1, int pageSize = 20)
    {
        var adminRoles = new List<UserRole> { UserRole.Admin, UserRole.SuperAdmin };
        return _userRepo.GetByRoles(adminRoles, page, pageSize)
            .Select(MapToDto)
            .ToList();
    }

    public AdminUserResponseDTO PatchAdminRole(int userId, PatchAdminRoleRequestDTO dto)
    {
        if (dto.Role == UserRole.User)
            throw new ArgumentException("Cannot demote an admin to regular User via this endpoint. Remove them from the system instead.");

        var user = _userRepo.GetById(userId)
            ?? throw new KeyNotFoundException("User not found");

        if (user.Role == UserRole.User)
            throw new ArgumentException("Target user is not an admin");

        user.Role = dto.Role;
        _userRepo.Update(user);
        return MapToDto(user);
    }

    /// <summary>
    /// Revokes admin privileges from a user, setting their role back to regular User.
    /// Cannot be used to revoke a SuperAdmin's role.
    /// </summary>
    public void RevokeAdmin(int userId, int callerSuperAdminId)
    {
        if (userId == callerSuperAdminId)
            throw new ArgumentException("Cannot revoke your own admin privileges.");

        var user = _userRepo.GetById(userId)
            ?? throw new KeyNotFoundException("User not found");

        if (user.Role == UserRole.SuperAdmin)
            throw new ArgumentException("Cannot revoke a SuperAdmin's privileges via this endpoint. Demote them to Admin first.");

        if (user.Role == UserRole.User)
            throw new ArgumentException("User does not have admin privileges.");

        user.Role = UserRole.User;
        _userRepo.Update(user);
    }

    public List<UserResponseDTO> GetAllUsers(int page, int pageSize)
    {
        return _userRepo.GetAllPaginated(page, pageSize)
            .Select(u => new UserResponseDTO
            {
                Id = u.Id,
                Username = u.Username,
                Reputation = u.Reputation,
                Banned = u.Banned,
                Role = u.Role
            })
            .ToList();
    }

    public async Task BanUserAsync(int userId, int callerAdminId)
    {
        if (userId == callerAdminId)
            throw new ArgumentException("Administrators cannot ban themselves.");

        var user = _userRepo.GetById(userId)
            ?? throw new KeyNotFoundException("User not found");

        if (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin)
            throw new ArgumentException("Cannot ban an administrator account.");

        user.Banned = true;

        // Set the token validity boundary to the current instant.
        // The OnTokenValidated middleware rejects any JWT whose "iat" claim
        // predates this value, immediately terminating active sessions.
        user.TokenValidFrom = DateTime.UtcNow;

        _userRepo.Update(user);

        // Invalidate all active refresh tokens so a banned user cannot silently
        // keep re-authenticating using a previously issued refresh token.
        await _refreshTokenRepo.RevokeAllForUserAsync(userId.ToString());
    }

    public void UnbanUser(int userId)
    {
        var user = _userRepo.GetById(userId)
            ?? throw new KeyNotFoundException("User not found");

        user.Banned = false;
        _userRepo.Update(user);
    }

    private static AdminUserResponseDTO MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role,
        Reputation = user.Reputation
    };
}
