using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace YuGo.Hubs
{
    public class NotificationHub : Hub
    {
        // Hub used for pushing real-time notifications to users
        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }
    }
}
