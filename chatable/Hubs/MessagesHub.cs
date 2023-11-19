using chatable.Contacts.Requests;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Experimental.ProjectCache;
using Supabase;
using System.Security.Claims;

namespace chatable.Hubs
{
    public class MessagesHub : Hub
    {
        private readonly Client _supabaseClient;
        private IHttpContextAccessor _httpContextAccessor;


        public MessagesHub(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }

        public async Task SendPeerMessage(String toUsername, MessageRequest messageRequest)
        {
            //var receiver = await _supabaseClient.From<User>().Where(x => x.UserName == sender.UserName).Get();
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = Context.User.Identity.Name;
            if (username is null) return;
            Console.WriteLine($"---> {username} just joined the chat");

            var response = await _supabaseClient
                .From<Connection>()
                .Insert(
                new Connection { UserId = username, ConnectionId = Context.ConnectionId }
                );

        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            //if (user is null) return;
            //Console.WriteLine($"---> {user.UserName} left the chat right now");
            //await _supabaseClient.From<Connection>().Where(e => e.UserId == user.UserName).Delete();
        }

        private User GetCurrentUser()
        {
            var HttpContext = _httpContextAccessor.HttpContext;
            var identity = HttpContext.User.Identity as ClaimsIdentity;

            if (identity != null)
            {
                var userClaims = identity.Claims;

                return new User
                {
                    UserName = userClaims.FirstOrDefault(o => o.Type == ClaimTypes.NameIdentifier)?.Value,
                    FullName = userClaims.FirstOrDefault(o => o.Type == ClaimTypes.Name)?.Value,
                };
            }
            return null;
        }

    }
}
