using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManagementAPI.DTOs;
using TaskManagementAPI.Services;

namespace TaskManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                var user = await _authService.RegisterAsync(registerDto);
                return CreatedAtAction(nameof(Register), new { id = user.Id }, user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new {message = ex.Message});
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error during registration", details = ex.Message });
            }
        }

        // DTO banaya fir configure ki, fir services banayi uske baad ham controller me jaa rahe he fir finally program.cs me
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var ipAddress = GetIpAddress();
                var response = await _authService.LoginAsync(loginDto, ipAddress);
                SetRefreshTokenCookie(response.RefreshToken); // Optiional: store in HTTP-only cookie
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message});
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {message = "An error occured during login", details = ex.Message});
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            try
            {
                var ipAddress = GetIpAddress();
                var response = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress);
                SetRefreshTokenCookie(response.RefreshToken); // Optional: update cookie
                return Ok(response); // response me accessToken, refreshToken aur donoki expiry Jaayegi
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new {message = ex.Message});
            }
            catch(Exception ex)
            {
                return StatusCode(500, new { message = "An error occured while refreshing token", details = ex.Message});
            }
        }


        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequestDto request)
        {
            try
            {
                var ipAddress = GetIpAddress();
                var success = await _authService.RevokeTokenAsync(request.RefreshToken, ipAddress);

                if(!success)
                {
                    return BadRequest(new { message = "Token is invalid or already revoked" });
                }
                return Ok(new { message = "Token revoked successfuly" });

            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occured while revoking token", details = ex.Message });
            }
        }


        private string GetIpAddress()
        {
            // Get IP address from request
            if(Request.Headers.ContainsKey("X-Forwaded-For"))
            {
                return Request.Headers["X-Forwaded-For"].ToString();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true, // Cannot be accessed by Javascript
                Expires = DateTime.UtcNow.AddDays(7), // Match refresh token expiry
                Secure = true, // Only sent over HTTPS
                SameSite = SameSiteMode.Strict, // CSRF protection
                Path = "/api/auth/refresh-token" // Only sent to refresh endpoint
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

    }
}
