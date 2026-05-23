using Microsoft.AspNetCore.Http;

namespace YuGo.DTOs
{
    public class RegisterRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CompleteRegistrationRequest
    {
        public int UserId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? TravelType { get; set; }
        public string? BudgetPreference { get; set; }
        public string? TravelStyle { get; set; }
        public string? PreferredTransport { get; set; }
        public string? ProfileImageUrl { get; set; }
        public IFormFile? ProfileImage { get; set; }
    }

    public class SocialLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty; 
        public string? IdToken { get; set; } 
    }

    public class AuthResponse
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public bool IsProfileComplete { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? TravelType { get; set; }
        public string? BudgetPreference { get; set; }
        public string? TravelStyle { get; set; }
        public string? PreferredTransport { get; set; }
        public string? ProfileImageUrl { get; set; }
        public List<UserSessionDto> ActiveSessions { get; set; } = new();
    }

    public class UserSessionDto
    {
        public int Id { get; set; }
        public string? IPAddress { get; set; }
        public string? Device { get; set; }
        public string? Location { get; set; }
        public DateTime LoginAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? TravelType { get; set; }
        public string? BudgetPreference { get; set; }
        public string? TravelStyle { get; set; }
        public string? PreferredTransport { get; set; }
    }
 
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
