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
        private int dd;

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
                bool isFriend;

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

                //check isFriend
                var friendResponse = await client.From<Friend>().Where(x => x.FriendId == UserName && x.UserId == currentUser.UserName).Get();
                var friend = friendResponse.Models.FirstOrDefault();
                if(friend != null)
                {
                    isFriend = true;
                }
                else
                {
                    isFriend = false;
                }

                var userResponse = new ProfileUser
                {
                    UserName = user.UserName,
                    FullName = user.FullName,
                    Email = user.Email,
                    DOB = user.DOB,
                    Gender = user.Gender,
                    AvatarUrl = user.Avatar,
                    isFriend = isFriend
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
        [Authorize]
        public async Task<ActionResult<User>> GetAllUsers(Client client)
        {
            var currentUser = GetCurrentUser();
            try
            {
                var response = await client.From<User>().Get();
                var users = response.Models;

                if (users is null)
                {
                    throw new Exception();
                }
                List<ProfileUser> result = new List<ProfileUser>();
                foreach (var user in users)
                {
                    if(user.UserName != currentUser.UserName)
                    {
                        bool isFriend;
                        //check isFriend
                        var friendResponse = await client.From<Friend>().Where(x => x.FriendId == user.UserName && x.UserId == currentUser.UserName).Get();
                        var friend = friendResponse.Models.FirstOrDefault();
                        if (friend != null)
                        {
                            isFriend = true;
                        }
                        else
                        {
                            isFriend = false;
                        }

                        var userResponse = new ProfileUser
                        {
                            UserName = user.UserName,
                            FullName = user.FullName,
                            Email = user.Email,
                            DOB = user.DOB,
                            Gender = user.Gender,
                            AvatarUrl = user.Avatar,
                            isFriend = isFriend
                        };

                        result.Add(userResponse);
                    }
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
                    UserName = currUser.UserName,
                    FullName = currUser.FullName,
                    Email = currUser.Email,
                    DOB = currUser.DOB,
                    Gender = currUser.Gender,
                    AvatarUrl = currUser.Avatar,
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
                string updatedTime = DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss");
                string fileName = $"user-{currentUser.UserName}?t={updatedTime}.{extension}";
                await client.Storage.From("users-avatar").Upload(
                    memoryStream.ToArray(),
                   fileName,
                    new Supabase.Storage.FileOptions
                    {
                        CacheControl = "3600",
                        Upsert = true

                    });
                var avatarUrl = client.Storage.From("users-avatar")
                                            .GetPublicUrl(fileName);
                var updateAvatar = await client
                                  .From<User>()
                                  .Where(x => x.UserName == currentUser.UserName)
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
    }

}