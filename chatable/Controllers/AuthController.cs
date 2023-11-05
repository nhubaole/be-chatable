
using BCrypt.Net;
using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Supabase;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace chatable.Controllers
{

    [Route("/api/v1/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;
        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterRequest userRequest, [FromServices] Client client)
        {
            try
            {
                var res = await client.From<User>().Get();
                List<User> records = res.Models;
                foreach (var record in records)
                {
                    if (record.UserName == userRequest.UserName)
                    {
                        throw new Exception();
                    }
                }
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(userRequest.Password);
                var user = new User
                {
                    UserName = userRequest.UserName,
                    FullName = userRequest.FullName,
                    Password = passwordHash,
                    CreatedAt = DateTime.Now
                };

                var response = await client.From<User>().Insert(user);
                var newUser = response.Models.First();
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Register successfully",
                    Data = newUser
                });
            }
            catch (Exception)
            {
                return BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "User already exists"
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginRequest userRequest, [FromServices] Client client)
        {
            try
            {
                var userResponse = await client.From<User>().Where(x => x.UserName == userRequest.UserName).Get();
                var user = userResponse.Models.FirstOrDefault();
                if (user is null)
                {
                    throw new Exception();
                }

                bool isValidPassword = BCrypt.Net.BCrypt.Verify(userRequest.Password, user.Password);

                if (!isValidPassword)
                {
                    throw new UnauthorizedAccessException();
                }
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Login successfully",
                    Data = GenerateToken(user)
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new ApiResponse { Success = false, Message = "Password is wrong" });
            }
            catch (Exception)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Message = "User name does not exists"
                });
            }

        }
        private string GenerateToken(User user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var secretKey = _configuration.GetValue<string>("AppSettings:SecretKey");
            var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);

            var tokenDescription = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("UserName", user.UserName),
                    
                    //new Claim("FullName", user.FullName),

                    //new Claim("TokenId", Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "owner")
                }),
                Expires = DateTime.UtcNow.AddMinutes(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(secretKeyBytes), SecurityAlgorithms.HmacSha512Signature)
            };
            var token = jwtTokenHandler.CreateToken(tokenDescription);
            return jwtTokenHandler.WriteToken(token);
        }

    }

}
