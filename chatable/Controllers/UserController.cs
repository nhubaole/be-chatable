using chatable.Contacts.Responses;
using chatable.Models;
using Microsoft.AspNetCore.Mvc;
using Supabase;

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
            try
            {
                var response = await client.From<User>().Where(x => x.UserName == UserName).Get();
                var user = response.Models.FirstOrDefault();
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
    }

}