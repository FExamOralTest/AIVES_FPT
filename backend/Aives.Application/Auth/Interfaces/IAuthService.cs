using Aives.Application.Auth.DTOs;

namespace Aives.Application.Auth.Interfaces;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(int userId);
    Task<UserDto> GetCurrentUserAsync(int userId);

    /// <summary>Chỉ ADMIN — lấy toàn bộ danh sách user.</summary>
    Task<IEnumerable<UserDto>> GetAllUsersAsync();

    /// <summary>Chỉ ADMIN — tạo user với role tuỳ chọn (ADMIN, TEACHER, STUDENT).</summary>
    Task<UserDto> CreateUserWithRoleAsync(CreateUserWithRoleRequest request);

    /// <summary>Chỉ ADMIN — cập nhật thông tin user, bao gồm đổi role.</summary>
    Task<UserDto> UpdateUserAsync(int userId, UpdateUserWithRoleRequest request, int? currentUserId = null);
}
