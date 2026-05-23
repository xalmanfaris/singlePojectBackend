using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YuGo.DTOs;
using YuGo.Interfaces;
using Microsoft.AspNetCore.SignalR;
using YuGo.Hubs;
using YuGo.Models;

namespace YuGo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TripController : ControllerBase
    {
        private readonly ITripService _tripService;
        private readonly IGeminiService _geminiService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public TripController(ITripService tripService, IGeminiService geminiService, IHubContext<NotificationHub> hubContext)
        {
            _tripService = tripService;
            _geminiService = geminiService;
            _hubContext = hubContext;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveTrip([FromBody] SaveTripRequestDto request)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized("User ID not found or invalid.");
                }

                var tripId = await _tripService.SaveTripAsync(userId, request);
                return Ok(new { Message = "Trip saved successfully", TripId = tripId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while saving the trip.", Details = ex.Message });
            }
        }

        [HttpGet("my-trips")]
        public async Task<IActionResult> GetMyTrips()
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized("User ID not found or invalid.");
                }

                var trips = await _tripService.GetUserTripsAsync(userId);
                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching trips.", Details = ex.Message });
            }
        }

        [HttpGet("lost-items")]
        public async Task<IActionResult> GetUserLostItems()
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized("User ID not found or invalid.");
                }

                var lostItems = await _tripService.GetUserLostItemsAsync(userId);
                return Ok(lostItems);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching lost items.", Details = ex.Message });
            }
        }

        [HttpPost("lost-items/{id}/recover")]
        public async Task<IActionResult> MarkItemAsRecovered(int id, [FromQuery] string? recoveredFrom)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized("User ID not found or invalid.");
                }

                var success = await _tripService.MarkLostItemAsRecoveredAsync(id, userId, recoveredFrom);
                if (!success)
                {
                    return NotFound(new { Error = "Lost item not found or you don't have permission to update it." });
                }

                return Ok(new { Message = "Item marked as recovered successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while updating the lost item.", Details = ex.Message });
            }
        }

        [HttpPost("{tripId}/checklist")]
        public async Task<IActionResult> SaveChecklistState(int tripId, [FromBody] SaveChecklistRequestDto request)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized("User ID not found or invalid.");
                }

                await _tripService.SaveChecklistStateAsync(tripId, userId, request.CheckedItemsJson);
                return Ok(new { Message = "Checklist saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while saving checklist.", Details = ex.Message });
            }
        }

        [HttpGet("{tripId}/checklist")]
        public async Task<IActionResult> GetChecklistState(int tripId)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized("User ID not found or invalid.");
                }

                var state = await _tripService.GetChecklistStateAsync(tripId, userId);
                return Ok(new { CheckedItemsJson = state });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching checklist.", Details = ex.Message });
            }
        }

        [HttpDelete("{tripId}")]
        public async Task<IActionResult> DeleteTrip(int tripId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("User ID not found or invalid.");
            }

            try
            {
                var success = await _tripService.DeleteTripAsync(tripId, userId);
                if (!success) return NotFound(new { message = "Trip not found or you don't have permission to delete it." });
                return Ok(new { message = "Trip deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting trip.", details = ex.Message });
            }
        }

        [HttpPost("{tripId}/location/{index}")]
        public async Task<IActionResult> UpdateLocation(int tripId, int index)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("User ID not found or invalid.");
            }

            try
            {
                await _tripService.UpdateCurrentLocationAsync(tripId, userId, index);
                return Ok(new { message = "Location updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating location.", details = ex.Message });
            }
        }

        [HttpGet("{tripId}/location")]
        public async Task<IActionResult> GetCurrentLocation(int tripId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("User ID not found or invalid.");
            }

            try
            {
                var index = await _tripService.GetCurrentLocationAsync(tripId, userId);
                return Ok(new { CurrentLocationIndex = index });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching location.", details = ex.Message });
            }
        }

        [HttpPost("{tripId}/predict-lost")]
        public async Task<IActionResult> PredictLostItems(int tripId, [FromBody] PredictLostRequestDto request)
        {
            try
            {
                var result = await _geminiService.PredictLostItemLocationAsync(request.Destination, request.PreviousLocationsJson, request.LostItemsJson);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error predicting lost items.", details = ex.Message });
            }
        }

        [HttpPost("{tripId}/lost-item")]
        public async Task<IActionResult> SaveLostItem(int tripId, [FromBody] SaveLostItemDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("User ID not found or invalid.");
            }

            try
            {
                await _tripService.SaveLostItemAsync(tripId, userId, request.ItemName, request.PredictedLocation, request.Reason);
                return Ok(new { message = "Lost item recorded successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error recording lost item.", details = ex.Message });
            }
        }

        [HttpDelete("{tripId}/item")]
        public async Task<IActionResult> RemoveItem(int tripId, [FromQuery] string itemName)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("User ID not found or invalid.");
            }

            try
            {
                await _tripService.RemoveItemFromTripAsync(tripId, userId, itemName);
                return Ok(new { message = "Item removed from database successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error removing item from database.", details = ex.Message });
            }
        }

        [HttpPut("{tripId}/activity-time")]
        public async Task<IActionResult> UpdateActivityTime(int tripId, [FromBody] UpdateTimeRequestDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("User ID not found or invalid.");
            }

            try
            {
                await _tripService.UpdateActivityTimeAsync(tripId, userId, request.Day, request.ActivityIndex, request.NewTime);
                return Ok(new { message = "Activity time updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating activity time.", details = ex.Message });
            }
        }

        [HttpPut("{tripId}/start-time")]
        public async Task<IActionResult> UpdateTripStartTime(int tripId, [FromBody] UpdateTripStartRequestDto request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("User ID not found or invalid.");
            }

            try
            {
                Console.WriteLine($"[DEBUG] Updating Trip {tripId} StartTime to {request.NewStartTime}");
                await _tripService.UpdateTripStartTimeAsync(tripId, userId, request.NewStartTime);
                Console.WriteLine($"[DEBUG] Trip {tripId} StartTime updated successfully.");
                return Ok(new { message = "Trip start time updated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateTripStartTime Failed: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"[INNER ERROR] {ex.InnerException.Message}");
                return StatusCode(500, new { message = "Error updating trip start time.", details = ex.Message });
            }
        }
    }

    public class UpdateTripStartRequestDto
    {
        [JsonPropertyName("newStartTime")]
        public DateTime NewStartTime { get; set; }
    }

    public class UpdateTimeRequestDto
    {
        [JsonPropertyName("day")]
        public int Day { get; set; }

        [JsonPropertyName("activityIndex")]
        public int ActivityIndex { get; set; }

        [JsonPropertyName("newTime")]
        public string NewTime { get; set; } = string.Empty;
    }

    public class PredictLostRequestDto
    {
        [JsonPropertyName("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonPropertyName("previousLocationsJson")]
        public string PreviousLocationsJson { get; set; } = string.Empty;

        [JsonPropertyName("lostItemsJson")]
        public string LostItemsJson { get; set; } = string.Empty;
    }

    public class SaveLostItemDto
    {
        [JsonPropertyName("itemName")]
        public string ItemName { get; set; } = string.Empty;

        [JsonPropertyName("predictedLocation")]
        public string PredictedLocation { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
