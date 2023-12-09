using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.IdentityModel.Tokens;
using Supabase;
using System.Security.Claims;

namespace chatable.Hubs
{
    [Authorize]
    public sealed class MessagesHub : Hub
    {
        private readonly Client _supabaseClient;
        private IHttpContextAccessor _httpContextAccessor;


        public MessagesHub(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }

        public async Task SendPeerMessage(String toUsername, String messageType, String content)
        {
            var senderId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var response = await _supabaseClient.From<Connection>().Where(x => x.UserId == toUsername).Get();
            var receiver = response.Models.FirstOrDefault();

            var messageRes = new MessageResponse()
            {
                SenderId = senderId,
                MessageType = messageType,
                Content = content,
                SentAt = DateTime.UtcNow,
            };

            await Clients
            .Client(receiver.ConnectionId)
            .SendAsync("MessageReceived", messageRes);

            String conversationId = await getConversationId(senderId, receiver.UserId);

            Message message = new Message()
            {
                SenderId = messageRes.SenderId,
                MessageType = messageRes.MessageType,
                Content = messageRes.Content,
                SentAt = messageRes.SentAt,
                ConversationId = conversationId
            };

            var responseInsertMsg = await _supabaseClient.From<Message>().Insert(message);
            var insertedMsg = responseInsertMsg.Models.FirstOrDefault();

            updateConversation(conversationId, insertedMsg);
        }

        public async Task<String> getConversationId(String senderId, String receiverId)
        {
            try
            {
                String opt1 = senderId + "_" + receiverId;
                String opt2 = receiverId + "_" + senderId;
                var response = await _supabaseClient.From<Conversation>()
                                                  .Where(x => x.ConversationId == opt1 || x.ConversationId == opt2)
                                                  .Get();
                var conversation = response.Models.FirstOrDefault();
                if (conversation != null)
                {
                    return conversation.ConversationId;
                }
                else
                {
                    await _supabaseClient
                    .From<Conversation>()
                    .Insert(
                    new Conversation
                    {
                        ConversationId = $"{senderId}_{receiverId}",
                        ConversationType = "Peer",
                        LastMessage = 1,
                        UnreadMessageCount = 0
                    }
                    );
                    return $"{senderId}_{receiverId}";
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong hàm getConversationId: {ex.Message}");
                throw;
            }
        }

        public async void updateConversation(String conversationId, Message lastMessage)
        {
            await _supabaseClient.From<Conversation>()
                   .Where(x => x.ConversationId == conversationId)
                   .Set(x => x.LastMessage, lastMessage.MessageId)
                   .Update();
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return;
            Console.WriteLine($"---> {userId} just joined the chat");

            var response = await _supabaseClient
                .From<Connection>()
                .Insert(
                new Connection { UserId = userId, ConnectionId = Context.ConnectionId }
                );

        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            //var username = Context.User.Identity.Name;
            if (userId is null) return;
            Console.WriteLine($"---> {userId} left the chat right now");
            await _supabaseClient.From<Connection>().Where(e => e.UserId == userId).Delete();
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
