using YuGo.DTOs;
using YuGo.Models;

namespace YuGo.Interfaces
{
    public interface ITripService
    {
        Task<int> SaveTripAsync(int userId, SaveTripRequestDto request);
        Task<IEnumerable<TripPlan>> GetUserTripsAsync(int userId);
        Task<TripPlan?> GetTripByIdAsync(int tripId, int userId);
        Task SaveChecklistStateAsync(int tripId, int userId, string checkedItemsJson);
        Task<string?> GetChecklistStateAsync(int tripId, int userId);
        Task<bool> DeleteTripAsync(int tripId, int userId);
        Task UpdateCurrentLocationAsync(int tripId, int userId, int locationIndex);
        Task<int> GetCurrentLocationAsync(int tripId, int userId);
        Task SaveLostItemAsync(int tripId, int userId, string itemName, string predictedLocation, string reason);
        Task RemoveItemFromTripAsync(int tripId, int userId, string itemName);
        Task UpdateActivityTimeAsync(int tripId, int userId, int day, int activityIndex, string newTime);
        Task UpdateTripStartTimeAsync(int tripId, int userId, DateTime newStartTime);
        Task<IEnumerable<TripPlan>> GetAllTripsForBackgroundServiceAsync();
        Task<IEnumerable<LostItem>> GetUserLostItemsAsync(int userId);
        Task<bool> MarkLostItemAsRecoveredAsync(int itemId, int userId, string? recoveredFrom);
        
        // Notification Methods
        Task SaveNotificationAsync(Notification notification);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId);
        Task MarkNotificationAsReadAsync(int notificationId, int userId);
        Task<bool> NotificationExistsRecentAsync(int tripId, string type, int minutesThreshold);
    }
}
