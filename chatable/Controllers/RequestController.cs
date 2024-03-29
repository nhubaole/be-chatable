﻿using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Hubs;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MimeKit;
using Supabase;
using Supabase.Interfaces;
using System.Security.Claims;
using static MailKit.Net.Imap.ImapEvent;

namespace chatable.Controllers
{
    [Route("/api/v1/[controller]")]
    [ApiController]
    public class RequestController : Controller
    {
        private readonly IHubContext<MessagesHub> _hubContext;

        public RequestController(IHubContext<MessagesHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> SendRequest(FriendRequest friendRequest, [FromServices] Client client)
        {
            Console.WriteLine("SendFriendRequest");
            try
            {
                var currentUser = GetCurrentUser();
                if (friendRequest.ReceiverId == null)
                {
                    throw new FormatException();
                }
                var requests = await client.From<Request>().Where(x => x.SenderId == currentUser.UserName &&
                                                                    x.ReceiverId == friendRequest.ReceiverId).Get();
                var result = requests.Models;
                if (result.Count != 0)
                {
                    if (requests.Model?.Status == "Decline")
                    {
                        await client.From<Request>().Where(x => x.SenderId == currentUser.UserName &&
                                                                   x.ReceiverId == friendRequest.ReceiverId)
                                                                  .Set(x => x.Status, "Pending").Update();
                        return Ok(new ApiResponse
                        {
                            Success = true,
                            Message = "Send friend request successfully."
                        });
                    }
                    throw new InvalidDataException();


                }
                var request = new Request
                {
                    SenderId = currentUser.UserName,
                    ReceiverId = friendRequest.ReceiverId,
                    Status = "Pending",
                    SentAt = DateTime.Now,
                };


                //get sender info
                var userResponse = await client.From<User>().Where(x => x.UserName == request.SenderId).Get();
                var user = userResponse.Models.FirstOrDefault();

                //send realtime req
                var requestRes = new RequestResponse
                {
                    UserId = request.SenderId,
                    Status = request.Status,
                    SentAt = request.SentAt,
                    Avatar = GetFileName(user.Avatar),
                    Name = user.FullName
                };

                var connectionRes = await client.From<Connection>().Where(x => x.UserId == request.ReceiverId).Get();
                var receiver = connectionRes.Models.FirstOrDefault();
                await _hubContext
                        .Clients
                        .Client(receiver.ConnectionId)
                        .SendAsync("FriendRequestReceived", requestRes);

                var response = await client.From<Request>().Insert(request);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Send friend request successfully."
                });
            }
            catch (FormatException)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"User {friendRequest.ReceiverId} is not exist."
                });
            }
            catch (InvalidDataException)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "The request has already exist."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpGet("Accept/{SenderID}")]
        [Authorize]
        public async Task<ActionResult> AcceptRequest(string SenderID, [FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var request = await client.From<Request>().Where(x => x.SenderId == SenderID &&
                                                                    x.ReceiverId == currentUser.UserName)
                                                                   .Set(x => x.Status, "Accepted").Update();
                var res = request.Models.FirstOrDefault();

                if (res != null)
                {
                    if (SenderID == currentUser.UserName)
                    {
                        throw new FormatException();
                    }
                    //throw new KeyNotFoundException();
                    var friend1 = new Friend
                    {
                        UserId = SenderID,
                        FriendId = currentUser.UserName
                    };
                    var friend2 = new Friend
                    {
                        UserId = currentUser.UserName,
                        FriendId = SenderID
                    };
                    var updateFriend1 = await client.From<Friend>().Insert(friend1);
                    var updateFriend2 = await client.From<Friend>().Insert(friend2);


                    //get reveiver info
                    var userResponse = await client.From<User>().Where(x => x.UserName == res.ReceiverId).Get();
                    var user = userResponse.Models.FirstOrDefault();

                    //send realtime req
                    var requestRes = new RequestResponse
                    {
                        UserId = res.ReceiverId,
                        Status = res.Status,
                        SentAt = res.SentAt,
                        Avatar = GetFileName(user.Avatar),
                        Name = user.FullName
                    };

                    var connectionRes = await client.From<Connection>().Where(x => x.UserId == res.SenderId).Get();
                    var receiver = connectionRes.Models.FirstOrDefault();
                    await _hubContext
                            .Clients
                            .Client(receiver.ConnectionId)
                            .SendAsync("FriendRequestAccepted", requestRes);


                    //create conversation
                    String opt1 = res.SenderId + "_" + res.ReceiverId;
                    String opt2 = res.ReceiverId + "_" + res.SenderId;
                    var response = await client.From<Conversation>()
                                                      .Where(x => x.ConversationId == opt1 || x.ConversationId == opt2)
                                                      .Get();
                    var conversation = response.Models.FirstOrDefault();
                    if (conversation == null)
                    {
                        await client
                        .From<Conversation>()
                        .Insert(
                        new Conversation
                        {
                            ConversationId = $"{res.SenderId}_{res.ReceiverId}",
                            ConversationType = "Peer",
                            LastMessage = Guid.Empty,
                            UnreadMessageCount = 0
                        }
                        );

                        //alert
                        var msg = new Message();
                        var newResConversationSender = new ConversationResponse()
                        {
                            ConversationId = requestRes.UserId,
                            ConversationType = "Peer",
                            LastMessage = new MessageResponse()
                            {
                                MessageId = msg.MessageId,
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = requestRes.Name,
                            ConversationAvatar = requestRes.Avatar,
                            isFriend = true
                        };
                        await _hubContext
                                .Clients
                                .Client(receiver.ConnectionId)
                                .SendAsync("NewConversationReceived", newResConversationSender);

                        //alert receiver
                        var receiverConnectionRes = await client.From<Connection>().Where(x => x.UserId == res.ReceiverId).Get();
                        var receiverConnection = receiverConnectionRes.Models.FirstOrDefault();

                        //get sender info
                        var senderResponse = await client.From<User>().Where(x => x.UserName == res.SenderId).Get();
                        var sender = senderResponse.Models.FirstOrDefault();


                        var newResConversationReceiver = new ConversationResponse()
                        {
                            ConversationId = res.SenderId,
                            ConversationType = "Peer",
                            LastMessage = new MessageResponse()
                            {
                                MessageId = msg.MessageId,
                                SenderId = msg.SenderId,
                                Content = msg.Content,
                                MessageType = msg.MessageType,
                                SentAt = msg.SentAt,
                            },
                            ConversationName = sender.FullName,
                            ConversationAvatar = GetFileName(sender.Avatar),
                            isFriend = true
                        };
                        await _hubContext
                                .Clients
                                .Client(receiverConnection.ConnectionId)
                                .SendAsync("NewConversationReceived", newResConversationReceiver);
                    }

                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = $"You have become friend with {SenderID}"
                    });
                }
                throw new Exception();

            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = $"Some thing went wrong. {ex.Message}"
                });
            }

        }

        [HttpGet("Decline/{SenderID}")]
        [Authorize]
        public async Task<ActionResult> DeclineRequest(string SenderID, [FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var request = await client.From<Request>().Where(x => x.SenderId == SenderID &&
                                                                    x.ReceiverId == currentUser.UserName)
                                                                   .Get();
                if (request.Models.Count != 0)
                {
                    await client.From<Request>().Where(x => x.SenderId == SenderID &&
                                                                    x.ReceiverId == currentUser.UserName).Delete();
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = $"You have declined {SenderID}'s friend request."
                    });
                }
                throw new Exception();


            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
        [HttpGet("Remove/{ReceiverID}")]
        [Authorize]
        public async Task<ActionResult> RemoveRequest(string ReceiverID, [FromServices] Client client)
        {
            try
            {

                var currentUser = GetCurrentUser();
                var request = await client.From<Request>().Where(x => x.SenderId == currentUser.UserName &&
                                                x.ReceiverId == ReceiverID).Get();
                if (request.Models.Count != 0)
                {

                    await client.From<Request>().Where(x => x.SenderId == currentUser.UserName &&
                                                x.ReceiverId == ReceiverID).Delete();
                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = $"Removed the request to {ReceiverID}"
                    });
                }
                throw new Exception();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = $"The request was not exist.{ex.Message}"
                });
            }
        }

        [HttpGet("Received")]
        [Authorize]
        public async Task<ActionResult> GetReceivedRequests([FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var request = await client.From<Request>().Where(x => x.ReceiverId == currentUser.UserName).Get();
                List<RequestResponse> requestResponses = new List<RequestResponse>();

                if (request.Models.Count != 0)
                {
                    foreach (var response in request.Models)
                    {
                        //get sender info
                        var userResponse = await client.From<User>().Where(x => x.UserName == response.SenderId).Get();
                        var user = userResponse.Models.FirstOrDefault();

                        requestResponses.Add(new RequestResponse()
                        {
                            UserId = response.SenderId,
                            Status = response.Status,
                            SentAt = response.SentAt,
                            Avatar = GetFileName(user.Avatar),
                            Name = user.FullName,
                        });
                    }

                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = $"Get list received request successful",
                        Data = requestResponses
                    });
                }
                throw new Exception();
            }
            catch (Exception ex)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"The request was not exist."
                });
            }
        }

        [HttpGet("Sent")]
        [Authorize]
        public async Task<ActionResult> GetSentRequests([FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var request = await client.From<Request>().Where(x => x.SenderId == currentUser.UserName).Get();
                List<RequestResponse> requestResponses = new List<RequestResponse>();

                if (request.Models.Count != 0)
                {
                    foreach (var response in request.Models)
                    {
                        //get receiver info
                        var userResponse = await client.From<User>().Where(x => x.UserName == response.ReceiverId).Get();
                        var user = userResponse.Models.FirstOrDefault();

                        requestResponses.Add(new RequestResponse()
                        {
                            UserId = response.ReceiverId,
                            Status = response.Status,
                            SentAt = response.SentAt,
                            Avatar = GetFileName(user.Avatar),
                            Name = user.FullName,
                        });
                    }

                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Message = $"Get list sent request successful",
                        Data = requestResponses
                    });
                }
                throw new Exception();
            }
            catch (Exception ex)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"The request was not exist."
                });
            }
        }

        [HttpGet("Status/{UserID}")]
        [Authorize]
        public async Task<ActionResult> GetStatusRequest(string UserID, [FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var request = await client.From<Request>().Where(x => (x.SenderId == currentUser.UserName && x.ReceiverId == UserID)).Get();
                if(request.Models.Count == 0)
                {
                    request = await client.From<Request>().Where(x => (x.SenderId == UserID && x.ReceiverId == currentUser.UserName)).Get(); 
                }

                var res = request.Models.FirstOrDefault();

                if (res != null)
                {
                    var response = new StatusResponse()
                    {
                        SenderID = res.SenderId,
                        Status = res.Status,
                        ReceiverID = res.ReceiverId,
                    };

                    return Ok(new ApiResponse
                    {
                        Success = true,
                        Data = response,
                        Message = $"Get state request successful",
                    });
                }
                throw new Exception();
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = ex.Message
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
