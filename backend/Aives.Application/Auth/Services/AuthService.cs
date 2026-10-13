using Aives.Application.Auth.DTOs;
using Aives.Application.Auth.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Application.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IJwtBlacklistService _blacklistService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IJwtBlacklistService blacklistService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _blacklistService = blacklistService;
        _logger = logger;
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.ExistsByUsernameAsync(request.Username))
        {
            _logger.LogWarning("Registration failed: Username {Username} already exists", request.Username);
            throw new InvalidOperationException("Username already exists.");
        }

        if (!string.IsNullOrEmpty(request.Email) && await _userRepository.ExistsByEmailAsync(request.Email))
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
            throw new InvalidOperationException("Email already exists.");
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.STUDENT,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);

        _logger.LogInformation("User registered successfully: {Username} with role {Role}", user.Username, user.Role);

        return MapToUserDto(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var identifier = request.Identifier?.Trim() ?? string.Empty;
        User? user = null;

        if (identifier.Contains('@'))
        {
            user = await _userRepository.GetByEmailAsync(identifier);
        }
        else
        {
            user = await _userRepository.GetByUsernameAsync(identifier);
        }

        if (user is null)
        {
            user = identifier.Contains('@')
                ? await _userRepository.GetByUsernameAsync(identifier)
                : await _userRepository.GetByEmailAsync(identifier);
        }

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: Invalid credentials for identifier {Identifier}", identifier);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        var token = _jwtService.GenerateToken(user);
        var expiresIn = _jwtService.GetExpirationSeconds();

        _logger.LogInformation("User logged in successfully: {Username} with role {Role}", user.Username, user.Role);

        return new AuthResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToUserDto(user)
        };
    }

    public async Task LogoutAsync(int userId, string? token = null)
    {
        if (!string.IsNullOrEmpty(token))
        {
            await _blacklistService.AddToBlacklistAsync(token, DateTime.UtcNow.AddDays(1));
        }
        _logger.LogInformation("User {UserId} logged out", userId);
    }

    public async Task<UserDto> GetCurrentUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        return MapToUserDto(user);
    }

    public async Task<UserDto> CreateUserWithRoleAsync(CreateUserWithRoleRequest request)
    {
        if (await _userRepository.ExistsByUsernameAsync(request.Username))
        {
            _logger.LogWarning("Create user failed: Username {Username} already exists", request.Username);
            throw new InvalidOperationException("Username already exists.");
        }

        if (!string.IsNullOrEmpty(request.Email) && await _userRepository.ExistsByEmailAsync(request.Email))
        {
            _logger.LogWarning("Create user failed: Email {Email} already exists", request.Email);
            throw new InvalidOperationException("Email already exists.");
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);

        _logger.LogInformation("User created by admin: {Username} with role {Role}", user.Username, user.Role);

        return MapToUserDto(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToUserDto);
    }

    public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserWithRoleRequest request, int? currentUserId = null)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var oldRole = user.Role;

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        if (request.Email is not null)
        {
            var normalised = request.Email.Trim().ToLowerInvariant();
            if (await _userRepository.ExistsByEmailAsync(normalised) &&
                user.Email != normalised)
                throw new InvalidOperationException("Email already in use.");
            user.Email = normalised;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _passwordHasher.Hash(request.Password);

        if (request.Role is not null)
        {
            var newRole = request.Role.Value;

            if (user.Role == UserRole.ADMIN && newRole != UserRole.ADMIN)
            {
                var adminCount = await _userRepository.GetAdminCountAsync();
                if (adminCount <= 1)
                {
                    _logger.LogWarning("Failed to demote last admin: User {UserId}", userId);
                    throw new InvalidOperationException("Cannot demote the last admin user.");
                }
            }

            user.Role = newRole;

            _logger.LogInformation("User {UserId} role changed from {OldRole} to {NewRole}",
                userId, oldRole, newRole);
        }

        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("User {UserId} updated successfully", userId);

        return MapToUserDto(user);
    }

    private static UserDto MapToUserDto(User user) => new()
    {
        UserId = user.UserId,
        Username = user.Username,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToString()
    };
}
