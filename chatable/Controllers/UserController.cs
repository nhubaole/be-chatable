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
    public class UserController : Controller
    {
        // Get user by id
        [HttpGet("{UserName}")]
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

                //var  = new UserResponse
                //{
                //    UserName = user.UserName,
                //    FullName = user.FullName,
                //    CreateAt = user.CreatedAt
                //};
                var userResponse = new ProfileUser
                {
                    UserName = currentUser.UserName,
                    FullName = currentUser.FullName,
                    Email = currentUser.Email,
                    DOB = currentUser.DOB,
                    Gender = currentUser.Gender,
                    AvatarUrl = client.Storage.From("users-avatar")
                    .GetPublicUrl($"user-{UserName}.png")
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
        [HttpGet("CurrentUser")]
        [Authorize]
        public async Task<ActionResult<User>> GetCurrentUserProfile([FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var response = await client.From<User>().Where(x => x.UserName == currentUser.UserName).Get();
                var currUser = response.Models.FirstOrDefault();
                var user = new ProfileUser
                {
                    UserName = currentUser.UserName,
                    FullName = currentUser.FullName,
                    Email = currentUser.Email,
                    DOB = currentUser.DOB,
                    Gender = currentUser.Gender,
                    AvatarUrl = client.Storage.From("users-avatar")
            .GetPublicUrl($"user-{currentUser.UserName}.png")
                };
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Successful.",
                    Data = user
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

        [HttpPut]
        [Authorize]
        public async Task<IActionResult> EditUser(EditUserRequest request, [FromServices] Client client)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var response = await client.From<User>().Where(x => x.UserName == currentUser.UserName).Get();
                var user = response.Models.FirstOrDefault();
                if (user is null)
                {
                    throw new Exception();
                }
                if (user.UserName != currentUser.UserName)
                {
                    return StatusCode(403, new ApiResponse
                    {
                        Success = false,
                        Message = "Access denied."
                    });
                }
                var update = await client.From<User>().Where(x => x.UserName == currentUser.UserName)
                                                            .Set(x => x.FullName, request.FullName)
                                                            .Set(x => x.DOB, request.DOB)
                                                            .Set(x => x.Gender, request.Gender).Update();
                var updatedUser = update.Models.FirstOrDefault();
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Edit successful.",
                    Data = updatedUser
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

        [HttpPost("UploadAvatar")]
        [Authorize]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file, [FromServices] Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {

                using var memoryStream = new MemoryStream();

                await file.CopyToAsync(memoryStream);

                var lastIndexOfDot = file.FileName.LastIndexOf('.');
                string extension = file.FileName.Substring(lastIndexOfDot + 1);

                await client.Storage.From("users-avatar").Upload(
                    memoryStream.ToArray(),
                    $"user-{currentUser.UserName}.{extension}",
                    new Supabase.Storage.FileOptions
                    {
                        CacheControl = "3600",
                        Upsert = false

                    });
                return Ok();
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