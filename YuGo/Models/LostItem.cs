using System;

namespace YuGo.Models
{
    public class LostItem
    {
        public int Id { get; set; }
        public int TripId { get; set; }
        public int UserId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string PredictedLocation { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRecovered { get; set; } = false;
        public string? RecoveredFrom { get; set; }
        
        // Navigation properties (optional for Dapper but good for clarity)
        public string? TripDestination { get; set; }
    }
}
