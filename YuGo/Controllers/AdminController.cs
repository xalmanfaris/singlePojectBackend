using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Dapper;
using YuGo.Data;
using YuGo.Interfaces;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.SignalR;
using YuGo.Hubs;
using YuGo.Models;
using System.Text.Json.Serialization;

namespace YuGo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly DbConnectionFactory _dbConnectionFactory;
        private readonly ITripService _tripService;
        private readonly IGeminiService _geminiService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AdminController(
            DbConnectionFactory dbConnectionFactory,
            ITripService tripService,
            IGeminiService geminiService,
            IHubContext<NotificationHub> hubContext)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _tripService = tripService;
            _geminiService = geminiService;
            _hubContext = hubContext;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                var sql = @"
                    SELECT 
                        u.Id,
                        u.FullName,
                        u.Email,
                        u.Role,
                        CASE WHEN u.IsActive = 1 THEN 'Active' ELSE 'Suspended' END AS Status,
                        (SELECT COUNT(*) FROM TripPlans WHERE UserId = u.Id) AS TripsCount,
                        CONVERT(VARCHAR(10), u.CreatedAt, 120) AS Joined,
                        up.ProfileImageUrl
                    FROM Users u
                    LEFT JOIN UserProfiles up ON u.Id = up.UserId
                    ORDER BY u.Id ASC";

                var users = await connection.QueryAsync<dynamic>(sql);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching users.", Details = ex.Message });
            }
        }

        [HttpPost("users/{id}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            try
            {
                var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(adminIdString) || !int.TryParse(adminIdString, out int adminId))
                {
                    return Unauthorized("Invalid administrator credentials.");
                }

                if (adminId == id)
                {
                    return BadRequest("You cannot suspend your own administrative session.");
                }

                using var connection = _dbConnectionFactory.CreateConnection();
                
                // Get user's current status and role
                var user = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Role, IsActive FROM Users WHERE Id = @Id", new { Id = id });

                if (user == null)
                {
                    return NotFound("User account not found.");
                }

                if (user.Role == "Admin")
                {
                    return BadRequest("Administrative roles must be demoted to standard user before suspension.");
                }

                bool newActiveStatus = !user.IsActive;
                await connection.ExecuteAsync(
                    "UPDATE Users SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                    new { IsActive = newActiveStatus, UpdatedAt = DateTime.Now, Id = id });

                return Ok(new { 
                    Message = $"User status updated successfully.", 
                    Status = newActiveStatus ? "Active" : "Suspended" 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while updating status.", Details = ex.Message });
            }
        }

        [HttpPost("users/{id}/toggle-role")]
        public async Task<IActionResult> ToggleUserRole(int id)
        {
            try
            {
                var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(adminIdString) || !int.TryParse(adminIdString, out int adminId))
                {
                    return Unauthorized("Invalid administrator credentials.");
                }

                using var connection = _dbConnectionFactory.CreateConnection();
                
                // Check if target user exists
                var user = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Id, Role, Email FROM Users WHERE Id = @Id", new { Id = id });

                if (user == null)
                {
                    return NotFound("User account not found.");
                }

                // Prevent safety issues
                if (id == adminId)
                {
                    return BadRequest("You cannot demote your own administrator status.");
                }

                // Prevent demoting the seed admin
                if (user.Email == "admin@yougo.com")
                {
                    return BadRequest("Root administrator cannot be demoted.");
                }

                string newRole = user.Role == "Admin" ? "User" : "Admin";
                
                await connection.ExecuteAsync(
                    "UPDATE Users SET Role = @Role, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                    new { Role = newRole, UpdatedAt = DateTime.Now, Id = id });

                return Ok(new { 
                    Message = $"User role toggled successfully.", 
                    Role = newRole 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while updating role.", Details = ex.Message });
            }
        }

        [HttpGet("trips")]
        public async Task<IActionResult> GetAllTrips()
        {
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                var sql = @"
                    SELECT 
                        tp.Id,
                        tp.Destination,
                        tp.StartingLocation AS Starting,
                        tp.Travelers,
                        CONVERT(VARCHAR(10), tp.StartDate, 120) AS StartDate,
                        CONVERT(VARCHAR(10), tp.EndDate, 120) AS EndDate,
                        CASE WHEN tp.AiPlanJson IS NOT NULL AND tp.AiPlanJson <> '' THEN 'Complete' ELSE 'Generating' END AS AiPlan,
                        u.Email AS UserEmail,
                        CASE 
                            WHEN tp.EndDate < GETDATE() THEN 'Completed'
                            WHEN tp.StartDate <= GETDATE() AND tp.EndDate >= GETDATE() THEN 'In Progress'
                            ELSE 'Planned'
                        END AS Status
                    FROM TripPlans tp
                    INNER JOIN Users u ON tp.UserId = u.Id
                    ORDER BY tp.Id DESC";

                var trips = await connection.QueryAsync<dynamic>(sql);
                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching system trips.", Details = ex.Message });
            }
        }

        [HttpGet("trips/{id}")]
        public async Task<IActionResult> GetTripDetails(int id)
        {
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                
                // 1. Fetch Trip Plan and User Details
                var tripSql = @"
                    SELECT 
                        tp.Id,
                        tp.UserId,
                        tp.Destination,
                        tp.StartingLocation,
                        tp.Travelers,
                        CONVERT(VARCHAR(10), tp.StartDate, 120) AS StartDate,
                        CONVERT(VARCHAR(10), tp.EndDate, 120) AS EndDate,
                        tp.TripDataJson,
                        tp.AiPlanJson,
                        tp.CreatedAt,
                        u.FullName AS UserFullName,
                        u.Email AS UserEmail,
                        up.ProfileImageUrl AS UserProfileImageUrl
                    FROM TripPlans tp
                    INNER JOIN Users u ON tp.UserId = u.Id
                    LEFT JOIN UserProfiles up ON u.Id = up.UserId
                    WHERE tp.Id = @Id";

                var trip = await connection.QueryFirstOrDefaultAsync<dynamic>(tripSql, new { Id = id });
                if (trip == null)
                {
                    return NotFound(new { Error = $"Trip with ID {id} not found." });
                }

                // 2. Fetch Active/Started Trip details
                var startedTripSql = @"
                    SELECT CheckedItemsJson, CurrentLocationIndex, UpdatedAt 
                    FROM StartedTrips 
                    WHERE TripId = @TripId";
                var startedTrip = await connection.QueryFirstOrDefaultAsync<dynamic>(startedTripSql, new { TripId = id });

                // 3. Fetch Lost Items details
                var lostItemsSql = @"
                    SELECT Id, ItemName, PredictedLocation, Reason, IsRecovered, RecoveredFrom, CreatedAt 
                    FROM LostItems 
                    WHERE TripId = @TripId
                    ORDER BY CreatedAt DESC";
                var lostItems = await connection.QueryAsync<dynamic>(lostItemsSql, new { TripId = id });

                // 4. Return combined details object
                return Ok(new
                {
                    Id = trip.Id,
                    UserId = trip.UserId,
                    Destination = trip.Destination,
                    StartingLocation = trip.StartingLocation,
                    Travelers = trip.Travelers,
                    StartDate = trip.StartDate,
                    EndDate = trip.EndDate,
                    TripDataJson = trip.TripDataJson,
                    AiPlanJson = trip.AiPlanJson,
                    CreatedAt = trip.CreatedAt,
                    User = new {
                        FullName = trip.UserFullName,
                        Email = trip.UserEmail,
                        ProfileImageUrl = trip.UserProfileImageUrl
                    },
                    StartedTrip = startedTrip,
                    LostItems = lostItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching trip details.", Details = ex.Message });
            }
        }
        [HttpGet("lost-items")]
        public async Task<IActionResult> GetAllLostItems()
        {
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                var sql = @"
                    SELECT 
                        li.Id,
                        li.TripId,
                        li.UserId,
                        li.ItemName,
                        li.PredictedLocation,
                        li.Reason,
                        li.IsRecovered,
                        li.RecoveredFrom,
                        CONVERT(VARCHAR(20), li.CreatedAt, 120) AS CreatedAt,
                        tp.Destination AS TripDestination,
                        u.FullName AS UserFullName,
                        u.Email AS UserEmail,
                        up.ProfileImageUrl AS UserProfileImageUrl
                    FROM LostItems li
                    INNER JOIN TripPlans tp ON li.TripId = tp.Id
                    INNER JOIN Users u ON li.UserId = u.Id
                    LEFT JOIN UserProfiles up ON u.Id = up.UserId
                    ORDER BY li.CreatedAt DESC";

                var items = await connection.QueryAsync<dynamic>(sql);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching lost items.", Details = ex.Message });
            }
        }

        [HttpPost("trips/{tripId}/manual-notify")]
        public async Task<IActionResult> SendManualNotification(int tripId, [FromBody] ManualNotifyRequestDto request)
        {
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                var tripSql = "SELECT * FROM TripPlans WHERE Id = @Id";
                var trip = await connection.QueryFirstOrDefaultAsync<dynamic>(tripSql, new { Id = tripId });
                if (trip == null)
                {
                    return NotFound(new { message = "Trip not found." });
                }

                int tripOwnerId = trip.UserId;
                string destination = trip.Destination;

                Console.WriteLine($"[DEBUG] Admin Manual Notify: TripId={tripId}, TripOwnerId={tripOwnerId}, Destination={destination}");

                string message;
                if (!string.IsNullOrEmpty(request.CustomMessage))
                {
                    message = request.CustomMessage;
                    Console.WriteLine($"[DEBUG] Using custom message: {message}");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Trip found. Generating AI message for {destination}...");
                    message = await _geminiService.GenerateNotificationMessageAsync(destination, request.ContextType, request.ActivityName);
                    Console.WriteLine($"[DEBUG] AI Message generated: {message.Substring(0, Math.Min(20, message.Length))}...");
                }

                var notification = new Notification
                {
                    UserId = tripOwnerId,
                    TripId = tripId,
                    Destination = destination,
                    Message = message,
                    Type = request.ContextType,
                    Timestamp = DateTime.Now,
                    IsRead = false
                };

                // Save to DB
                await _tripService.SaveNotificationAsync(notification);

                // Send real-time SignalR to trip owner
                await _hubContext.Clients.Group(tripOwnerId.ToString()).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    tripId = tripId,
                    destination = destination,
                    message = message,
                    type = request.ContextType,
                    timestamp = notification.Timestamp
                });

                return Ok(new { message = "Notification sent manually successfully", aiMessage = message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error sending manual notification.", details = ex.Message });
            }
        }

        [HttpGet("overview-stats")]
        public async Task<IActionResult> GetOverviewStats()
        {
            try
            {
                using var connection = _dbConnectionFactory.CreateConnection();
                
                // 1. Total Users
                var totalUsers = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users");
                
                // 2. Total Trips
                var totalTrips = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM TripPlans");
                
                // 3. Lost Items AI Recoveries
                var totalLostItems = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LostItems");
                var recoveredLostItems = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LostItems WHERE IsRecovered = 1");
                double recoveryRate = totalLostItems > 0 ? Math.Round((double)recoveredLostItems / totalLostItems * 100, 1) : 0;

                // 4. Live/In-Progress Trips (StartDate <= Today and EndDate >= Today)
                var inProgressTrips = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM TripPlans WHERE StartDate <= GETDATE() AND EndDate >= GETDATE()");

                // 5. Total Notifications Dispatched
                var totalNotifications = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Notifications");

                // 6. User growth this week (users created in the last 7 days)
                var usersThisWeek = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM Users WHERE CreatedAt >= DATEADD(day, -7, GETDATE())");

                // 7. Trips built this month (trips created in the last 30 days)
                var tripsThisMonth = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM TripPlans WHERE CreatedAt >= DATEADD(month, -1, GETDATE())");

                // 8. Recent Dynamic Activity Logs
                var recentUsersSql = @"
                    SELECT TOP 3 'User Registration' AS [Type], FullName + ' (' + Email + ')' AS [Detail], CreatedAt AS [Timestamp]
                    FROM Users ORDER BY CreatedAt DESC";
                var recentUsers = await connection.QueryAsync<dynamic>(recentUsersSql);

                var recentTripsSql = @"
                    SELECT TOP 3 'Trip Plan Created' AS [Type], 'Trip to ' + Destination AS [Detail], CreatedAt AS [Timestamp]
                    FROM TripPlans ORDER BY CreatedAt DESC";
                var recentTrips = await connection.QueryAsync<dynamic>(recentTripsSql);

                var recentLostItemsSql = @"
                    SELECT TOP 3 'Lost Item Registered' AS [Type], ItemName + ' (' + PredictedLocation + ')' AS [Detail], CreatedAt AS [Timestamp]
                    FROM LostItems ORDER BY CreatedAt DESC";
                var recentLostItems = await connection.QueryAsync<dynamic>(recentLostItemsSql);

                // 9. dynamic transactions calculation
                var dapperTransactions = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users") + 
                                         await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM TripPlans") * 3 + 
                                         await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LostItems") * 2;

                var allRecent = recentUsers.Concat(recentTrips).Concat(recentLostItems)
                    .OrderByDescending(a => (DateTime)a.Timestamp)
                    .Take(5)
                    .Select(a => new {
                        Timestamp = ((DateTime)a.Timestamp).ToString("hh:mm:ss tt"),
                        Type = a.Type,
                        Detail = a.Detail,
                        Status = "Success",
                        Duration = "142ms"
                    });

                return Ok(new
                {
                    TotalUsers = totalUsers,
                    TotalTrips = totalTrips,
                    TotalLostItems = totalLostItems,
                    RecoveredLostItems = recoveredLostItems,
                    RecoveryRate = recoveryRate,
                    InProgressTrips = inProgressTrips,
                    TotalNotifications = totalNotifications,
                    UsersThisWeek = usersThisWeek,
                    TripsThisMonth = tripsThisMonth,
                    RecentActivities = allRecent,
                    DapperTransactions = dapperTransactions
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An error occurred while fetching overview stats.", Details = ex.Message });
            }
        }
    }

    public class ManualNotifyRequestDto
    {
        [JsonPropertyName("contextType")]
        public string ContextType { get; set; } = string.Empty;

        [JsonPropertyName("activityName")]
        public string? ActivityName { get; set; }

        [JsonPropertyName("customMessage")]
        public string? CustomMessage { get; set; }
    }
}
