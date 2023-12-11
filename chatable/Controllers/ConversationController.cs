using chatable.Contacts.Responses;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supabase;
using System.Security.Claims;

namespace chatable.Controllers
{
    [Route("/api/v1/[controller]")]
    [ApiController]
    public class ConversationController : Controller
    {
        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetAllConversation([FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<Conversation>().Where(x => x.ConversationId.Contains($"_{currentUser.UserName}") || x.ConversationId.Contains($"{currentUser.UserName}_")).Get();
                var conversations = response.Models;

                if (conversations is null)
                {
                    throw new Exception();
                }

                List<ConversationResponse> conversationResponses = new List<ConversationResponse>();
                foreach (var conversation in conversations)
                {
                    string conversationId = conversation.ConversationId;
                    if (conversationId.Contains($"_{currentUser.UserName}"))
                    {
                        conversationId = conversationId.Replace($"_{currentUser.UserName}", "");
                    }
                    if (conversationId.Contains($"{currentUser.UserName}_"))
                    {
                        conversationId = conversationId.Replace($"{currentUser.UserName}_", "");
                    }

                    var msgResponse = await client.From<Message>().Where(x => x.MessageId == conversation.LastMessage).Get();
                    var msg = msgResponse.Models.FirstOrDefault();
                    if (msg is null)
                    {
                        throw new Exception();
                    }
                    conversationResponses.Add(new ConversationResponse()
                    {
                        ConversationId = conversationId,
                        ConversationType = conversation.ConversationType,
                        LastMessage = new MessageResponse()
                        {
                            SenderId = msg.SenderId,
                            Content = msg.Content,
                            MessageType = msg.MessageType,
                            SentAt = msg.SentAt,
                        }
                    });
                }

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Successfuly",
                    Data = conversationResponses
                });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"Conversation not found"
                });
            }
        }

        [HttpGet("{UserName}")]
        [Authorize]
        public async Task<ActionResult> GetConversationById(string UserName, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<Conversation>().Where(x => x.ConversationId.Contains($"{UserName}_{currentUser.UserName}") || x.ConversationId.Contains($"{currentUser.UserName}_{UserName}")).Get();
                var conversation = response.Models.FirstOrDefault();

                if (conversation is null)
                {
                    throw new Exception();
                }

                var msgResponse = await client.From<Message>().Where(x => x.MessageId == conversation.LastMessage).Get();
                var msg = msgResponse.Models.FirstOrDefault();
                if (msg is null)
                {
                    throw new Exception();
                }

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Successfuly",
                    Data = new ConversationResponse()
                    {
                        ConversationId = UserName,
                        ConversationType = conversation.ConversationType,
                        LastMessage = new MessageResponse()
                        {
                            SenderId = msg.SenderId,
                            Content = msg.Content,
                            MessageType = msg.MessageType,
                            SentAt = msg.SentAt,
                        }
                    }
                });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"Conversation not found"
                });
            }
        }

        [HttpGet("{UserName}/Messages")]
        [Authorize]
        public async Task<ActionResult> GetMessagesFromAConversation(string UserName, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<Message>().Where(x => x.ConversationId.Contains($"{UserName}_{currentUser.UserName}") || x.ConversationId.Contains($"{currentUser.UserName}_{UserName}")).Get();
                var messages = response.Models;

                if (messages is null || messages.Count == 0)
                {
                    throw new Exception();
                }

                var listMessage = new List<MessageResponse>();
                foreach (var message in messages)
                {
                    listMessage.Add(new MessageResponse()
                    {
                        Content = message.Content,
                        MessageType = message.MessageType,
                        SenderId = message.SenderId,
                        SentAt = message.SentAt,
                    });
                }

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Successfuly",
                    Data = listMessage
                });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"Message not found"
                });
            }
        }

        private User GetCurrentUser()
        {
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
