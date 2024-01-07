using chatable.Models;
using Microsoft.AspNetCore.SignalR;
using Supabase.Gotrue;

namespace chatable.Hubs
{
	public class CallMapping
	{
		public static readonly Dictionary<string, string> map = new();
	}
	public sealed class CallHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            // Console.WriteLine(Context.ConnectionId + " đã connect vào Hub");
            return base.OnConnectedAsync();
        }

		public void AddMapping(string username)
		{
            CallMapping.map[username] = Context.ConnectionId;
			foreach (var kvp in CallMapping.map)
			{
				Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
			}
		}


		public async Task SendCallTo(string receiverId, PeerInfo callerInfo, string typeCall, string roomId)
        {
            // Console.WriteLine("SendCallTo " + receiverId);
            await Clients.Client(CallMapping.map[receiverId]).SendAsync("inviteCall", Context.ConnectionId, callerInfo, typeCall, roomId);
        }

        public async Task SendResponseCallTo(string callerId, string response)
        {
            // Console.WriteLine("SendResponseCallTo " + callerId);
            await Clients.Client(callerId).SendAsync("receiverResponse", response);
        }

        public async Task SendMissingCallMessageTo(string receiverId, string callerId) {
            // Console.Wri teLine("SendMissingCallMessageTo " + receiverId);
            await Clients.Client(receiverId).SendAsync("missingCall", callerId);
        }

		public override Task OnDisconnectedAsync(Exception? exception)
		{
			CallMapping.map.Remove(Context.ConnectionId);
			//Console.WriteLine($"---> {Context.ConnectionId} left the call-hub");
			//foreach (var kvp in CallMapping.map)
			//{
			//	Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
			//}
			return base.OnDisconnectedAsync(exception);
		}
	}
}