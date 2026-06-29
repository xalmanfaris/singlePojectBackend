using Microsoft.AspNetCore.Mvc;
using YuGo.DTOs;
using YuGo.Interfaces;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace YuGo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || !Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return BadRequest("Invalid email format.");
                }

                if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 || 
                    !Regex.IsMatch(request.Password, @"[a-zA-Z]") || !Regex.IsMatch(request.Password, @"[0-9]"))
                {
                    return BadRequest("Password must be at least 8 characters long and contain both letters and numbers.");
                }

                if (await _authService.UserExistsAsync(request.Email))
                {
                    return BadRequest("User already exists with this email.");
                }

                var response = await _authService.RegisterAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal server error occurred during registration.",
                    details = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();
                var response = await _authService.LoginAsync(request, ipAddress, userAgent);

                if (response == null || string.IsNullOrEmpty(response.Token))
                {
                    return Unauthorized(new { message = response?.Message ?? "Invalid login attempt." });
                }

                
                Response.Cookies.Append("X-Access-Token", response.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.Now.AddDays(7)
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal server error occurred during login.",
                    details = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpPost("admin-login")]
        public async Task<IActionResult> AdminLogin([FromForm] LoginRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var response = await _authService.LoginAsync(request, ipAddress, userAgent);

            if (response == null || string.IsNullOrEmpty(response.Token))
            {
                return Unauthorized(new { message = response?.Message ?? "Invalid login attempt." });
            }

            if (response.Role != "Admin")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied. Only administrators are allowed." });
            }

            Response.Cookies.Append("X-Access-Token", response.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.Now.AddDays(7)
            });

            return Ok(response);
        }

        [HttpPost("social-login")]
        public async Task<IActionResult> SocialLogin([FromForm] SocialLoginRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var response = await _authService.SocialLoginAsync(request, ipAddress, userAgent);
            
            if (response != null && !string.IsNullOrEmpty(response.Token))
            {
                
                Response.Cookies.Append("X-Access-Token", response.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.Now.AddDays(7)
                });
            }

            return Ok(response);
        }

        [Authorize]
        [HttpPost("complete-registration")]
        public async Task<IActionResult> CompleteRegistration([FromForm] CompleteRegistrationRequest request)
        {
            
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized("Invalid token.");

            request.UserId = int.Parse(userIdClaim.Value);
            var success = await _authService.CompleteRegistrationAsync(request);

            if (!success)
            {
                return NotFound("User not found.");
            }

            return Ok(new { Message = "Registration completed successfully." });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromForm] string? refreshToken)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            await _authService.LogoutAsync(int.Parse(userIdClaim.Value), refreshToken);
            
            // Clear Swagger auth cookie
            Response.Cookies.Delete("X-Access-Token");
            
            return Ok(new { Message = "Logged out successfully." });
        }

        [Authorize]
        [HttpPost("terminate-session")]
        public async Task<IActionResult> TerminateSession([FromForm] int sessionId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            var result = await _authService.TerminateSessionAsync(sessionId, int.Parse(userIdClaim.Value));
            if (result) return Ok(new { Message = "Session terminated." });
            return BadRequest(new { Message = "Failed to terminate session." });
        }
    }
}
