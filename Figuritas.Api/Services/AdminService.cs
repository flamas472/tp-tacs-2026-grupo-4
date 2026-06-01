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

    public AdminService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
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

    public List<AdminUserResponseDTO> GetAllAdmins()
    {
        var admins = _userRepo.GetByRole(UserRole.Admin);
        var superAdmins = _userRepo.GetByRole(UserRole.SuperAdmin);
        return admins.Concat(superAdmins)
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

    private static AdminUserResponseDTO MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role,
        Reputation = user.Reputation
    };
}
