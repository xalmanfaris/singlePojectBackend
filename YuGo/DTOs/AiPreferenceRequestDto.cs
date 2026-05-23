namespace YuGo.DTOs
{
    public class BudgetRangeDto
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
    }

    public class AiPreferenceRequestDto
    {
        public string StartingLocation { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Dates { get; set; } = string.Empty;
        public int Travelers { get; set; }
        public string TransportMode { get; set; } = string.Empty;
        public string BudgetMode { get; set; } = string.Empty;
        public BudgetRangeDto? BudgetRange { get; set; }
        public string BudgetStyle { get; set; } = string.Empty;
    }
}
