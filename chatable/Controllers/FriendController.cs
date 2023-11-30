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
    public class FriendController : Controller
    {
        [HttpGet("{UserName}")]
        [Authorize]
        public async Task<ActionResult<Friend>> GetFriendById(string UserName, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<Friend>().Where(x => x.FriendId == UserName && x.UserId == currentUser.UserName).Get();
                var friend = response.Models.FirstOrDefault();

                if (friend is null)
                {
                    throw new Exception();
                }

                var uResponse = await client.From<User>().Where(x => x.UserName == friend.FriendId).Get();
                var user = uResponse.Models.FirstOrDefault();

                if (user is null)
                {
                    throw new Exception();
                }

                var userResponse = new UserResponse
                {
                    UserName = user.UserName,
                    FullName = user.FullName,
                    CreateAt = user.CreatedAt
                };

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Successfuly",
                    Data = userResponse
                });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"Friend {UserName} not found"
                });
            }
        }


        [HttpGet]
        [Authorize]
        public async Task<ActionResult<User>> GetAllFriends(Client client)
        {
            var currentUser = GetCurrentUser();

            try
            {
                var response = await client.From<Friend>().Where(x => x.UserId == currentUser.UserName).Get();
                var friends = response.Models;

                if (friends is null)
                {
                    throw new Exception();
                }

                List<UserResponse> result = new List<UserResponse>();
                foreach (var friend in friends)
                {
                    var uResponse = await client.From<User>().Where(x => x.UserName == friend.FriendId).Get();
                    var user = uResponse.Models.FirstOrDefault();

                    var usersResponse = new UserResponse
                    {
                        UserName = user.UserName,
                        FullName = user.FullName,
                        CreateAt = user.CreatedAt
                    };
                    result.Add(usersResponse);
                }

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Successfully",
                    Data = result
                });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = "Friends not exists."
                });
            }
        }

        [HttpDelete("{UserName}")]
        [Authorize]
        public async Task<ActionResult<Friend>> DeleteFriend(string UserName, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<Friend>()
                                            .Where(x => x.FriendId == UserName && x.UserId == currentUser.UserName)
                                            .Get();
                var friend = response.Models.FirstOrDefault();

                if (friend is null)
                {
                    throw new Exception();
                }
                await client.From<Friend>().Where(x => x.UserId == friend.UserId && x.FriendId == friend.FriendId).Delete();
                await client.From<Friend>().Where(x => x.FriendId == friend.UserId && x.UserId == friend.FriendId).Delete();

                await client.From<Request>()
                            .Where(x => x.SenderId == UserName && x.ReceiverId == currentUser.UserName)
                            .Delete();
                await client.From<Request>()
                            .Where(x => x.ReceiverId == UserName && x.SenderId == currentUser.UserName)
                            .Delete();

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"Friend {UserName} has been deleted successfully",
                    Data = ""
                });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"Friend {UserName} not found"
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
