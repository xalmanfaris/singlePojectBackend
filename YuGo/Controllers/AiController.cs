using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YuGo.DTOs;
using YuGo.Interfaces;

namespace YuGo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly IGeminiService _geminiService;

        public AiController(IGeminiService geminiService)
        {
            _geminiService = geminiService;
        }

        [HttpPost("suggest-transport")]
        public async Task<IActionResult> SuggestTransport([FromBody] AiSuggestionRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.StartingLocation) || string.IsNullOrWhiteSpace(request.Destination))
            {
                return BadRequest(new { message = "Starting location and destination are required." });
            }

            try
            {
                var jsonResult = await _geminiService.GetTransportSuggestionAsync(
                    request.StartingLocation, 
                    request.Destination, 
                    request.Dates,
                    request.TransportMode
                );
                
                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }

        [HttpPost("analyze-omissions")]
        public async Task<IActionResult> AnalyzeOmissions([FromBody] AiAnalyzeOmissionsRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Destination) || string.IsNullOrWhiteSpace(request.OmittedItemsJson))
            {
                return BadRequest(new { message = "Destination and omitted items are required." });
            }

            try
            {
                var jsonResult = await _geminiService.AnalyzeOmissionsAsync(request.Destination, request.OmittedItemsJson);
                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }

        [HttpPost("suggest-preferences")]
        public async Task<IActionResult> SuggestPreferences([FromBody] AiPreferenceRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.StartingLocation) || string.IsNullOrWhiteSpace(request.Destination))
            {
                return BadRequest(new { message = "Starting location and destination are required." });
            }

            try
            {
                var jsonResult = await _geminiService.GetPreferenceSuggestionAsync(
                    request.StartingLocation, 
                    request.Destination, 
                    request.Dates,
                    request.Travelers,
                    request.TransportMode,
                    request.BudgetMode,
                    request.BudgetRange?.Min ?? 0,
                    request.BudgetRange?.Max ?? 0,
                    request.BudgetStyle
                );
                
                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }

        [HttpPost("estimate-budget")]
        public async Task<IActionResult> EstimateBudget([FromBody] AiBudgetEstimateRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Destination))
            {
                return BadRequest("Destination is required.");
            }

            try
            {
                var jsonResult = await _geminiService.EstimateBudgetAsync(
                    request.StartingLocation, 
                    request.Destination, 
                    request.Dates,
                    request.Travelers
                );
                
                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }
        [HttpPost("suggest-activities")]
        public async Task<IActionResult> SuggestActivities([FromBody] AiActivityRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Destination) || string.IsNullOrWhiteSpace(request.TripType))
            {
                return BadRequest(new { message = "Destination and TripType are required." });
            }

            try
            {
                var jsonResult = await _geminiService.GetActivitySuggestionsAsync(
                    request.StartingLocation,
                    request.Destination,
                    request.Dates,
                    request.TripType,
                    request.Travelers
                );

                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }
        [HttpPost("suggest-packing")]
        public async Task<IActionResult> SuggestPacking([FromBody] AiPackingRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Destination))
            {
                return BadRequest(new { message = "Destination is required." });
            }

            try
            {
                var jsonResult = await _geminiService.GetPackingSuggestionsAsync(
                    request.Destination,
                    request.StartingLocation,
                    request.Dates,
                    request.Travelers,
                    request.TransportMode,
                    request.TripType,
                    request.FoodPreferences,
                    request.StayType,
                    request.BudgetStyle
                );

                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }
        [HttpPost("generate-plan")]
        public async Task<IActionResult> GeneratePlan([FromBody] AiTripPlanRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Destination))
            {
                return BadRequest(new { message = "Destination is required." });
            }

            try
            {
                var jsonResult = await _geminiService.GenerateTripPlanAsync(
                    request.StartingLocation,
                    request.Destination,
                    request.StartDate,
                    request.EndDate,
                    request.Travelers,
                    request.TransportMode,
                    request.BudgetMode,
                    request.BudgetMin,
                    request.BudgetMax,
                    request.BudgetStyle,
                    request.TripType,
                    request.FoodPreferences,
                    request.StayPreference,
                    request.TravelPace
                );

                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }

        [HttpPost("trip-insights")]
        public async Task<IActionResult> GetTripInsights([FromBody] AiTripInsightsRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Destination))
            {
                return BadRequest(new { message = "Destination is required." });
            }

            try
            {
                var jsonResult = await _geminiService.GetTripInsightsAsync(
                    request.Destination,
                    request.StartingLocation,
                    request.Dates,
                    request.Travelers
                );

                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }

        [HttpPost("suggest-recovery")]
        public async Task<IActionResult> SuggestRecovery([FromBody] AiRecoveryStepsRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.ItemName) || string.IsNullOrWhiteSpace(request.LastLocation))
            {
                return BadRequest(new { message = "Item name and last location are required." });
            }

            try
            {
                var jsonResult = await _geminiService.GetRecoveryStepsAsync(
                    request.ItemName,
                    request.LastLocation,
                    request.Reason
                );

                return Content(jsonResult, "application/json");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error communicating with AI service.", details = ex.Message });
            }
        }
    }
}
