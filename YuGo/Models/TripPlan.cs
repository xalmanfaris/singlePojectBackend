namespace YuGo.Models
{
    public class TripPlan
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Destination { get; set; } = string.Empty;
        public string? StartingLocation { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Travelers { get; set; }
        public string? TripDataJson { get; set; }
        public string? AiPlanJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
