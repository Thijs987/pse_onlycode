using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Domain;
using Application;

[ApiController]
[Route("api/auth")]
public class UserController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

    public UserController(AuthService auth, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _auth = auth;
        _config = config;
    }

    //register request, for now returns all the users info
    [HttpPost("register")]
    public async Task<IActionResult> CreateUser(RegisterRequest request)
    {
        // Use configured base URL (AppSettings:BaseUrl) so verification links point to the configured server
        var configuredBase = _config["AppSettings:BaseUrl"];
        var result = await _auth.Register(request.Email, request.Username, request.Password, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(), baseUrl: configuredBase);
        if (!result.IsSuccess)
        {
            // authentication error
            return Unauthorized(result.Error);
        }

        return Ok(result.Value);
    }

    // Login request, for now returns all users info
    [HttpPost("login")]
    public async Task<IActionResult> Logincontrol(LoginRequest request)
    {
        var result = await _auth.Login(request.Email, request.Password);
        if (!result.IsSuccess)
        {
            // authentication error
            return Unauthorized(result.Error);
        }

        return Ok(result.Value);
    }

    public record RegisterRequest(string Email, string Username, string Password);
    public record LoginRequest(string Email, string Password);
}
