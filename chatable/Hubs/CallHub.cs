using chatable.Models;
using Microsoft.AspNetCore.SignalR;
using Supabase.Gotrue;
using System.Dynamic;

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
            Console.WriteLine(username + " đã connect vào CALL");
            foreach (var kvp in CallMapping.map)
			{
				Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
			}
		}

		public async Task SendCallTo(string receiverId, PeerInfo callerInfo, string typeCall, string roomId)
        {
            
            /*
			Cần phải kiểm tra xem receiverId có tồn tại trong CallMapping hay không.
			Phòng trường hợp khi call vào một group
			Sẽ có receiverId đang online và receiverId offline (offline thì không gọi cho người đó)
			Mà offline thì receiverId sẽ không có trong CallMapping
			*/
			if (CallMapping.map.ContainsKey(receiverId))
			{
                Console.WriteLine("SendCallTo " + receiverId);
                await Clients.Client(CallMapping.map[receiverId]).SendAsync("inviteCall", Context.ConnectionId, callerInfo, typeCall, roomId);
            } else
			{
				await Clients.Caller.SendAsync("receiverResponse", "offline");
            }
        }

        public async Task SendResponseCallTo(string callerConnectionId, string response)
        {
            // Console.WriteLine("SendResponseCallTo " + callerId);
            await Clients.Client(callerConnectionId).SendAsync("receiverResponse", response);
        }

		public async Task SendMissingCallMessageTo(string receiverId, string callerId) {
			if (CallMapping.map.ContainsKey(receiverId))
			{
				await Clients.Client(CallMapping.map[receiverId]).SendAsync("missingCall", callerId);
			}
		}

        public async Task SendFinishCallMessageTo(string callerId, string receiverId, string conversationId, string content)
        {
			Console.WriteLine("SendFinishCallMessageTo " + receiverId);
            if (CallMapping.map.ContainsKey(receiverId))
            {
                await Clients.Client(CallMapping.map[receiverId]).SendAsync("finishCallMessage", callerId, conversationId, content);
            }
        }

        public override Task OnDisconnectedAsync(Exception? exception)
		{
			foreach (var kvp in CallMapping.map)
			{
				if (kvp.Value == Context.ConnectionId)
				{
                    Console.WriteLine($"---> {kvp.Key} left the CALL");
                    CallMapping.map.Remove(kvp.Key);
					break;
                }
			}
			
			foreach (var kvp in CallMapping.map)
			{
				Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
			}
			return base.OnDisconnectedAsync(exception);
		}


	}
}
