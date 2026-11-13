using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Hubs;

[Authorize(Roles = "ADMIN")]
public sealed class OccupancyHub : Hub
{
    // Hub cho admin theo dõi phòng đang sử dụng real-time
    // Client sẽ subscribe vào hub này và nhận update khi có thay đổi
    
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    // Method để client yêu cầu refresh data
    public async Task RequestUpdate()
    {
        await Clients.Caller.SendAsync("UpdateRequested");
    }
}
