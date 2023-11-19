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
    [Authorize]
    public class MessagesHub : Hub
    {
        private readonly UserManager<User> _userManager;
        private readonly Client _supabaseClient;
        private IHttpContextAccessor _httpContextAccessor;


        public MessagesHub(UserManager<User> userManager, Client supabaseClient)
        {
            _userManager = userManager;
            _supabaseClient = supabaseClient;
        }

        public async Task SendPeerMessage(String toUsername, MessageRequest messageRequest)
        {
            var sender = await _userManager.FindByIdAsync(Context.ConnectionId);
            var receiver = await _supabaseClient.From<User>().Where(x => x.UserName == sender.UserName).Get();
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            var user = GetCurrentUser();
            if (user is null) return;
            Console.WriteLine($"---> {user.UserName} just joined the chat");
            var response = await _supabaseClient
                .From<Connection>()
                .Insert(
                new Connection { UserId = user.UserName, ConnectionId = Context.ConnectionId }
                );

        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            var user = await _userManager.FindByIdAsync(Context.ConnectionId);
            if (user is null) return;
            Console.WriteLine($"---> {user.UserName} left the chat right now");
            await _supabaseClient.From<Connection>().Where(e => e.UserId == user.UserName).Delete();
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
