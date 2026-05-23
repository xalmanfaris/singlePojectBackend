namespace YuGo.DTOs
{
    public class AiSuggestionRequestDto
    {
        public string StartingLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Dates { get; set; } = string.Empty;
        public string? TransportMode { get; set; }
    }
}
