using System;

namespace YuGo.Models
{
    public class UserProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? TravelType { get; set; }
        public string? BudgetPreference { get; set; }
        public string? TravelStyle { get; set; }
        public string? PreferredTransport { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

       
        public User? User { get; set; }
    }
}
