using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Helper;
using chatable.Hubs;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Graph;
using Supabase;
using Supabase.Interfaces;
using System;
using System.Security.Claims;
using System.Security.Cryptography;

namespace chatable.Controllers
{
    [Route("/api/v1/[controller]")]
    [ApiController]
    public class GroupController : Controller
    {
        private readonly IHubContext<MessagesHub> _hubContext;

        public GroupController(IHubContext<MessagesHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Group>> CreateGroup(CreateGroupRequest request, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var res = await client.From<User>().Get();
                List<User> records = res.Models;
                foreach (var member in request.MemberList)
                {
                    if (!records.Any(x => x.UserName == member))
                    {
                        return NotFound(new ApiResponse
                        {
                            Success = false,
                            Message = "Member is not exist."
                        });
                    }
                }

                List<string> distinctMemberList = request.MemberList.Distinct().ToList();
                if (distinctMemberList.Count != request.MemberList.Count)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Must not duplicate member."
                    });
                }

                if (request.MemberList.Count <= 1)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Member in group must be greater than 2."
                    });
                }

                string randomId = Utils.RandomString(8);
                var Group = new Group
                {
                    GroupId = randomId,
                    GroupName = request.GroupName,
                    AdminId = currentUser.UserName,
                    CreatedAt = DateTime.Now,
                    Avatar = "https://goexjtmckylmpnrbxtcn.supabase.co/storage/v1/object/public/groups-avatar/group-default.png"
                };
                var responseGroup = await client.From<Group>().Insert(Group);

                var groupConnection = new GroupConnection()
                {
                    ConnectionId = Guid.NewGuid().ToString(),
                    GroupId = randomId,
                };
                var groupConnectionRes = await client.From<GroupConnection>().Insert(groupConnection);

                foreach (var member in request.MemberList)
                {
                    var userConnectionRes = await client.From<Connection>().Where(x => x.UserId == member).Get();
                    var userConnection = userConnectionRes.Models.FirstOrDefault();
                    await _hubContext.Groups.AddToGroupAsync(userConnection.ConnectionId, groupConnection.ConnectionId);
                    var GroupParticipants = new GroupParticipants
                    {
                        GroupId = randomId,
                        MemberId = member
                    };
                    var responseGroupPart = await client.From<GroupParticipants>().Insert(GroupParticipants);
                }
                var ownerConnectionRes = await client.From<Connection>().Where(x => x.UserId == currentUser.UserName).Get();
                var ownerConnection = ownerConnectionRes.Models.FirstOrDefault();
                await _hubContext.Groups.AddToGroupAsync(ownerConnection.ConnectionId, groupConnection.ConnectionId);
                var ownerParticipant = new GroupParticipants
                {
                    GroupId = randomId,
                    MemberId = currentUser.UserName
                };
                var responseOwnerPart = await client.From<GroupParticipants>().Insert(ownerParticipant);

                //create group conversation
                var newConversation = new Conversation
                {
                    ConversationId = randomId,
                    ConversationType = "Group",
                    LastMessage = Guid.Empty,
                    UnreadMessageCount = 0
                };
                await client
                    .From<Conversation>()
                    .Insert(newConversation);

                //alert
                var msg = new Message();
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
                    ConversationName = Group.GroupName,
                    ConversationAvatar = GetFileName(Group.Avatar)
                };
                await _hubContext
                        .Clients
                        .Group(groupConnection.ConnectionId)
                        .SendAsync("NewConversationReceived", newResConversation);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Create group successful.",
                    Data = randomId
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
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetGroups([FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<GroupParticipants>().Where(x => x.MemberId == currentUser.UserName).Get();
                var groups = response.Models;
                if (groups == null)
                {
                    throw new Exception();
                }
                List<Group> groupsList = new List<Group>();
                foreach (var group in groups)
                {
                    var res = await client.From<Group>().Where(x => x.GroupId == group.GroupId).Get();
                    var groupResponse = res.Models.FirstOrDefault();
                    groupResponse.Avatar = GetFileName(groupResponse.Avatar);
                    groupsList.Add(groupResponse);
                }
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Get all groups successful.",
                    Data = groupsList
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
        // [HttpGet("{GroupId}")]

        [HttpGet("{GroupId}/Member")]
        [Authorize]
        public async Task<IActionResult> GetMembers(string GroupId, [FromServices] Client client)
        {
            //var currentUser = GetCurrentUser();
            try
            {

                var response = await client.From<GroupParticipants>().Where(x => x.GroupId == GroupId).Get();
                var groupParticipants = response.Models;
                List<UserResponse> listMember = new List<UserResponse>();
                foreach (var participant in groupParticipants)
                {
                    var memberRes = await client.From<User>().Where(x => x.UserName == participant.MemberId).Get();
                    var member = memberRes.Models.FirstOrDefault();
                    var memberParticipants = new UserResponse
                    {
                        UserName = member.UserName,
                        FullName = member.FullName,
                        CreateAt = member.CreatedAt
                    };
                    listMember.Add(memberParticipants);
                }

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Get members in group {GroupId} successful.",
                    Data = listMember
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Data = ex.Message
                });
            }
        }
        [HttpPost("Member")]
        [Authorize]
        public async Task<ActionResult<GroupParticipants>> AddMemberToGroup(AddMemberRequest request, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var users = await client.From<User>().Get();
                List<User> records = users.Models;
                foreach (var member in request.MemberList)
                {
                    if (!records.Any(x => x.UserName == member))
                    {
                        return NotFound(new ApiResponse
                        {
                            Success = false,
                            Message = "Member is not exist."
                        });
                    }
                }

                var response = await client.From<GroupParticipants>().Where(x => x.GroupId == request.GroupId).Get();
                var participants = response.Models;
                foreach (var member in request.MemberList)
                {
                    if (participants.Any(x => x.MemberId == member))
                    {
                        return BadRequest(new ApiResponse
                        {
                            Success = false,
                            Message = $"Member was exsist in group {participants.FirstOrDefault().GroupId}"
                        });
                    }
                }

                foreach (var member in request.MemberList)
                {
                    var members = new GroupParticipants
                    {
                        GroupId = request.GroupId,
                        MemberId = member
                    };
                    var res = await client.From<GroupParticipants>().Insert(members);
                }
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Add member group {participants.FirstOrDefault().GroupId} successful."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Data = ex.Message
                });
            }
        }

        [HttpDelete("{GroupID}")]
        [Authorize]
        public async Task<ActionResult<Group>> DeteleGroup(string GroupID, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<Group>().Where(x => x.GroupId == GroupID).Get();
                var group = response.Models.FirstOrDefault();
                if (group is null)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = $"Group {GroupID} was not exist."
                    });
                }
                if (group.AdminId != currentUser.UserName)
                {
                    return StatusCode(403, new ApiResponse
                    {
                        Success = false,
                        Message = "Access denied."
                    });
                }

                await client.From<Group>().Where(x => x.GroupId == GroupID && x.AdminId == currentUser.UserName).Delete();
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Group {group.GroupName} was deleted."
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

        [HttpDelete("{GroupId}/Member")]
        [Authorize]
        public async Task<ActionResult<GroupParticipants>> RemoveMemberFromGroup(string GroupId, RemoveMemberGroup memberId, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<Group>().Where(x => x.GroupId == GroupId).Get();
                var group = response.Models.FirstOrDefault();
                if (group is null)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = $"Group {GroupId} was not exist."
                    });
                }
                if (group.AdminId != currentUser.UserName)
                {
                    return StatusCode(403, new ApiResponse
                    {
                        Success = false,
                        Message = "Access denied."
                    });
                }
                var res = await client.From<GroupParticipants>()
                    .Where(x => x.GroupId == GroupId).Select(x => new object[] { x.MemberId }).Get();
                var MemberId = res.Models;
                if (MemberId.ToString().Contains(memberId.MemberId))
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = $"Member was not exsist in group {GroupId}"
                    });
                }
                await client.From<GroupParticipants>().Where(x => x.GroupId == GroupId
                                                                        && x.MemberId == memberId.MemberId).Delete();

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Member {memberId.MemberId} was removed from group {GroupId}"
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
        [HttpDelete("{GroupId}/Leave")]
        [Authorize]
        public async Task<ActionResult<GroupParticipants>> LeaveGroup(string GroupId, [FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var response = await client.From<GroupParticipants>().Where(x => x.GroupId == GroupId).Get();
                var groupParticipants = response.Models;
                if (groupParticipants == null)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = $"Group {GroupId} does not exist."
                    });
                }
                await client.From<GroupParticipants>().Where(x => x.GroupId == GroupId
                                                                && x.MemberId == currentUser.UserName).Delete();
                var resGroup = await client.From<Group>().Where(x => x.GroupId == GroupId).Get();
                var group = resGroup.Models.FirstOrDefault();
                var res = await client.From<GroupParticipants>().Where(x => x.GroupId == GroupId).Get();
                var updatedParticipant = res.Models.FirstOrDefault();
                if (group.AdminId == currentUser.UserName)
                {
                    var updatedAdmin = await client.From<Group>().Where(x => x.GroupId == GroupId)
                        .Set(x => x.AdminId, updatedParticipant.MemberId).Update();
                }



                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"You have left the group {GroupId}"
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

        [HttpPost("UploadAvatar/{GroupId}")]
        [Authorize]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file, string GroupId, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<GroupParticipants>()
                    .Where(x => x.GroupId == GroupId).Get();
                var group = response.Models;
                if (group.FirstOrDefault() is null)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = $"Group {GroupId} does not exist."
                    });
                }
                if (!group.Any(x => x.MemberId == currentUser.UserName))
                {
                    return StatusCode(403, new ApiResponse
                    {
                        Success = false,
                        Message = $"You are not in group {GroupId}"
                    });
                }
                using var memoryStream = new MemoryStream();

                await file.CopyToAsync(memoryStream);

                var lastIndexOfDot = file.FileName.LastIndexOf('.');
                string extension = file.FileName.Substring(lastIndexOfDot + 1);
                string updatedTime = DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss");
                string fileName = $"group-{GroupId}?t={updatedTime}.{extension}";

                await client.Storage.From("groups-avatar").Upload(
                    memoryStream.ToArray(),
                    fileName,
                    new Supabase.Storage.FileOptions
                    {
                        CacheControl = "3600",
                        Upsert = true

                    });
                var avatarUrl = client.Storage.From("groups-avatar")
                                            .GetPublicUrl(fileName);
                var updateAvatar = await client
                                  .From<Group>()
                                  .Where(x => x.GroupId == GroupId)
                                  .Set(x => x.Avatar, avatarUrl)
                                  .Update();
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Upload image successful.",
                    Data = avatarUrl
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

        private async void StoreGroupConnection(Client client, string groupId)
        {
            var group = new GroupConnection()
            {
                ConnectionId = Guid.NewGuid().ToString(),
                GroupId = groupId,
            };
            var res = await client.From<GroupConnection>().Insert(group);
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
