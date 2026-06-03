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

    public UserController(AuthService auth)
    {
        _auth = auth;
    }

    //register request, for now returns all the users info
    [HttpPost("register")]
    public async Task<IActionResult> CreateUser(RegisterRequest request)
    {
        var result = await _auth.Register(request.Email, request.Username, request.Password);
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
