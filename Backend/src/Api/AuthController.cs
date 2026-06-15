using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Domain;
using Application;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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

        // If JWT settings are configured, issue a token. Otherwise return user info only.
        var jwtKey = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            return Ok(result.Value);
        }

        var issuer = _config["Jwt:Issuer"] ?? "PSE-Green";
        var audience = _config["Jwt:Audience"] ?? "PSE-Green-Clients";

        // token lifetime in minutes (maybe add in config Jwt:ExpiresMinutes)
        var expiresMinutes = 60 * 24 * 7; // 7 days
        if (int.TryParse(_config["Jwt:ExpiresMinutes"], out var configured))
        {
            expiresMinutes = configured;
        }

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, result.Value.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, result.Value.Email),
            new Claim("username", result.Value.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        // Set token as HttpOnly, Secure cookie for browser-based clients
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(token.ValidTo)
        };

        // Cookie name: access_token
        Response.Cookies.Append("access_token", tokenString, cookieOptions);

        return Ok(new { user = result.Value, token = tokenString, expires = token.ValidTo });
    }

    public record RegisterRequest(string Email, string Username, string Password);
    public record LoginRequest(string Email, string Password);
}
