using chatable.Contacts.Requests;
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
    public class RequestController : Controller
    {
        [HttpPost]
        public async Task<IActionResult> SendRequest(FriendRequest friendRequest, [FromServices] Client client)
        {
            try
            {
                var user = GetCurrentUser();
                if (friendRequest.ReceiverId == null)
                {
                    throw new FormatException();
                }
                var requests = await client.From<Request>().Where(x => x.SenderId == user.UserName &&
                                                                    x.ReceiverId == friendRequest.ReceiverId).Get();
                //var result = requests.Models;
                //if (requests != null)
                //{
                //    throw new InvalidDataException();
                //}
                var request = new Request
                {
                    SenderId = user.UserName,
                    ReceiverId = friendRequest.ReceiverId,
                    Status = "Pending",
                    SentAt = DateTime.Now,
                };
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
            //catch (InvalidDataException)
            //{
            //    return BadRequest(new ApiResponse
            //    {
            //        Success = false,
            //        Message = "The request has already exist."
            //    });
            //}
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }

        }

        [HttpGet("Accept/{sender_id}")]
        [Authorize]
        public async Task<ActionResult> AcceptRequest(string sender_id, [FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var request = await client.From<Request>().Where(x => x.SenderId == sender_id &&
                                                                    x.ReceiverId == currentUser.UserName)
                                                                        .Set(x => x.Status, "Accepted").Update();
                if (sender_id == currentUser.UserName)
                {
                    throw new FormatException();
                }
                if (request == null)
                {
                    throw new KeyNotFoundException();
                }
                var friend1 = new Friend
                {
                    UserId = sender_id,
                    FriendId = currentUser.UserName
                };
                var friend2 = new Friend
                {
                    UserId = currentUser.UserName,
                    FriendId = sender_id
                };
                var updateFriend1 = await client.From<Friend>().Insert(friend1);
                var updateFriend2 = await client.From<Friend>().Insert(friend2);
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = $"You have become friend with {sender_id}"
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = "The request is not exist."
                });

            }
            catch (FormatException)
            {
                return StatusCode(403, new ApiResponse
                {
                    Success = false,
                    Message = "Access denied."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
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
