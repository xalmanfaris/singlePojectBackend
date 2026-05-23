using System;

namespace YuGo.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TripId { get; set; }
        public string? Destination { get; set; }
        public string? Message { get; set; }
        public string? Type { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
    }
}
