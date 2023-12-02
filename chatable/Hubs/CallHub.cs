using chatable.Models;
using Microsoft.AspNetCore.SignalR;

namespace chatable.Hubs
{
    public sealed class CallHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            // Console.WriteLine(Context.ConnectionId + " đã connect vào Hub");
            return base.OnConnectedAsync();
        }
        public async Task SendCallTo(string receiverId, PeerInfo callerInfo, string typeCall, string roomId)
        {
            // Console.WriteLine("SendCallTo " + receiverId);
            await Clients.Client(receiverId).SendAsync("inviteCall", Context.ConnectionId, callerInfo, typeCall, roomId);
        }

        public async Task SendResponseCallTo(string callerId, string response)
        {
            // Console.WriteLine("SendResponseCallTo " + callerId);
            await Clients.Client(callerId).SendAsync("receiverResponse", response);
        }
    }
}