using Dapper;
using System.Text.Json;
using YuGo.Data;
using YuGo.DTOs;
using YuGo.Interfaces;
using YuGo.Models;
using System.Text.Json.Nodes;
using System.Linq;

namespace YuGo.Services
{
    public class TripService : ITripService
    {
        private readonly DbConnectionFactory _dbFactory;

        public TripService(DbConnectionFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<int> SaveTripAsync(int userId, SaveTripRequestDto request)
        {
            using var connection = _dbFactory.CreateConnection();
            
            var tripDataJson = request.TripData?.GetRawText();
            var aiPlanJson = request.AiPlan?.GetRawText();

            if (request.Id.HasValue && request.Id.Value > 0)
            {
                var sql = @"
                    UPDATE TripPlans 
                    SET Destination = @Destination, 
                        StartingLocation = @StartingLocation, 
                        StartDate = @StartDate, 
                        EndDate = @EndDate, 
                        Travelers = @Travelers, 
                        TripDataJson = @TripDataJson, 
                        AiPlanJson = @AiPlanJson
                    WHERE Id = @Id AND UserId = @UserId;
                ";
                await connection.ExecuteAsync(sql, new
                {
                    Id = request.Id.Value,
                    UserId = userId,
                    Destination = request.Destination,
                    StartingLocation = request.StartingLocation,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Travelers = request.Travelers,
                    TripDataJson = tripDataJson,
                    AiPlanJson = aiPlanJson
                });
                return request.Id.Value;
            }
            else
            {
                var sql = @"
                    INSERT INTO TripPlans (UserId, Destination, StartingLocation, StartDate, EndDate, Travelers, TripDataJson, AiPlanJson)
                    VALUES (@UserId, @Destination, @StartingLocation, @StartDate, @EndDate, @Travelers, @TripDataJson, @AiPlanJson);
                    SELECT CAST(SCOPE_IDENTITY() as int);
                ";

                var id = await connection.QuerySingleAsync<int>(sql, new
                {
                    UserId = userId,
                    Destination = request.Destination,
                    StartingLocation = request.StartingLocation,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Travelers = request.Travelers,
                    TripDataJson = tripDataJson,
                    AiPlanJson = aiPlanJson
                });

                return id;
            }
        }

        public async Task<IEnumerable<TripPlan>> GetUserTripsAsync(int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = "SELECT * FROM TripPlans WHERE UserId = @UserId ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<TripPlan>(sql, new { UserId = userId });
        }

        public async Task<TripPlan?> GetTripByIdAsync(int tripId, int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = "SELECT * FROM TripPlans WHERE Id = @Id AND UserId = @UserId";
            return await connection.QuerySingleOrDefaultAsync<TripPlan>(sql, new { Id = tripId, UserId = userId });
        }

        public async Task SaveChecklistStateAsync(int tripId, int userId, string checkedItemsJson)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = @"
                IF EXISTS (SELECT 1 FROM StartedTrips WHERE TripId = @TripId AND UserId = @UserId)
                BEGIN
                    UPDATE StartedTrips SET CheckedItemsJson = @CheckedItemsJson, UpdatedAt = GETDATE() WHERE TripId = @TripId AND UserId = @UserId
                END
                ELSE
                BEGIN
                    INSERT INTO StartedTrips (TripId, UserId, CheckedItemsJson) VALUES (@TripId, @UserId, @CheckedItemsJson)
                END
            ";
            await connection.ExecuteAsync(sql, new { TripId = tripId, UserId = userId, CheckedItemsJson = checkedItemsJson });
        }

        public async Task<string?> GetChecklistStateAsync(int tripId, int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = "SELECT CheckedItemsJson FROM StartedTrips WHERE TripId = @TripId AND UserId = @UserId";
            return await connection.QuerySingleOrDefaultAsync<string>(sql, new { TripId = tripId, UserId = userId });
        }

