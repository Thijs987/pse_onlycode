using Infrastructure.Persistence;
using Application;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

/* Services
 * - AuthService: Handles user registration and login
 * - LobbyService: Handles lobby creation and management
 */ /*
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LobbyService>();
*/

var app = builder.Build();

app.MapControllers();

app.Run();