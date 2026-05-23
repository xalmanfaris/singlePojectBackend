namespace YuGo.DTOs
{
    public class AiTripInsightsRequestDto
    {
        public string Destination { get; set; } = string.Empty;
        public string StartingLocation { get; set; } = string.Empty;
        public string Dates { get; set; } = string.Empty;
        public int Travelers { get; set; }
    }
}
