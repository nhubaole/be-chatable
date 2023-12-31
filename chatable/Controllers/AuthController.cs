using BCrypt.Net;
using chatable.Contacts.Requests;
using chatable.Contacts.Responses;
using chatable.Helper;
using chatable.Models;
using chatable.Services;
using EmailService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using NETCore.MailKit.Core;
using Supabase;
using System.Configuration;
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
        private readonly IEmailSender _emailSender;
        public AuthController(IConfiguration configuration, IEmailSender emailSender)
        {
            _configuration = configuration;
            _emailSender = emailSender;
        }

        [HttpPost("Register")]
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
                    Avatar = userRequest.Avatar,
                    DOB = userRequest.DOB,
                    Gender = userRequest.Gender,
                    Password = passwordHash,
                    LastTimeOnl = DateTime.Now,
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

        [HttpPost("Login")]
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
                var token = await TokenManager.GenerateToken(user, _configuration, client);
                Response.Cookies.Append("jwt", token.AccessToken, new CookieOptions //Save the JWT in the browser cookies, Key is "jwt"
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.None,
                    Secure = true
                });

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Login successfully",
                    Data = token
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

        [HttpPost("Logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                Response.Cookies.Delete("jwt");

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Logged out."
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

        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshToken(Token tokenRequest, [FromServices] Client client)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var secretKey = _configuration.GetValue<string>("AppSettings:SecretKey");
            var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            var param = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
                ClockSkew = TimeSpan.Zero,
                ValidateLifetime = false // do not check token expired
            };
            try
            {
                // check format token
                var tokenValid = jwtTokenHandler.ValidateToken(tokenRequest.AccessToken, param, out var validatedToken);
                // check algorithm encode token
                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512, StringComparison.InvariantCultureIgnoreCase);

                    if (!result)
                    {
                        return BadRequest(new ApiResponse
                        {
                            Success = false,
                            Message = "Invalid token"
                        });
                    }
                }

                //check token expired?
                var utcExpireDate = long.Parse(tokenValid.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp).Value);
                var expireDate = Utils.ConvertToDateTime(utcExpireDate);
                if (expireDate > DateTime.UtcNow)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Access token has not expired yet."
                    });
                }

                var response = await client.From<RefreshToken>().Where(x => x.Token == tokenRequest.RefreshToken).Get();
                var storedToken = response.Models.FirstOrDefault();
                if (storedToken is null)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Refresh token does not exist."
                    });
                }
                if (storedToken.IsUsed)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Refresh token has been used."
                    });
                }
                if (storedToken.IsRevoked)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Refresh token has been revoked."
                    });
                }
                // check access token id match jwt id in DB
                var jti = tokenValid.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti).Value;
                if (storedToken.JwtId != jti)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Token does not match."
                    });
                }

                // Update token  is used
                storedToken.IsRevoked = true;
                storedToken.IsUsed = true;
                await client.From<RefreshToken>().Update(storedToken);

                var res = await client.From<User>().Where(x => x.UserName == storedToken.UserId).Get();
                var currentUser = res.Models.FirstOrDefault();
                var token = await TokenManager.GenerateToken(currentUser, _configuration, client);
                Response.Cookies.Append("jwt", token.AccessToken, new CookieOptions //Save the JWT in the browser cookies, Key is "jwt"
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.None,
                    Secure = true
                });

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Refresh token successful.",
                    Data = token
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

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            try
            {

                var message = new EmailService.Message
                (
                    new string[] { email },
                    "test",
                    "this is content"
                );
                _emailSender.SendEmail(message);
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
