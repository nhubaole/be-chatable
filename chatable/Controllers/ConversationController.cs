using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Hubs;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Org.BouncyCastle.Asn1.Ocsp;
using Supabase;
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

                if (conversations != null)
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
                        if (msg == null)
                        {
                            msg = new Message() { };
                        }

                        //check isFriend
                        bool isFriend;
                        var friendResponse = await client.From<Friend>().Where(x => x.FriendId == conversationId && x.UserId == currentUser.UserName).Get();
                        var friend = friendResponse.Models.FirstOrDefault();
                        if (friend != null)
                        {
                            isFriend = true;
                        }
                        else
                        {
                            isFriend = false;
                        }

                        conversationResponses.Add(new ConversationResponse()
                        {
                            ConversationId = conversationId,
                            ConversationType = conversation.ConversationType,
                            LastMessage = new MessageResponse()
                            {
                                MessageId = msg.MessageId,
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = user.FullName,
                            ConversationAvatar = GetFileName(user.Avatar),
                            isFriend = isFriend,
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

                        if (groupConversation != null)
                        {
                            //get group info
                            var res = await client.From<Group>().Where(x => x.GroupId == group.GroupId).Get();
                            var groupResponse = res.Models.FirstOrDefault();

                            //get last message
                            var msgResponse = await client.From<Message>().Where(x => x.MessageId == groupConversation.LastMessage).Get();
                            var msg = msgResponse.Models.FirstOrDefault();
                            if (msg == null)
                            {
                                msg = new Message() { };
                            }

                            conversationResponses.Add(new ConversationResponse()
                            {
                                ConversationId = groupConversation.ConversationId,
                                ConversationType = groupConversation.ConversationType,
                                LastMessage = new MessageResponse()
                                {
                                    MessageId = msg.MessageId,
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
                if (Type == "Peer")
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
                    if (msg == null)
                    {
                        msg = new Message() { };
                    }

                    //check isFriend
                    bool isFriend = false;
                    var friendResponse = await client.From<Friend>().Where(x => x.FriendId == ConversationId && x.UserId == currentUser.UserName).Get();
                    var friend = friendResponse.Models.FirstOrDefault();
                    if (friend != null)
                    {
                        isFriend = true;
                    }
                    else
                    {
                        isFriend = false;
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
                                MessageId = msg.MessageId,
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = user.FullName,
                            ConversationAvatar = GetFileName(user.Avatar),
                            isFriend = isFriend,
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
                    if (msg == null)
                    {
                        msg = new Message() { };
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
                                MessageId = msg.MessageId,
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
                List<Message> messages;

                if (Type == "Peer")
                {
                    var response = await client.From<Message>().Where(x => x.ConversationId.Contains($"{ConversationId}_{currentUser.UserName}") || x.ConversationId.Contains($"{currentUser.UserName}_{ConversationId}")).Get();
                    messages = response.Models;
                }
                else
                {
                    var response = await client.From<Message>().Where(x => x.ConversationId == ConversationId).Get();
                    messages = response.Models;
                }

                if (messages is null)
                {
                    throw new Exception();
                }

                foreach (var message in messages)
                {
                    //get reaction list
                    var msgId = message.MessageId.ToString();
                    var reactionRes = await client.From<Reaction>().Where(x => x.MessageId == msgId).Get();
                    var reactions = reactionRes.Models;

                    //get sender info
                    var userResponse = await client.From<User>().Where(x => x.UserName == message.SenderId).Get();
                    var user = userResponse.Models.FirstOrDefault();

                    List<ReactionResponse> reactionsResponse = new List<ReactionResponse>();
                    foreach (var reaction in reactions)
                    {
                        //get sender reaction
                        var reacterResponse = await client.From<User>().Where(x => x.UserName == reaction.SenderId).Get();
                        var reacter = reacterResponse.Models.FirstOrDefault();

                        reactionsResponse.Add(new ReactionResponse()
                        {
                            SenderId = reaction.SenderId,
                            MessageId = reaction.MessageId,
                            Type = reaction.Type,
                            ConversationId = message.ConversationId,
                            SenderName = reacter.FullName,
                            SenderAvatar = reacter.Avatar
                        });
                    }

                    listMessage.Add(new MessageResponse()
                    {
                        MessageId = message.MessageId,
                        Content = message.Content,
                        MessageType = message.MessageType,
                        SenderId = message.SenderId,
                        SentAt = message.SentAt,
                        SenderName = user.FullName,
                        SenderAvatar = GetFileName(user.Avatar),
                        Reactions = reactionsResponse
                    });
                }
                listMessage.Sort((m1, m2) => m1.SentAt.CompareTo(m2.SentAt));

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

        [HttpPost("{Type}/{ConversationId}/Messages/{MessageType}")]
        [Authorize]
        public async Task<ActionResult> AddFileMessage(string Type, string ConversationId, string MessageType, [FromForm] IFormFile file, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);

                var lastIndexOfDot = file.FileName.LastIndexOf('.');
                string extension = file.FileName.Substring(lastIndexOfDot + 1);
                string updatedTime = DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss");
                string fileName = $"message-{currentUser.UserName}-{updatedTime}.{extension}";
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

                if(Type == "Peer")
                {
                    //send peer msg
                    var senderId = currentUser.UserName;
                    var response = await client.From<Connection>().Where(x => x.UserId == ConversationId).Get();
                    var receiver = response.Models.FirstOrDefault();

                    var msgId = Guid.NewGuid();

                    var messageRes = new MessageResponse()
                    {
                        MessageId = msgId,
                        SenderId = senderId,
                        MessageType = MessageType,
                        Content = fileUrl,
                        SentAt = DateTime.UtcNow,
                    };

                    await _hubContext
                        .Clients
                        .Client(receiver.ConnectionId)
                        .SendAsync("MessageReceived", messageRes);

                    String conversationId;

                    //get conversationId
                    String opt1 = senderId + "_" + receiver.UserId;
                    String opt2 = receiver.UserId + "_" + senderId;
                    var responseCon = await client.From<Conversation>()
                                                      .Where(x => x.ConversationId == opt1 || x.ConversationId == opt2)
                                                      .Get();
                    var conversation = responseCon.Models.FirstOrDefault();
                    if (conversation != null)
                    {
                        conversationId = conversation.ConversationId;
                    }
                    else
                    {
                        var newConversation = new Conversation
                        {
                            ConversationId = $"{senderId}_{receiver.UserId}",
                            ConversationType = "Peer",
                            LastMessage = Guid.Empty,
                            UnreadMessageCount = 0
                        };
                        await client
                        .From<Conversation>()
                        .Insert(newConversation);

                        //alert
                        var msg = new Message();
                        var userResponse = await client.From<User>().Where(x => x.UserName == senderId).Get();
                        User user = userResponse.Models.FirstOrDefault();
                        var newResConversation = new ConversationResponse()
                        {
                            ConversationId = user.UserName,
                            ConversationType = newConversation.ConversationType,
                            LastMessage = new MessageResponse()
                            {
                                MessageId = msg.MessageId,
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = user.FullName,
                            ConversationAvatar = GetFileName(user.Avatar)
                        };
                        await _hubContext
                                .Clients
                                .Client(receiver.ConnectionId)
                                .SendAsync("NewConversationReceived", newResConversation);

                        conversationId = $"{senderId}_{receiver.UserId}";
                    }


                    //add message
                    Message message = new Message()
                    {
                        MessageId = msgId,
                        SenderId = messageRes.SenderId,
                        MessageType = messageRes.MessageType,
                        Content = messageRes.Content,
                        SentAt = messageRes.SentAt,
                        ConversationId = conversationId
                    };

                    var responseInsertMsg = await client.From<Message>().Insert(message);
                    var insertedMsg = responseInsertMsg.Models.FirstOrDefault();

                    //update conversation
                    //await client.From<Conversation>()
                    //  .Where(x => x.ConversationId == conversationId)
                    //  .Set(x => x.LastMessage, insertedMsg.MessageId)
                    //  .Update();
                }
                else
                {
                    //send group msg via hub
                    var senderId = currentUser.UserName;
                    var response = await client.From<GroupConnection>().Where(x => x.GroupId == ConversationId).Get();
                    var receiver = response.Models.FirstOrDefault();

                    var msgId = Guid.NewGuid();

                    var messageRes = new MessageResponse()
                    {
                        MessageId = msgId,
                        SenderId = senderId,
                        MessageType = MessageType,
                        Content = fileUrl,
                        SentAt = DateTime.UtcNow,
                        GroupId = ConversationId
                    };

                    var currConnectionRes = await client.From<Connection>().Where(x => x.UserId == currentUser.UserName).Get();
                    var currConnection = currConnectionRes.Models.FirstOrDefault();

                    await _hubContext.Clients
                    .GroupExcept(receiver.ConnectionId, currConnection.ConnectionId)
                    .SendAsync("MessageReceivedFromGroup", messageRes);

                    //insert group conversation if not exist
                    var responseCon = await client.From<Conversation>()
                                                  .Where(x => x.ConversationId == ConversationId)
                                                  .Get();
                    var conversation = responseCon.Models.FirstOrDefault();
                    if (conversation == null)
                    {
                        var newConversation = new Conversation
                        {
                            ConversationId = ConversationId,
                            ConversationType = "Group",
                            LastMessage = Guid.Empty,
                            UnreadMessageCount = 0
                        };
                        await client
                        .From<Conversation>()
                        .Insert(newConversation);

                        //alert
                        var msg = new Message();
                        var groupResponse = await client.From<Group>().Where(x => x.GroupId == ConversationId).Get();
                        Group group = groupResponse.Models.FirstOrDefault();
                        var newResConversation = new ConversationResponse()
                        {
                            ConversationId = newConversation.ConversationId,
                            ConversationType = newConversation.ConversationType,
                            LastMessage = new MessageResponse()
                            {
                                MessageId = msg.MessageId,
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = group.GroupName,
                            ConversationAvatar = GetFileName(group.Avatar)
                        };
                        await _hubContext
                                .Clients
                                .GroupExcept(receiver.ConnectionId, currConnection.ConnectionId)
                                .SendAsync("NewConversationReceived", newResConversation);

                    }

                    //set last message
                    Message message = new Message()
                    {
                        MessageId = msgId,
                        SenderId = messageRes.SenderId,
                        MessageType = messageRes.MessageType,
                        Content = messageRes.Content,
                        SentAt = messageRes.SentAt,
                        ConversationId = ConversationId
                    };

                    var responseInsertMsg = await client.From<Message>().Insert(message);
                    var insertedMsg = responseInsertMsg.Models.FirstOrDefault();

                    //await client.From<Conversation>()
                    //               .Where(x => x.ConversationId == ConversationId)
                    //               .Set(x => x.LastMessage, insertedMsg.MessageId)
                    //               .Update();
                }
                
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Sent file successfully.",
                    Data = fileUrl
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
