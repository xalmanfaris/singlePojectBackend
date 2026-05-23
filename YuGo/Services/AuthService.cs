using Dapper;
using YuGo.Data;
using YuGo.DTOs;
using YuGo.Interfaces;
using YuGo.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using System.Net.Http.Json;

namespace YuGo.Services
{
    public class AuthService : IAuthService
    {
        private readonly DbConnectionFactory _dbConnectionFactory;
        private readonly IConfiguration _configuration;
        private readonly IPhotoService _photoService;

        public AuthService(DbConnectionFactory dbConnectionFactory, IConfiguration configuration, IPhotoService photoService)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _configuration = configuration;
            _photoService = photoService;
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email", new { Email = email });
            return user != null;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var sql = @"
                INSERT INTO Users (FullName, Email, PasswordHash, Role, CreatedAt)
                VALUES (@FullName, @Email, @PasswordHash, @Role, @CreatedAt);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            var userId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                request.FullName,
                request.Email,
                PasswordHash = passwordHash,
                Role = "User",
                CreatedAt = DateTime.Now
            });

            var user = new User { Id = userId, FullName = request.FullName, Email = request.Email, Role = "User" };
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            return new AuthResponse
            {
                Id = userId,
                FullName = request.FullName,
                Email = request.Email,
                Role = "User",
                Token = token,
                RefreshToken = refreshToken,
                IsProfileComplete = false,
                Message = "Registration successful!"
            };
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null)
        {
            using var connection = _dbConnectionFactory.CreateConnection();

            var email = request.Email?.Trim();
            var password = request.Password?.Trim();

            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email", 
                new { Email = email });

            if (user == null)
            {
                return new AuthResponse { Message = "User does not exist." };
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                if (!string.IsNullOrEmpty(user.Provider))
                {
                    return new AuthResponse { Message = $"This account is registered with {user.Provider}. Please use social login." };
                }
                return new AuthResponse { Message = "Account has no password set. Please reset your password." };
            }

            bool isPasswordCorrect = false;
            bool needsRehash = false;

            try
            {
                
                if (user.PasswordHash.StartsWith("$2") && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    isPasswordCorrect = true;
                }
                
                else if (password == user.PasswordHash)
                {
                    isPasswordCorrect = true;
                    needsRehash = true;
                }
            }
            catch (Exception)
            {
                
                if (password == user.PasswordHash)
                {
                    isPasswordCorrect = true;
                    needsRehash = true;
                }
            }

            if (!isPasswordCorrect)
            {
                return new AuthResponse { Message = "Incorrect password." };
            }

            if (needsRehash)
            {
                var newHash = BCrypt.Net.BCrypt.HashPassword(password);
                await connection.ExecuteAsync("UPDATE Users SET PasswordHash = @NewHash WHERE Id = @Id", 
                    new { NewHash = newHash, user.Id });
            }

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            
            var location = await GetLocationAsync(ipAddress);

            await connection.ExecuteAsync(@"
                UPDATE Users 
                SET LastLoginAt = @LastLogin,
                    RefreshToken = @RefreshToken,
                    RefreshTokenExpiryTime = @RefreshTokenExpiryTime
                WHERE Id = @Id", 
                new { 
                    LastLogin = DateTime.Now, 
                    RefreshToken = refreshToken,
                    RefreshTokenExpiryTime = user.RefreshTokenExpiryTime,
                    user.Id 
                });

            
            await connection.ExecuteAsync(@"
                INSERT INTO UserSessions (UserId, IPAddress, Device, Location, LoginAt, IsActive, RefreshToken)
                VALUES (@UserId, @IPAddress, @Device, @Location, @LoginAt, 1, @RefreshToken)",
                new { 
                    UserId = user.Id, 
                    IPAddress = ipAddress, 
                    Device = userAgent, 
                    Location = location,
                    LoginAt = DateTime.Now,
                    RefreshToken = refreshToken
                });

            var profileExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM UserProfiles WHERE UserId = @UserId", new { UserId = user.Id }) > 0;

            return new AuthResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Token = token,
                RefreshToken = refreshToken,
                IsProfileComplete = profileExists,
                Message = $"Welcome back, {user.Role}!"
            };
        }

        public async Task<AuthResponse> SocialLoginAsync(SocialLoginRequest request, string? ipAddress = null, string? userAgent = null)
        {
            using var connection = _dbConnectionFactory.CreateConnection();

            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email", new { request.Email });

            if (user == null)
            {
                var sql = @"
                    INSERT INTO Users (FullName, Email, Provider, ExternalId, Role, CreatedAt)
                    VALUES (@FullName, @Email, @Provider, @ExternalId, @Role, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                var userId = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    request.FullName,
                    request.Email,
                    request.Provider,
                    request.ExternalId,
                    Role = "User",
                    CreatedAt = DateTime.Now
                });

                user = new User { Id = userId, FullName = request.FullName, Email = request.Email, Role = "User" };
            }

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            
            var location = await GetLocationAsync(ipAddress);

            
            await connection.ExecuteAsync(@"
                UPDATE Users 
                SET LastLoginAt = @LastLogin,
                    RefreshToken = @RefreshToken,
                    RefreshTokenExpiryTime = @RefreshTokenExpiryTime
                WHERE Id = @Id", 
                new { 
                    LastLogin = DateTime.Now, 
                    RefreshToken = refreshToken,
                    RefreshTokenExpiryTime = user.RefreshTokenExpiryTime,
                    user.Id 
                });

            
            await connection.ExecuteAsync(@"
                INSERT INTO UserSessions (UserId, IPAddress, Device, Location, LoginAt, IsActive, RefreshToken)
                VALUES (@UserId, @IPAddress, @Device, @Location, @LoginAt, 1, @RefreshToken)",
                new { 
                    UserId = user.Id, 
                    IPAddress = ipAddress, 
                    Device = userAgent, 
                    Location = location,
                    LoginAt = DateTime.Now,
                    RefreshToken = refreshToken
                });

            var profileExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM UserProfiles WHERE UserId = @UserId", new { UserId = user.Id }) > 0;

            return new AuthResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Token = token,
                RefreshToken = refreshToken,
                IsProfileComplete = profileExists,
                Message = $"Logged in with {request.Provider}."
            };
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(double.Parse(_configuration["Jwt:ExpiryMinutes"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<string> GetLocationAsync(string? ip)
        {
            if (string.IsNullOrEmpty(ip) || ip == "::1" || ip == "127.0.0.1")
                return "Localhost";

            try
            {
                using var client = new HttpClient();
                var response = await client.GetFromJsonAsync<IpApiResponse>($"http://ip-api.com/json/{ip}");
                if (response != null && response.Status == "success")
                {
                    return $"{response.City}, {response.Country}";
                }
            }
            catch { }

            return "Unknown Location";
        }

        private class IpApiResponse
        {
            public string Status { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
        }

        public async Task<bool> CompleteRegistrationAsync(CompleteRegistrationRequest request)
        {
            using var connection = _dbConnectionFactory.CreateConnection();

            string? profileImageUrl = request.ProfileImageUrl;

            if (request.ProfileImage != null)
            {
                var uploadResult = await _photoService.AddPhotoAsync(request.ProfileImage);
                if (uploadResult.Error != null)
                {
                    throw new Exception(uploadResult.Error.Message);
                }
                profileImageUrl = uploadResult.SecureUrl.ToString();
            }

            var sql = @"
                IF EXISTS (SELECT 1 FROM UserProfiles WHERE UserId = @UserId)
                BEGIN
                    UPDATE UserProfiles 
                    SET PhoneNumber = @PhoneNumber,
                        Country = @Country,
                        TravelType = @TravelType,
                        BudgetPreference = @BudgetPreference,
                        TravelStyle = @TravelStyle,
                        PreferredTransport = @PreferredTransport,
                        ProfileImageUrl = @ProfileImageUrl,
                        UpdatedAt = @UpdatedAt
                    WHERE UserId = @UserId
                END
                ELSE
                BEGIN
                    INSERT INTO UserProfiles (UserId, PhoneNumber, Country, TravelType, BudgetPreference, TravelStyle, PreferredTransport, ProfileImageUrl, CreatedAt)
                    VALUES (@UserId, @PhoneNumber, @Country, @TravelType, @BudgetPreference, @TravelStyle, @PreferredTransport, @ProfileImageUrl, @UpdatedAt)
                END";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                request.UserId,
                request.PhoneNumber,
                request.Country,
                request.TravelType,
                request.BudgetPreference,
                request.TravelStyle,
                request.PreferredTransport,
                ProfileImageUrl = profileImageUrl,
                UpdatedAt = DateTime.Now
            });

            return affectedRows > 0;
        }

        public async Task<UserDto?> GetUserByIdAsync(int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = @"
                SELECT u.Id, u.FullName, u.Email, p.PhoneNumber, p.Country, 
                       p.TravelType, p.BudgetPreference, p.TravelStyle, p.PreferredTransport, p.ProfileImageUrl
                FROM Users u
                LEFT JOIN UserProfiles p ON u.Id = p.UserId
                WHERE u.Id = @Id";
            
            var userDto = await connection.QueryFirstOrDefaultAsync<UserDto>(sql, new { Id = userId });
            
            if (userDto != null)
            {
                var sessionsSql = "SELECT Id, IPAddress, Device, Location, LoginAt, IsActive FROM UserSessions WHERE UserId = @UserId AND IsActive = 1 ORDER BY LoginAt DESC";
                var sessions = await connection.QueryAsync<UserSessionDto>(sessionsSql, new { UserId = userId });
                userDto.ActiveSessions = sessions.ToList();
            }

            return userDto;
        }

        public async Task<UserDto?> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
           
            await connection.ExecuteAsync("UPDATE Users SET FullName = @FullName, UpdatedAt = @UpdatedAt WHERE Id = @UserId", 
                new { userId, request.FullName, UpdatedAt = DateTime.Now });

            // Update details in UserProfiles table
            var sql = @"
                IF EXISTS (SELECT 1 FROM UserProfiles WHERE UserId = @UserId)
                BEGIN
                    UPDATE UserProfiles 
                    SET PhoneNumber = @PhoneNumber,
                        Country = @Country,
                        TravelType = @TravelType,
                        BudgetPreference = @BudgetPreference,
                        TravelStyle = @TravelStyle,
                        PreferredTransport = @PreferredTransport,
                        UpdatedAt = @UpdatedAt
                    WHERE UserId = @UserId
                END
                ELSE
                BEGIN
                    INSERT INTO UserProfiles (UserId, PhoneNumber, Country, TravelType, BudgetPreference, TravelStyle, PreferredTransport, CreatedAt)
                    VALUES (@UserId, @PhoneNumber, @Country, @TravelType, @BudgetPreference, @TravelStyle, @PreferredTransport, @UpdatedAt)
                END";

            var affectedRows = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                request.PhoneNumber,
                request.Country,
                request.TravelType,
                request.BudgetPreference,
                request.TravelStyle,
                request.PreferredTransport,
                UpdatedAt = DateTime.Now
            });

            if (affectedRows > 0)
            {
                return await GetUserByIdAsync(userId);
            }

            return null;
        }

        public async Task<string?> UpdateProfileImageAsync(int userId, IFormFile image)
        {
            var uploadResult = await _photoService.AddPhotoAsync(image);
            if (uploadResult.Error != null)
            {
                throw new Exception(uploadResult.Error.Message);
            }

            var profileImageUrl = uploadResult.SecureUrl.ToString();

            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = @"
                IF EXISTS (SELECT 1 FROM UserProfiles WHERE UserId = @UserId)
                BEGIN
                    UPDATE UserProfiles SET ProfileImageUrl = @ProfileImageUrl, UpdatedAt = @UpdatedAt WHERE UserId = @UserId
                END
                ELSE
                BEGIN
                    INSERT INTO UserProfiles (UserId, ProfileImageUrl, CreatedAt) VALUES (@UserId, @ProfileImageUrl, @UpdatedAt)
                END";
            await connection.ExecuteAsync(sql, new { ProfileImageUrl = profileImageUrl, UpdatedAt = DateTime.Now, UserId = userId });

            return profileImageUrl;
        }

        public async Task LogoutAsync(int userId, string? refreshToken = null)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            if (!string.IsNullOrEmpty(refreshToken))
            {
                
                await connection.ExecuteAsync("DELETE FROM UserSessions WHERE UserId = @UserId AND RefreshToken = @RefreshToken", 
                    new { UserId = userId, RefreshToken = refreshToken });
                
        
                await connection.ExecuteAsync("UPDATE Users SET RefreshToken = NULL, RefreshTokenExpiryTime = NULL WHERE Id = @Id AND RefreshToken = @RefreshToken", 
                    new { Id = userId, RefreshToken = refreshToken });
            }
            else
            {
              
                await connection.ExecuteAsync("DELETE FROM UserSessions WHERE UserId = @UserId", new { UserId = userId });
                await connection.ExecuteAsync("UPDATE Users SET RefreshToken = NULL, RefreshTokenExpiryTime = NULL WHERE Id = @Id", new { Id = userId });
            }
        }

        public async Task<bool> TerminateSessionAsync(int sessionId, int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
           
            var refreshToken = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT RefreshToken FROM UserSessions WHERE Id = @SessionId", new { SessionId = sessionId });

            var affectedRows = await connection.ExecuteAsync(
                "DELETE FROM UserSessions WHERE Id = @SessionId AND UserId = @UserId", 
                new { SessionId = sessionId, UserId = userId });

            if (affectedRows > 0 && !string.IsNullOrEmpty(refreshToken))
            {
              
                await connection.ExecuteAsync(
                    "UPDATE Users SET RefreshToken = NULL, RefreshTokenExpiryTime = NULL WHERE Id = @UserId AND RefreshToken = @RefreshToken",
                    new { UserId = userId, RefreshToken = refreshToken });
            }

            return affectedRows > 0;
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
           
            var user = await connection.QueryFirstOrDefaultAsync<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = userId });
            if (user == null) return false;

           
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                bool isPasswordCorrect = false;
                try
                {
                    if (user.PasswordHash.StartsWith("$2") && BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                    {
                        isPasswordCorrect = true;
                    }
                    else if (request.CurrentPassword == user.PasswordHash)
                    {
                        isPasswordCorrect = true;
                    }
                }
                catch
                {
                    if (request.CurrentPassword == user.PasswordHash)
                    {
                        isPasswordCorrect = true;
                    }
                }

                if (!isPasswordCorrect)
                {
                    return false;
                }
            }

            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            var affectedRows = await connection.ExecuteAsync(
                "UPDATE Users SET PasswordHash = @NewHash, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { NewHash = newPasswordHash, UpdatedAt = DateTime.Now, Id = userId }
            );

            return affectedRows > 0;
        }

        public async Task<bool> DeleteAccountAsync(int userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var affectedRows = await connection.ExecuteAsync("DELETE FROM Users WHERE Id = @Id", new { Id = userId });
            return affectedRows > 0;
        }
    }
}
