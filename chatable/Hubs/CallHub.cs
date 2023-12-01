using chatable.Models;
using Microsoft.AspNetCore.SignalR;

namespace chatable.Hubs
{
    public class RoomCall
    {
        public static readonly Dictionary<string, Dictionary<string, PeerInfo>> peersInRoom = new();
    }

    public sealed class CallHub : Hub
    {

        public void JoinRoom(PeerInfo newPeer, string roomId)
        {

            // Check if the room exists in the dictionary
            if (!RoomCall.peersInRoom.ContainsKey(roomId))
            {
                RoomCall.peersInRoom[roomId] = new();
            }

            var room = RoomCall.peersInRoom[roomId];
            foreach (var peer in room)
            {
                if (peer.Key != Context.ConnectionId)
                {
                    Clients.Client(peer.Key!).SendAsync("NewPeerJoin", peer);
                }
            }

            RoomCall.peersInRoom[roomId].Add(Context.ConnectionId, newPeer);

        }

        private PeerInfo GetPeerInfo(string roomId)
        {
            var room = RoomCall.peersInRoom[roomId];
            return room[Context.ConnectionId];
        }

        public async Task SendCallTo(string receiverId, string typeCall, string roomId)
        {
            PeerInfo callerInfo = GetPeerInfo(roomId);

            await Clients.Client(receiverId).SendAsync("inviteCall", callerInfo, typeCall);
        }

        public void LeaveRoom(string roomId)
        {
            var room = RoomCall.peersInRoom[roomId];
            room.Remove(Context.ConnectionId);
        }
    }
}