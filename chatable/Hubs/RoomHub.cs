using chatable.Models;
using Microsoft.AspNetCore.SignalR;

namespace chatable.Hubs
{
    public class RoomCall
    {
        public static readonly Dictionary<string, Dictionary<string, PeerInfo>> peersInRoom = new();
    }

    public sealed class RoomHub : Hub
    {
        public void JoinRoom(PeerInfo newPeer, string roomId)
        {

            // Check if the room exists in the dictionary
            if (!RoomCall.peersInRoom.ContainsKey(roomId))
            {
                RoomCall.peersInRoom[roomId] = new();
            }

            var room = RoomCall.peersInRoom[roomId];

            Clients.Caller.SendAsync("ListPeerdInRoom", room);

            foreach (var peer in room)
            {
                if (peer.Key != Context.ConnectionId)
                {
                    Clients.Client(peer.Key!).SendAsync("NewPeerJoin", Context.ConnectionId, newPeer);
                }
            }

            RoomCall.peersInRoom[roomId].Add(Context.ConnectionId, newPeer);
            // Console.WriteLine(Context.ConnectionId);
            // Console.WriteLine(newPeer.Name + " đã tham gia phòng " + roomId);

        }

        public void LeaveRoom(string roomId)
        {
            var room = RoomCall.peersInRoom[roomId];
            room.Remove(Context.ConnectionId);
            // Console.WriteLine(Context.ConnectionId + " đã rời phòng " + roomId);

        }
    }
}