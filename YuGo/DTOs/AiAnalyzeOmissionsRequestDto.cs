namespace YuGo.DTOs
{
    public class AiAnalyzeOmissionsRequestDto
    {
        public string Destination { get; set; } = string.Empty;
        public string OmittedItemsJson { get; set; } = string.Empty;
    }
}
