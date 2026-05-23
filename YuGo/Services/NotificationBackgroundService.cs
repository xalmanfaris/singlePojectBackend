using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using YuGo.Hubs;
using YuGo.Interfaces;
using YuGo.Models;

namespace YuGo.Services
{
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationBackgroundService> _logger;
        private readonly HashSet<string> _processedNotifications = new();

        public NotificationBackgroundService(
            IServiceProvider serviceProvider,
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var tripService = scope.ServiceProvider.GetRequiredService<ITripService>();
                        var aiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();
                        var trips = await tripService.GetAllTripsForBackgroundServiceAsync();

                        var now = DateTime.Now;

                        foreach (var trip in trips)
                        {
                            stoppingToken.ThrowIfCancellationRequested();
                            await ProcessTripNotifications(trip, now, aiService, tripService, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Notification Background Service was canceled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in NotificationBackgroundService");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessTripNotifications(TripPlan trip, DateTime now, IGeminiService aiService, ITripService tripService, CancellationToken ct)
        {
            if (!trip.StartDate.HasValue) return;

            var startDate = trip.StartDate.Value;
            var timeToStart = startDate - now;

           
            if (timeToStart.TotalHours <= 24 && timeToStart.TotalHours > 23)
            {
                await SendAiNotification(trip, "OneDayBefore", null, aiService, tripService);
            }

            
            if (timeToStart.TotalHours <= 1 && timeToStart.TotalHours > 0)
            {
               
                await SendAiNotification(trip, "OneHourBefore", null, aiService, tripService, 15);
            }

           
            if (timeToStart.TotalMinutes <= 2 && timeToStart.TotalMinutes >= -2)
            {
                
                await SendAiNotification(trip, "StartTripPrompt", null, aiService, tripService, 1440);
            }

            
            if (startDate.Date == now.Date && now.Hour == 8 && now.Minute == 0)
            {
                await SendAiNotification(trip, "TripStart", null, aiService, tripService, 1440);
            }
            
            if (!string.IsNullOrEmpty(trip.AiPlanJson))
            {
                try
                {
                    var node = JsonNode.Parse(trip.AiPlanJson);
                    var itinerary = node?["itinerary"]?.AsArray();
                    if (itinerary != null)
                    {
                        foreach (var day in itinerary)
                        {
                            
                            var dayNum = day?["day"]?.GetValue<int>() ?? 1;
                            var actualDay = startDate.AddDays(dayNum - 1);

                            if (actualDay.Date == now.Date)
                            {
                                var activities = day?["activities"]?.AsArray();
                                if (activities != null)
                                {
                                    foreach (var act in activities)
                                    {
                                        var timeStr = act?["time"]?.ToString();
                                        if (IsItTimeForActivity(timeStr, now))
                                        {
                                            await SendAiNotification(trip, "ActivityStart", act?["activity"]?.ToString(), aiService, tripService);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private bool IsItTimeForActivity(string? timeStr, DateTime now)
        {
            if (string.IsNullOrEmpty(timeStr)) return false;
           
            var currentStr = now.ToString("hh:mm tt");
            var currentStr24 = now.ToString("HH:mm");
            return timeStr.Contains(currentStr) || timeStr.Contains(currentStr24);
        }

        private async Task SendAiNotification(TripPlan trip, string contextType, string? activityName, IGeminiService aiService, ITripService tripService, int repeatThresholdMinutes = 1440)
        {
            try
            {
                var alreadySent = await tripService.NotificationExistsRecentAsync(trip.Id, contextType, repeatThresholdMinutes);
                if (alreadySent) return;

                string notificationKey = $"{trip.Id}-{contextType}-{activityName ?? "trip"}-{DateTime.Now:yyyyMMddHHmm}";
                
                if (_processedNotifications.Contains(notificationKey)) return;

                var message = await aiService.GenerateNotificationMessageAsync(trip.Destination, contextType, activityName);
                
                var notification = new Notification
                {
                    UserId = trip.UserId,
                    TripId = trip.Id,
                    Destination = trip.Destination,
                    Message = message,
                    Type = contextType,
                    Timestamp = DateTime.Now,
                    IsRead = false
                };

                await tripService.SaveNotificationAsync(notification);

                await _hubContext.Clients.Group(trip.UserId.ToString()).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id, 
                    tripId = trip.Id,
                    destination = trip.Destination,
                    message = message,
                    type = contextType,
                    timestamp = notification.Timestamp
                });

                _processedNotifications.Add(notificationKey);
                
                if (_processedNotifications.Count > 1000) _processedNotifications.Clear();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SendAiNotification was canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred in SendAiNotification for trip {trip.Id}");
            }
        }
    }
}
