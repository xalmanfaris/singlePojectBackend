namespace YuGo.DTOs
{
    public class AiTripPlanRequestDto
    {
        public string StartingLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int Travelers { get; set; }
        public string TransportMode { get; set; } = string.Empty;
        public string BudgetMode { get; set; } = string.Empty;
        public decimal BudgetMin { get; set; }
        public decimal BudgetMax { get; set; }
        public string BudgetStyle { get; set; } = string.Empty;
        public string TripType { get; set; } = string.Empty;
        public string FoodPreferences { get; set; } = string.Empty;
        public string StayPreference { get; set; } = string.Empty;
        public string TravelPace { get; set; } = "Moderate"; // Optional, default
    }
}
