using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Hubs;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Supabase;
using Supabase.Interfaces;
using System.Security.Claims;

namespace chatable.Controllers
{
    [Route("/api/v1/[controller]")]
    [ApiController]
    public class ConversationController : Controller
    {

        private readonly IHubContext<MessagesHub> _hubContext;

        public ConversationController(IHubContext<MessagesHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetAllConversation([FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                List<ConversationResponse> conversationResponses = new List<ConversationResponse>();

                var response = await client.From<Conversation>().Where(x => x.ConversationId.Contains($"_{currentUser.UserName}") || x.ConversationId.Contains($"{currentUser.UserName}_")).Get();
                var conversations = response.Models;

                if(conversations != null)
                {
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

                        //get user infor
                        var userResponse = await client.From<User>().Where(x => x.UserName == conversationId).Get();
                        var user = userResponse.Models.FirstOrDefault();

                        //get last message
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
                            },
                            ConversationName = user.FullName,
                            ConversationAvatar = GetFileName(user.Avatar)
                        });
                    }
                }

                var resGroups = await client.From<GroupParticipants>().Where(x => x.MemberId == currentUser.UserName).Get();
                var groups = resGroups.Models;
                if (groups != null)
                {
                    foreach (var group in groups)
                    {
                        var responseGroupCons = await client.From<Conversation>().Where(x => x.ConversationId == group.GroupId).Get();
                        var groupConversation = responseGroupCons.Models.FirstOrDefault();

                        if(groupConversation != null)
                        {
                            //get group info
                            var res = await client.From<Group>().Where(x => x.GroupId == group.GroupId).Get();
                            var groupResponse = res.Models.FirstOrDefault();

                            //get last message
                            var msgResponse = await client.From<Message>().Where(x => x.MessageId == groupConversation.LastMessage).Get();
                            var msg = msgResponse.Models.FirstOrDefault();
                            if (msg is null)
                            {
                                throw new Exception();
                            }

                            conversationResponses.Add(new ConversationResponse()
                            {
                                ConversationId = groupConversation.ConversationId,
                                ConversationType = groupConversation.ConversationType,
                                LastMessage = new MessageResponse()
                                {
                                    SenderId = msg.SenderId,
                                    Content = msg.Content,
                                    MessageType = msg.MessageType,
                                    SentAt = msg.SentAt,
                                },
                                ConversationName = groupResponse.GroupName,
                                ConversationAvatar = GetFileName(groupResponse.Avatar)
                            });
                        }
                    }
                }

                var sortedConversations = conversationResponses.OrderByDescending(conversation => conversation.LastMessage.SentAt).ToList();

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Successfuly",
                    Data = sortedConversations
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

        [HttpGet("{Type}/{ConversationId}")]
        [Authorize]
        public async Task<ActionResult> GetConversationById(string Type, string ConversationId, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
               if(Type == "Peer")
               {
                    var response = await client.From<Conversation>().Where(x => x.ConversationId.Contains($"{ConversationId}_{currentUser.UserName}") || x.ConversationId.Contains($"{currentUser.UserName}_{ConversationId}")).Get();
                    var conversation = response.Models.FirstOrDefault();

                    if (conversation is null)
                    {
                        throw new Exception();
                    }

                    //get user 
                    var userResponse = await client.From<User>().Where(x => x.UserName == ConversationId).Get();
                    User user = userResponse.Models.FirstOrDefault();

                    //get last msg
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
                            ConversationId = ConversationId,
                            ConversationType = conversation.ConversationType,
                            LastMessage = new MessageResponse()
                            {
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = user.FullName,
                            ConversationAvatar = GetFileName(user.Avatar)

                        }
                    });
               }
               else
                {
                    var response = await client.From<Conversation>().Where(x => x.ConversationId == ConversationId).Get();
                    var conversation = response.Models.FirstOrDefault();

                    if (conversation is null)
                    {
                        throw new Exception();
                    }

                    //get group infor
                    var groupResponse = await client.From<Group>().Where(x => x.GroupId == ConversationId).Get();
                    Group group = groupResponse.Models.FirstOrDefault();

                    //get last msg
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
                            ConversationId = ConversationId,
                            ConversationType = conversation.ConversationType,
                            LastMessage = new MessageResponse()
                            {
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = group.GroupName,
                            ConversationAvatar = GetFileName(group.Avatar)
                        }
                    });
                }
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

        [HttpGet("{Type}/{ConversationId}/Messages")]
        [Authorize]
        public async Task<ActionResult> GetMessagesFromAConversation(string Type, string ConversationId, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var listMessage = new List<MessageResponse>();

                if (Type == "Peer")
                {
                    var response = await client.From<Message>().Where(x => x.ConversationId.Contains($"{ConversationId}_{currentUser.UserName}") || x.ConversationId.Contains($"{currentUser.UserName}_{ConversationId}")).Get();
                    var messages = response.Models;

                    if (messages is null || messages.Count == 0)
                    {
                        throw new Exception();
                    }

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
                }
                else
                {
                    var response = await client.From<Message>().Where(x => x.ConversationId == ConversationId).Get();
                    var messages = response.Models;

                    if (messages is null || messages.Count == 0)
                    {
                        throw new Exception();
                    }

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

        [HttpPost("Messages")]
        [Authorize]
        public async Task<ActionResult> AddFileMessage([FromForm] IFormFile file, [FromBody] AddMsgRequest request, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);

                var lastIndexOfDot = file.FileName.LastIndexOf('.');
                string extension = file.FileName.Substring(lastIndexOfDot + 1);
                string updatedTime = DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss");
                string fileName = $"message-{currentUser.UserName}?t={updatedTime}.{extension}";
                await client.Storage.From("message-file").Upload(
                    memoryStream.ToArray(),
                   fileName,
                    new Supabase.Storage.FileOptions
                    {
                        CacheControl = "3600",
                        Upsert = true
                    });
                var fileUrl = client.Storage.From("message-file")
                                            .GetPublicUrl(fileName);
                
                Uri uri = new Uri(fileUrl);
                string path = uri.AbsolutePath;

                await _hubContext.Clients.All.SendAsync("SendPeerMessage", request.ConversationId, request.ConversationType, path);
                

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Sent file successfully.",
                    Data = Path.GetFileName(path)
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

        private string GetFileName(string url)
        {
            if (url != null)
            {
                int lastSlashIndex = url.LastIndexOf('/');
                string avatarFileName = url.Substring(lastSlashIndex + 1);
                return avatarFileName;
            }
            return null;
        }
    }
}
