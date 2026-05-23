namespace YuGo.DTOs
{
    public class AiActivityRequestDto
    {
        public string StartingLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Dates { get; set; } = string.Empty;
        public string TripType { get; set; } = string.Empty;
        public int Travelers { get; set; }
    }
}
