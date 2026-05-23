namespace YuGo.DTOs
{
    public class AiPackingRequestDto
    {
        public string Destination { get; set; } = string.Empty;
        public string StartingLocation { get; set; } = string.Empty;
        public string Dates { get; set; } = string.Empty;
        public int Travelers { get; set; }
        public string TransportMode { get; set; } = string.Empty;
        public string TripType { get; set; } = string.Empty;
        public string FoodPreferences { get; set; } = string.Empty;
        public string StayType { get; set; } = string.Empty;
        public string BudgetStyle { get; set; } = string.Empty;
    }
}
