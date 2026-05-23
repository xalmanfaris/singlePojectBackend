using System.Threading.Tasks;

namespace YuGo.Interfaces
{
    public interface IGeminiService
    {
        Task<string> GetTransportSuggestionAsync(string startingLocation, string destination, string dates, string? transportMode = null);
        Task<string> GetPreferenceSuggestionAsync(string startingLocation, string destination, string dates, int travelers, string transportMode, string budgetMode, decimal budgetMin, decimal budgetMax, string budgetStyle);
        Task<string> EstimateBudgetAsync(string startingLocation, string destination, string dates, int travelers);
        Task<string> GetActivitySuggestionsAsync(string startingLocation, string destination, string dates, string tripType, int travelers);
        Task<string> GetPackingSuggestionsAsync(string destination, string startingLocation, string dates, int travelers, string transportMode, string tripType, string foodPreferences, string stayType, string budgetStyle);
        Task<string> GenerateTripPlanAsync(string startingLocation, string destination, string startDate, string endDate, int travelers, string transportMode, string budgetMode, decimal budgetMin, decimal budgetMax, string budgetStyle, string tripType, string foodPreferences, string stayPreference, string travelPace);
        Task<string> AnalyzeOmissionsAsync(string destination, string omittedItemsJson);
        Task<string> PredictLostItemLocationAsync(string destination, string previousLocationsJson, string lostItemsJson);
        Task<string> GenerateNotificationMessageAsync(string destination, string contextType, string? activityName = null);
        Task<string> GetTripInsightsAsync(string destination, string startingLocation, string dates, int travelers);
        Task<string> GetRecoveryStepsAsync(string itemName, string lastLocation, string reason);
    }
}
