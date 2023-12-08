using chatable.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Supabase;
using Microsoft.Build.Graph;
using chatable.Contacts.Responses;
using chatable.Contacts.Requests;
using System;
using chatable.Helper;
using System.Security.Cryptography;
using System.Security.Claims;

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
                if(distinctMemberList.Count != request.MemberList.Count) {
                    return BadRequest(new ApiResponse { 
                        Success = false,
                        Message = "Must not duplicate member."
                    });
                }
                bool isUserExist = true;
                foreach (var member in request.MemberList)
                {
                    isUserExist = records.Any(x=>x.UserName == member);
                }
                if(request.MemberList.Count <= 1) {
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
            catch(Exception ex)
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
