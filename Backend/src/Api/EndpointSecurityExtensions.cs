using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

public static class EndpointSecurityExtensions
{
    public static RouteGroupBuilder RequireJwtAuthentication(this RouteGroupBuilder group, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"];
        if (!string.IsNullOrWhiteSpace(jwtKey))
        {
            group.RequireAuthorization();
        }

        return group;
    }
}
