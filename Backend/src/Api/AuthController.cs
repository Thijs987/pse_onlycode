using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Domain;
using Application;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Application.Results;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

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
        // Use configured base URL (AppSettings:BaseUrl)
        var configuredBase = _config["AppSettings:BaseUrl"];
        var result = await _auth.Register(request.Email, request.Username, request.Password, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(), baseUrl: configuredBase);
        if (!result.IsSuccess)
        {
            // authentication error
            return Unauthorized(result.Error);
        }

        return Ok(result.Value);
    }

    // Login request, returns all users info
    [HttpPost("login")]
    public async Task<IActionResult> Logincontrol(LoginRequest request)
    {
        var result = await _auth.Login(request.Username, request.Password);
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

        // Create JWT token
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

        // Serialize token to string
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

        // Create refresh token (stored server-side) and set as HttpOnly Secure cookie
        var refreshToken = await _auth.CreateRefreshTokenForUserAsync(result.Value.Id, HttpContext.Connection.RemoteIpAddress?.ToString());
        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30) // default lifetime; configurable
        };
        Response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);

        // Double-submit CSRF: create a readable CSRF token cookie for JS to read and include in the X-CSRF-Token header
        var csrfToken = GenerateCsrfToken();
        var csrfCookieOptions = new CookieOptions
        {
            HttpOnly = false, // must be readable by JS
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };
        Response.Cookies.Append("csrf_token", csrfToken, csrfCookieOptions);

        return Ok(new { user = result.Value, token = tokenString, expires = token.ValidTo });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        // Get refresh token from cookie
        var refreshToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrWhiteSpace(refreshToken)) return Unauthorized(new ServiceError(ServiceErrorCode.InvalidCredentials, "Missing refresh token."));

        var res = await _auth.ValidateAndRotateRefreshTokenAsync(refreshToken, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!res.IsSuccess)
        {
            return Unauthorized(res.Error);
        }

        var (userDto, newRefreshToken) = res.Value;

        // issue new access token
        var jwtKey = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey)) return Ok(new { user = userDto });

        var issuer = _config["Jwt:Issuer"] ?? "PSE-Green";
        var audience = _config["Jwt:Audience"] ?? "PSE-Green-Clients";
        var expiresMinutes = 15;
        if (int.TryParse(_config["Jwt:AccessTokenMinutes"], out var configured)) expiresMinutes = configured;

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userDto.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, userDto.Email),
            new Claim("username", userDto.Username),
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

        // Set new access token and refresh token cookies
        var cookieOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Expires = new DateTimeOffset(token.ValidTo) };
        Response.Cookies.Append("access_token", tokenString, cookieOptions);

        var refreshCookieOptions = new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(30) };
        Response.Cookies.Append("refresh_token", newRefreshToken, refreshCookieOptions);

        // Rotate CSRF token
        var newCsrf = GenerateCsrfToken();
        var newCsrfCookieOptions = new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(30) };
        Response.Cookies.Append("csrf_token", newCsrf, newCsrfCookieOptions);

        return Ok(new { user = userDto, token = tokenString, expires = token.ValidTo });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Revoke refresh token if present
        var refreshToken = Request.Cookies["refresh_token"];
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _auth.RevokeRefreshTokenAsync(refreshToken);
        }

        // delete cookies
        Response.Cookies.Delete("refresh_token");
        Response.Cookies.Delete("access_token");
        Response.Cookies.Delete("csrf_token");

        return Ok(new { success = true });
    }

    private static string GenerateCsrfToken()
    {
        // Generate a random CSRF token (32 bytes, base64url encoded)
        using var rng = RandomNumberGenerator.Create();
        var data = new byte[32];
        rng.GetBytes(data);
        return Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public record RegisterRequest(
        [Required]
        [EmailAddress]
        [StringLength(255)]
        string Email,
        [Required]
        [StringLength(30, MinimumLength = 3)]
        string Username,
        [Required]
        [StringLength(100, MinimumLength = 8)]
        string Password);

    public record LoginRequest(
        [Required]
        [StringLength(30, MinimumLength = 3)]
        string Username,
        [Required]
        [StringLength(100, MinimumLength = 8)]
        string Password);
}
