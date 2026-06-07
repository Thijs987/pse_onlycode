using Application;
using Microsoft.AspNetCore.Builder;

namespace Application.Results;
using Results = Microsoft.AspNetCore.Http.Results;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

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
    }
}