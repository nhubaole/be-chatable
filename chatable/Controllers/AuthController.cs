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
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;


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
                    CreatedAt = DateTime.Now,
                    Email = userRequest.Email
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
                //Response.Cookies.Append("access", token.AccessToken, new CookieOptions //Save the access token in the browser cookies, Key is "access"
                //{
                //    HttpOnly = true,
                //    SameSite = SameSiteMode.None,
                //    Secure = true
                //});
                //Response.Cookies.Append("refresh", token.RefreshToken, new CookieOptions //Save the refresh token in the browser cookies, Key is "refresh"
                //{
                //    HttpOnly = true,
                //    SameSite = SameSiteMode.None,
                //    Secure = true
                //});

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
                //Response.Cookies.Delete("access");
                //Response.Cookies.Delete("refresh");

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
                //Response.Cookies.Append("access", token.AccessToken, new CookieOptions //Save the access token in the browser cookies, Key is "access"
                //{
                //    HttpOnly = true,
                //    SameSite = SameSiteMode.None,
                //    Secure = true
                //});
                //Response.Cookies.Append("refresh", token.RefreshToken, new CookieOptions //Save the refresh token in the browser cookies, Key is "refresh"
                //{
                //    HttpOnly = true,
                //    SameSite = SameSiteMode.None,
                //    Secure = true
                //});

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
        public async Task<IActionResult> ForgotPassword(string email, [FromServices] Client client)
        {
            try
            {
                var response = await client.From<User>().Where(x => x.Email == email).Get();
                var user = response.Models.FirstOrDefault();
                if (user == null)
                {

                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Email not found."
                    });
                }
                var token = await TokenManager.GenerateToken(user, _configuration, client);
                var message = new EmailService.Message
                (
                    new string[] { email },
                    "Password Confirmation",
                    "\r\n<style>\r\n    \r\n    p {\r\n        font-family: SVN-Poppins;\r\n    }\r\n\r\n    td {\r\n        font-family: SVN-Poppins;\r\n    }\r\n\r\n    .detail-text {\r\n        color: #bbb;\r\n        font-family: SVN-Poppins;\r\n        font-size: 12px;\r\n        font-weight: 400;\r\n        font-style: normal;\r\n        letter-spacing: normal;\r\n        line-height: 20px;\r\n        text-transform: none;\r\n        text-align: center;\r\n        padding: 0;\r\n        margin: 0\r\n    }\r\n\r\n    .topBorder {\r\n        background-color:  #0091ff;\r\n        font-size: 1px;\r\n        line-height: 3px\r\n    }\r\n\r\n    /* .confirm-button {\r\n        padding: 12px 35px;\r\n        border-radius: 50px;\r\n        background-color:  #0091ff;\r\n    } */\r\n\r\n\r\n    .text-button {\r\n        color: #fff;\r\n        font-family: SVN-Poppins;\r\n        font-size: 13px;\r\n        font-weight: 600;\r\n        font-style: normal;\r\n        letter-spacing: 1px;\r\n        line-height: 20px;\r\n        text-transform: uppercase;\r\n        text-decoration: none;\r\n        display: block\r\n    }\r\n\r\n    .normal-text {\r\n        color: #000;\r\n        font-family: SVN-Poppins;\r\n        font-size: 28px;\r\n        font-weight: 500;\r\n        font-style: normal;\r\n        letter-spacing: normal;\r\n        line-height: 36px;\r\n        text-transform: none;\r\n        text-align: center;\r\n        padding: 0;\r\n        margin: 0\r\n    }\r\n\r\n    .img-avt {\r\n        width: 100%;\r\n        max-width: 200px;\r\n        height: auto;\r\n        display: block;\r\n        color: #f9f9f9;\r\n        border-radius: 100%;\r\n        border: 2px solid #fff;\r\n    }\r\n\r\n    .tableCard {\r\n        background-color: #fff;\r\n        border-color: #e5e5e5;\r\n        border-style: solid;\r\n        border-width: 0 1px 1px 1px;\r\n        margin-top: 20px\r\n    }\r\n\r\n    .link-text {\r\n        color: #bbb;\r\n        text-decoration: underline\r\n    }\r\n</style>\r\n<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"table-layout:fixed;background-color:#f9f9f9\"\r\n    id=\"bodyTable\">\r\n    <tbody>\r\n        <tr>\r\n            <td style=\"padding-right:10px;padding-left:10px;\" align=\"center\" valign=\"top\" id=\"bodyCell\">\r\n                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" class=\"wrapperBody\"\r\n                    style=\"max-width:600px\">\r\n                    <tbody>\r\n                        <tr>\r\n                            <td align=\"center\" valign=\"top\">\r\n                                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" class=\"tableCard\">\r\n                                    <tbody>\r\n                                        <tr>\r\n                                            <td class=\"topBorder\" height=\"3\">&nbsp;</td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"padding-top: 30px; padding-bottom: 10px;\" align=\"center\"\r\n                                                valign=\"middle\" class=\"emailLogo\">\r\n                                                <a href=\"#\" style=\"text-decoration:none\" target=\"_blank\">\r\n                                                    <img alt=\"\" border=\"0\" src=\"https://i.imgur.com/WDO6qit.png\"\r\n                                                        style=\"width:100%;max-width:150px;height:auto;display:block\"\r\n                                                        width=\"150\">\r\n                                                </a>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"padding-bottom: 20px;\" align=\"center\" valign=\"top\"\r\n                                                class=\"imgHero\">\r\n                                                <a href=\"#\" style=\"text-decoration:none\" target=\"_blank\">\r\n                                                    <img alt=\"\" border=\"0\" src=\"https://i.imgur.com/0R8FekN.png\" class=\"img-avt\"\r\n                                                        width=\"200px\">\r\n                                                </a>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"padding-bottom: 5px; padding-left: 20px; padding-right: 20px;\"\r\n                                                align=\"center\" valign=\"top\" class=\"mainTitle\">\r\n                                                <h2 class=\"normal-text\">Chào người dùng</h2>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <!-- Verify Email // -->\r\n                                        <tr>\r\n                                            <td style=\"padding-bottom: 10px; padding-left: 20px; padding-right: 20px;\"\r\n                                                align=\"center\" valign=\"top\" class=\"subTitle\">\r\n                                                <h4 class=\"detail-text\"\r\n                                                    style=\"font-size:16px;line-height:24px;padding:0;margin:0\">Xác nhận\r\n                                                    email của bạn</h4>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"padding-left:20px;padding-right:20px\" align=\"center\" valign=\"top\"\r\n                                                class=\"containtTable ui-sortable\">\r\n                                                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"\r\n                                                    class=\"tableDescription\" style=\"\">\r\n                                                    <tbody>\r\n                                                        <tr>\r\n                                                            <td style=\"padding-bottom: 20px;\" align=\"center\"\r\n                                                                valign=\"top\" class=\"description\">\r\n                                                                <p class=\"normal-text\"\r\n                                                                    style=\"font-size:14px;font-weight:400;font-style:normal\">\r\n                                                                    Để hoàn thành quá trình đặt lại mật khẩu, vui lòng\r\n                                                                    ấn Xác nhận Email. <br> Email sẽ có hiệu lực trong 5\r\n                                                                    phút</p>\r\n                                                            </td>\r\n                                                        </tr>\r\n                                                    </tbody>\r\n                                                </table>\r\n                                                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"\r\n                                                    class=\"tableButton\" style=\"\">\r\n                                                    <tbody>\r\n                                                        <tr>\r\n                                                            <td style=\"padding-top:20px;padding-bottom:20px\"\r\n                                                                align=\"center\" valign=\"top\">\r\n                                                                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\"\r\n                                                                    align=\"center\">\r\n                                                                    <tbody>\r\n                                                                        <tr>\r\n                                                                            <td align=\"center\" class=\"confirm-button\" style=\"padding: 12px 35px;\r\n                                                                            border-radius: 50px;\r\n                                                                            background-color:  #0091ff;\">\r\n                                                                                <a style=\"text-decoration: none;\" href=\"https://localhost:7154/password/recover?id=" + token.AccessToken + "\" target=\"_blank\"\r\n                                                                                    class=\"text-button\" style=\"color: #f9f9f9;\">Xác nhận\r\n                                                                                    Email</a>\r\n                                                                            </td>\r\n                                                                        </tr>\r\n                                                                    </tbody>\r\n                                                                </table>\r\n                                                            </td>\r\n                                                        </tr>\r\n                                                    </tbody>\r\n                                                </table>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"font-size:1px;line-height:1px\" height=\"20\">&nbsp;</td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td align=\"center\" valign=\"middle\" style=\"padding-bottom: 40px;\"\r\n                                                class=\"emailRegards\">\r\n                                                <!-- Image and Link // -->\r\n                                                <a href=\"#\" target=\"_blank\" style=\"text-decoration:none;\">\r\n                                                    <img mc:edit=\"signature\" src=\"https://i.imgur.com/HBcwSk7.png\" alt=\"\"\r\n                                                        width=\"150\" border=\"0\" style=\"width:100%;\r\n                                                    max-width:150px; height:auto; display:block;\">\r\n                                                </a>\r\n                                            </td>\r\n                                        </tr>\r\n                                    </tbody>\r\n                                </table>\r\n                                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" class=\"space\">\r\n                                    <tbody>\r\n                                        <tr>\r\n                                            <td style=\"font-size:1px;line-height:1px\" height=\"30\">&nbsp;</td>\r\n                                        </tr>\r\n                                    </tbody>\r\n                                </table>\r\n                            </td>\r\n                        </tr>\r\n                    </tbody>\r\n                </table>\r\n                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" class=\"wrapperFooter\"\r\n                    style=\"max-width:600px\">\r\n                    <tbody>\r\n                        <tr>\r\n                            <td align=\"center\" valign=\"top\">\r\n                                <table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" class=\"footer\">\r\n                                    <tbody>\r\n                                        <tr>\r\n                                            <td style=\"padding-top:10px;padding-bottom:10px;padding-left:10px;padding-right:10px\"\r\n                                                align=\"center\" valign=\"top\" class=\"socialLinks\">\r\n                                                <a href=\"#facebook-link\" style=\"display:inline-block\" target=\"_blank\"\r\n                                                    class=\"facebook\">\r\n                                                    <img alt=\"\" border=\"0\"\r\n                                                        src=\"http://email.aumfusion.com/vespro/img/social/light/facebook.png\"\r\n                                                        style=\"height:auto;width:100%;max-width:40px;margin-left:2px;margin-right:2px\"\r\n                                                        width=\"40\">\r\n                                                </a>\r\n                                                <a href=\"#twitter-link\" style=\"display: inline-block;\" target=\"_blank\"\r\n                                                    class=\"google\">\r\n                                                    <img alt=\"\" border=\"0\"\r\n                                                        src=\"http://email.aumfusion.com/vespro/img/social/light/google.png\"\r\n                                                        style=\"height:auto;width:100%;max-width:40px;margin-left:2px;margin-right:2px\"\r\n                                                        width=\"40\">\r\n                                                </a>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"padding: 10px 10px 5px;\" align=\"center\" valign=\"top\"\r\n                                                class=\"brandInfo\">\r\n                                                <p class=\"detail-text\">©&nbsp;Chatable Inc. | 123 Đ.ABC | TP.HCM,\r\n                                                    VietNam .</p>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"padding: 0px 10px 20px;\" align=\"center\" valign=\"top\"\r\n                                                class=\"footerLinks\">\r\n                                                <p class=\"detail-text\"> <a href=\"#\" class=\"link-text\"\r\n                                                        target=\"_blank\">Đến Chatable</a>&nbsp;|&nbsp; <a href=\"#\"\r\n                                                        class=\"link-text\" target=\"_blank\">Điều khoản người dùng\r\n                                                    </a>&nbsp;|&nbsp; <a href=\"#\" class=\"link-text\"\r\n                                                        target=\"_blank\">Chính sách riêng tư</a>\r\n                                                </p>\r\n                                            </td>\r\n                                        </tr>\r\n                                        <tr>\r\n                                            <td style=\"padding: 0px 10px 10px;\" align=\"center\" valign=\"top\"\r\n                                                class=\"footerEmailInfo\">\r\n                                                <p class=\"detail-text\">Nếu bạn có thắc mắc, vui lòng liên hệ chúng tôi\r\n                                                    tại <a href=\"#\" class=\"link-text\"\r\n                                                        target=\"_blank\">support@mail.com.</a>\r\n                                                </p>\r\n                                            </td>\r\n                                        </tr>\r\n\r\n                                        <tr>\r\n                                            <td style=\"font-size:1px;line-height:1px\" height=\"30\">&nbsp;</td>\r\n                                        </tr>\r\n                                    </tbody>\r\n                                </table>\r\n                            </td>\r\n                        </tr>\r\n                        <tr>\r\n                            <td style=\"font-size:1px;line-height:1px\" height=\"30\">&nbsp;</td>\r\n                        </tr>\r\n                    </tbody>\r\n                </table>\r\n            </td>\r\n        </tr>\r\n    </tbody>\r\n</table>\r\n\r\n"
                 );

                _emailSender.SendEmail(message);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Send request forgot password successfully.",
                    Data = token.AccessToken
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

        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest resetPassword, [FromServices] Client client)
        {
            try
            {

                var handler = new JwtSecurityTokenHandler();
                var jwtSecurityToken = handler.ReadToken(resetPassword.Token);
                var jsonToken = jwtSecurityToken as JwtSecurityToken;
                string userInToken = jsonToken.Claims.FirstOrDefault(x => x.Type == "nameid")?.Value;
                if (userInToken == null)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Access token invalid."
                    });
                }
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(resetPassword.Password);
                var updatedPasswordUser = await client
                                   .From<User>()
                                  .Where(x => x.UserName == userInToken)
                                  .Set(x => x.Password, passwordHash)
                                  .Update();

                return Ok(userInToken);
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
