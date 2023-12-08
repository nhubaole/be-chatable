using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Helper;
using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Graph;
using Supabase;
using System;
using System.Security.Claims;
using System.Security.Cryptography;

namespace chatable.Controllers
{
    [Route("/api/v1/[controller]")]
    [ApiController]
    public class GroupController : Controller
    {
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Group>> CreateGroup(CreateGroupRequest request, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {

                var res = await client.From<User>().Get();
                List<User> records = res.Models;
                List<string> distinctMemberList = request.MemberList.Distinct().ToList();
                if (distinctMemberList.Count != request.MemberList.Count)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Must not duplicate member."
                    });
                }
                bool isUserExist = true;
                foreach (var member in request.MemberList)
                {
                    isUserExist = records.Any(x => x.UserName == member);
                }
                if (request.MemberList.Count <= 1)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Member in group must be greater than 2."
                    });
                }
                if (!isUserExist)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = "Member is not exist."
                    });
                }
                string randomId = Utils.RandomString(8);
                var Group = new Group
                {
                    GroupId = randomId,
                    GroupName = request.GroupName,
                    AdminId = currentUser.UserName,
                    CreatedAt = DateTime.Now
                };
                var responseGroup = await client.From<Group>().Insert(Group);
                foreach (var member in request.MemberList)
                {
                    var GroupParticipants = new GroupParticipants
                    {
                        GroupId = randomId,
                        MemberId = member
                    };
                    var responseGroupPart = await client.From<GroupParticipants>().Insert(GroupParticipants);
                }
                var ownerParticipant = new GroupParticipants
                {
                    GroupId = randomId,
                    MemberId = currentUser.UserName
                };
                var responseOwnerPart = await client.From<GroupParticipants>().Insert(ownerParticipant);


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
                var response = await client.From<Group>().Where(x => x.AdminId == currentUser.UserName).Get();
                var groups = response.Models;
                if (groups == null)
                {
                    throw new Exception();
                }
                List<Group> groupsList = new List<Group>();
                foreach (var group in groups)
                {
                    var groupResponse = new Group
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        ConversationId = group.ConversationId,
                        AdminId = group.AdminId,
                        CreatedAt = group.CreatedAt
                    };
                    groupsList.Add(groupResponse);
                }
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Get all groups succesful.",
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
        [HttpPost("member")]
        [Authorize]
        public async Task<ActionResult<GroupParticipants>> AddMemberToGroup(AddMemberRequest request, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
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
