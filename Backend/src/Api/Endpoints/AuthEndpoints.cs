using Application;
using Microsoft.AspNetCore.Builder;

namespace Application.Results;
using Results = Microsoft.AspNetCore.Http.Results;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        // GET /api/auth/verify-email?email=...&token=...
        group.MapGet("/verify-email", async (
            string email,
            string token,
            AuthService authService) =>
        {
            var result = await authService.VerifyEmailAsync(email, token);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(new
            {
                success = true,
                message = "Email verified successfully"
            });
        });

        // POST /api/auth/request-password-reset
        group.MapPost("/request-password-reset", async (
            RequestPasswordResetRequest req,
            AuthService authService) =>
        {
            await authService.RequestPasswordResetAsync(req.Email);
            // Always return 200 to avoid leaking whether the email exists
            return Results.Ok(new { success = true });
        });

        // POST /api/auth/reset-password
        group.MapPost("/reset-password", async (
            ResetPasswordRequest req,
            AuthService authService) =>
        {
            var result = await authService.ResetPasswordAsync(req.Email, req.Token, req.Password);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }
            return Results.Ok(new { success = true });
        });
    }

    public record RequestPasswordResetRequest(string Email);
    public record ResetPasswordRequest(string Email, string Token, string Password);
}