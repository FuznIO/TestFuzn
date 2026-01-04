using Microsoft.AspNetCore.Mvc;
using SampleApp.WebApp.Services;

namespace SampleApp.WebApp.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly UserService _userService;

    public AuthController(TokenService tokenService, UserService userService)
    {
        _tokenService = tokenService;
        _userService = userService;
    }

    [HttpPost("token")]
    public ActionResult<TokenResponse> GetToken([FromBody] LoginRequest request)
    {
        var user = _userService.ValidateUser(request.Username, request.Password);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        var token = _tokenService.GenerateToken(user.Username);
        return Ok(new TokenResponse(token, DateTime.UtcNow.AddHours(1)));
    }
}

public record LoginRequest(string Username, string Password);
public record TokenResponse(string Token, DateTime ExpiresAt);
