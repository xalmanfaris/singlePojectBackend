using System.Text.Json.Serialization;

namespace YuGo.DTOs
{
    public class SaveTripRequestDto
    {
        public int? Id { get; set; }
        public string Destination { get; set; } = string.Empty;
        public string? StartingLocation { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Travelers { get; set; }
        
        // Using JsonElement is required for correct serialization in System.Text.Json
        public System.Text.Json.JsonElement? TripData { get; set; }
        public System.Text.Json.JsonElement? AiPlan { get; set; }
    }
}
