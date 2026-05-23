using System;

namespace YuGo.Models
{
    public class UserSession
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? IPAddress { get; set; }
        public string? Device { get; set; }
        public string? Location { get; set; }
        public DateTime LoginAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string? RefreshToken { get; set; }
    }
}