        public async Task<bool> DeleteTripAsync(int tripId, int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = "DELETE FROM TripPlans WHERE Id = @Id AND UserId = @UserId";
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = tripId, UserId = userId });
            return rowsAffected > 0;
        }

        public async Task UpdateCurrentLocationAsync(int tripId, int userId, int locationIndex)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = @"
                UPDATE StartedTrips SET CurrentLocationIndex = @LocationIndex, UpdatedAt = GETDATE() 
                WHERE TripId = @TripId AND UserId = @UserId
            ";
            await connection.ExecuteAsync(sql, new { TripId = tripId, UserId = userId, LocationIndex = locationIndex });
        }

        public async Task<int> GetCurrentLocationAsync(int tripId, int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = "SELECT CurrentLocationIndex FROM StartedTrips WHERE TripId = @TripId AND UserId = @UserId";
            return await connection.QuerySingleOrDefaultAsync<int>(sql, new { TripId = tripId, UserId = userId });
        }

        public async Task SaveLostItemAsync(int tripId, int userId, string itemName, string predictedLocation, string reason)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = @"
                INSERT INTO LostItems (TripId, UserId, ItemName, PredictedLocation, Reason)
                VALUES (@TripId, @UserId, @ItemName, @PredictedLocation, @Reason)
            ";
            await connection.ExecuteAsync(sql, new { TripId = tripId, UserId = userId, ItemName = itemName, PredictedLocation = predictedLocation, Reason = reason });
        }

        public async Task RemoveItemFromTripAsync(int tripId, int userId, string itemName)
        {
            using var connection = _dbFactory.CreateConnection();
            var trip = await GetTripByIdAsync(tripId, userId);
            if (trip == null) return;

            // 1. Update TripPlans (AiPlanJson & TripDataJson)
            try {
                bool updated = false;

                // Update AiPlanJson
                if (!string.IsNullOrEmpty(trip.AiPlanJson))
                {
                    var node = JsonNode.Parse(trip.AiPlanJson);
                    if (RemoveFromPackingList(node?["packingList"]?.AsArray(), itemName))
                    {
                        trip.AiPlanJson = node!.ToJsonString();
                        updated = true;
                    }
                }

                // Update TripDataJson (where aiPacking is stored)
                if (!string.IsNullOrEmpty(trip.TripDataJson))
                {
                    var node = JsonNode.Parse(trip.TripDataJson);
                    if (RemoveFromPackingList(node?["aiPacking"]?["categories"]?.AsArray(), itemName))
                    {
                        trip.TripDataJson = node!.ToJsonString();
                        updated = true;
                    }
                }

                if (updated)
                {
                    await connection.ExecuteAsync(
                        "UPDATE TripPlans SET AiPlanJson = @AiPlan, TripDataJson = @TripData WHERE Id = @Id AND UserId = @UserId", 
                        new { AiPlan = trip.AiPlanJson, TripData = trip.TripDataJson, Id = tripId, UserId = userId });
                }
            } catch (Exception ex) {
                Console.WriteLine("Error updating Trip JSON blobs: " + ex.Message);
            }
        }

        private bool RemoveFromPackingList(JsonArray? packingList, string itemName)
        {
            if (packingList == null) return false;
            bool anyRemoved = false;
            var categoriesToRemove = new List<JsonNode>();

            foreach (var category in packingList)
            {
                var items = category?["items"]?.AsArray();
                if (items != null)
                {
                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        var item = items[i];
                        var name = (item is JsonObject) ? item["name"]?.ToString() : item?.ToString();
                        if (string.Equals(name, itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            items.RemoveAt(i);
                            anyRemoved = true;
                        }
                    }
                    if (items.Count == 0) categoriesToRemove.Add(category!);
                }
            }

            foreach (var cat in categoriesToRemove) 
            {
                packingList.Remove(cat);
                anyRemoved = true;
            }
            return anyRemoved;
        }

            // 2. Note: StartedTrips (Checklist) is best handled by the frontend 
            // since it involves index recalculation which requires the full list context.
            // But at least the item is gone from the master plan in the DB now.

        public async Task UpdateActivityTimeAsync(int tripId, int userId, int day, int activityIndex, string newTime)
        {
            using var connection = _dbFactory.CreateConnection();
            var trip = await GetTripByIdAsync(tripId, userId);
            if (trip == null || string.IsNullOrEmpty(trip.AiPlanJson)) return;

            try
            {
                var node = JsonNode.Parse(trip.AiPlanJson);
                var itinerary = node?["itinerary"]?.AsArray();
                if (itinerary != null)
                {
                    var dayNode = itinerary.FirstOrDefault(d => d?["day"]?.GetValue<int>() == day);
                    var activities = dayNode?["activities"]?.AsArray();
                    if (activities != null && activityIndex >= 0 && activityIndex < activities.Count)
                    {
                        activities[activityIndex]!["time"] = newTime;
                        trip.AiPlanJson = node!.ToJsonString();

                        await connection.ExecuteAsync(
                            "UPDATE TripPlans SET AiPlanJson = @AiPlan WHERE Id = @Id AND UserId = @UserId",
                            new { AiPlan = trip.AiPlanJson, Id = tripId, UserId = userId });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error updating activity time: " + ex.Message);
            }
        }

        public async Task UpdateTripStartTimeAsync(int tripId, int userId, DateTime newStartTime)
        {
            using var connection = _dbFactory.CreateConnection();
            
            // 1. Check if the trip exists first
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM TripPlans WHERE Id = @Id AND UserId = @UserId", 
                new { Id = tripId, UserId = userId });
            
            Console.WriteLine($"[DEBUG] Trip existence check: {exists} found for Id={tripId}, UserId={userId}");

            if (exists == 0)
            {
                // Try to find it by ID only to see if UserId is the problem
                var existsById = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM TripPlans WHERE Id = @Id", 
                    new { Id = tripId });
                Console.WriteLine($"[DEBUG] Trip existence check by ID only: {existsById} found for Id={tripId}");
            }

            // 2. Perform the update
            var sql = "UPDATE TripPlans SET StartDate = @StartDate WHERE Id = @Id AND UserId = @UserId";
            var rows = await connection.ExecuteAsync(sql, new { StartDate = newStartTime, Id = tripId, UserId = userId });
            
            Console.WriteLine($"[DEBUG] Update completed. Rows affected: {rows}");
            
            if (rows == 0)
            {
                throw new Exception($"Failed to update Trip {tripId}. Trip not found or unauthorized.");
            }
        }

        public async Task<IEnumerable<TripPlan>> GetAllTripsForBackgroundServiceAsync()
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QueryAsync<TripPlan>("SELECT * FROM TripPlans");
        }

        public async Task<IEnumerable<LostItem>> GetUserLostItemsAsync(int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = @"
                SELECT li.*, tp.Destination as TripDestination 
                FROM LostItems li
                JOIN TripPlans tp ON li.TripId = tp.Id
                WHERE li.UserId = @UserId
                ORDER BY li.CreatedAt DESC";
            return await connection.QueryAsync<LostItem>(sql, new { UserId = userId });
        }

        public async Task<bool> MarkLostItemAsRecoveredAsync(int itemId, int userId, string? recoveredFrom)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = @"
                UPDATE LostItems 
                SET IsRecovered = 1, RecoveredFrom = @RecoveredFrom
                WHERE Id = @Id AND UserId = @UserId";
            var affectedRows = await connection.ExecuteAsync(sql, new { Id = itemId, UserId = userId, RecoveredFrom = recoveredFrom });
            return affectedRows > 0;
        }

        // Notification Methods
        public async Task SaveNotificationAsync(Notification notification)
        {
            try {
                using var connection = _dbFactory.CreateConnection();
                var sql = @"
                    INSERT INTO Notifications (UserId, TripId, Destination, Message, Type, Timestamp, IsRead)
                    VALUES (@UserId, @TripId, @Destination, @Message, @Type, @Timestamp, @IsRead);
                    SELECT CAST(SCOPE_IDENTITY() as int);
                ";
                notification.Id = await connection.QuerySingleAsync<int>(sql, notification);
                Console.WriteLine($"[DEBUG] Notification saved with ID: {notification.Id}");
            } catch (Exception ex) {
                Console.WriteLine($"[ERROR] Failed to save notification: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = "SELECT * FROM Notifications WHERE UserId = @UserId ORDER BY Timestamp DESC";
            return await connection.QueryAsync<Notification>(sql, new { UserId = userId });
        }

        public async Task MarkNotificationAsReadAsync(int notificationId, int userId)
        {
            using var connection = _dbFactory.CreateConnection();
            var sql = "UPDATE Notifications SET IsRead = 1 WHERE Id = @Id AND UserId = @UserId";
            await connection.ExecuteAsync(sql, new { Id = notificationId, UserId = userId });
        }

        public async Task<bool> NotificationExistsRecentAsync(int tripId, string type, int minutesThreshold)
        {
            using var connection = _dbFactory.CreateConnection();
            // Check if any notification of this type exists within the last X minutes
            var sql = @"
                SELECT COUNT(1) 
                FROM Notifications 
                WHERE TripId = @TripId AND Type = @Type 
                AND Timestamp > DATEADD(minute, -@Threshold, GETDATE())";
            
            var count = await connection.ExecuteScalarAsync<int>(sql, 
                new { TripId = tripId, Type = type, Threshold = minutesThreshold });
            
            return count > 0;
        }
    }
}
