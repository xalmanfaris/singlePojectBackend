using YuGo.DTOs;

namespace YuGo.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse?> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null);
        Task<AuthResponse> SocialLoginAsync(SocialLoginRequest request, string? ipAddress = null, string? userAgent = null);
        Task<bool> CompleteRegistrationAsync(CompleteRegistrationRequest request);
        Task<bool> UserExistsAsync(string email);
        Task<UserDto?> GetUserByIdAsync(int userId);
        Task<UserDto?> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<string?> UpdateProfileImageAsync(int userId, IFormFile image);
        Task LogoutAsync(int userId, string? refreshToken = null);
        Task<bool> TerminateSessionAsync(int sessionId, int userId);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<bool> DeleteAccountAsync(int userId);
    }
}
