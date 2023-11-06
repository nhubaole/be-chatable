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
    public class UserController : Controller
    {
        // Get user by id
        [HttpGet("{UserName}")]
        [Authorize]
        public async Task<ActionResult<User>> GetUserById(string UserName, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<User>().Where(x => x.UserName == UserName).Get();
                var user = response.Models.FirstOrDefault();

                if (user is null)
                {
                    throw new Exception();
                }
                if (currentUser.UserName != UserName)
                {
                    throw new FormatException();
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
            catch (FormatException)
            {
                return StatusCode(403, new ApiResponse
                {
                    Success = false,
                    Message = "Access denied."
                });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = $"User {UserName} not found"
                });
            }
        }


        [HttpGet]
        public async Task<ActionResult<User>> GetAllUsers(Client client)
        {
            try
            {
                var response = await client.From<User>().Get();
                var users = response.Models;

                if (users is null)
                {
                    throw new Exception();
                }
                List<UserResponse> result = new List<UserResponse>();
                foreach (var user in users)
                {
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
                    Message = "Users not exists."
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