using BCrypt.Net;
using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Enums;
using Figuritas.Shared.Model;

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
        if (_userRepo.GetAll().Any(u => u.Username == username))
            throw new ArgumentException("Username already exists");
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required");

        var admin = new User
        {
            Username = username,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Admin
        };

        _userRepo.Add(admin);
        return admin;
    }

    public List<UserResponseDTO> GetAllAdmins()
    {
        return _userRepo.GetAll()
            .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin)
            .Select(MapToDto)
            .ToList();
    }

    public UserResponseDTO PatchAdminRole(int userId, PatchAdminRoleRequestDTO dto)
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

    private static UserResponseDTO MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role,
        Reputation = user.Reputation
    };
}
