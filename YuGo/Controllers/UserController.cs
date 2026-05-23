using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YuGo.Interfaces;
using YuGo.DTOs;
using System.Security.Claims;

namespace YuGo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IAuthService _authService;

        public UserController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized("Invalid token.");

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Invalid user ID in token.");
            }

            var user = await _authService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            return Ok(user);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);
            var updatedUser = await _authService.UpdateProfileAsync(userId, request);

            if (updatedUser == null) return BadRequest("Could not update profile.");

            return Ok(updatedUser);
        }

        [HttpPost("profile-image")]
        public async Task<IActionResult> UpdateProfileImage(IFormFile image)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);
            var imageUrl = await _authService.UpdateProfileImageAsync(userId, image);

            if (imageUrl == null) return BadRequest("Could not upload image.");


            return Ok(new { imageUrl });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            int userId = int.Parse(userIdClaim.Value);
            var success = await _authService.ChangePasswordAsync(userId, request);
            if (!success)
            {
                return BadRequest("Invalid current password or update failed.");
            }

            return Ok(new { Message = "Password updated successfully." });
        }

        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            var result = await _authService.DeleteAccountAsync(int.Parse(userIdClaim.Value));
            if (result) return Ok(new { Message = "Account deleted successfully." });
            return BadRequest(new { Message = "Failed to delete account." });
        }
    }
}
