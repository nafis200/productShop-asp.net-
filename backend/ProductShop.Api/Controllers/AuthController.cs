using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductShop.Api.Auth;
using ProductShop.Application.Dtos;
using ProductShop.Application.Interfaces;

namespace ProductShop.Api.Controllers
{
    // [controller] is replaced by the class name without "Controller"  ->  /api/auth
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // POST /api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]                                 // no token needed (you don't have one yet)
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var authResponse = await _authService.RegisterAsync(registerDto);

            if (authResponse is null)
            {
                return Conflict(new { message = $"A user with email '{registerDto.Email}' already exists." });
            }

            return Created("/api/auth/me", authResponse);
        }

        // POST /api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var authResponse = await _authService.LoginAsync(loginDto);

            if (authResponse is null)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            return Ok(authResponse);
        }

        // GET /api/auth/me   (needs a token — the global AuthorizeFilter in Program.cs applies)
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Me()
        {
            var user = await _authService.GetUserByIdAsync(User.GetUserId());

            if (user is null)
            {
                // Token is valid, but the account was deleted after it was issued.
                return NotFound();
            }

            return Ok(user);
        }
    }
}
