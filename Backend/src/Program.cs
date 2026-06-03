using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// To run the server either do 'dotnet run' or 'dotnet watch'.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<MessageRouter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets();

app.Map("/lobby", async (HttpContext context, ConnectionManager connectionManager, MessageRouter router) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        string lobbyId = context.Request.Query["lobbyId"].ToString();
        string playerId = context.Request.Query["playerId"].ToString();

        if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(playerId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!connectionManager.IsLobbyAvailable(lobbyId))
        {
            Console.WriteLine($"Rejected {playerId}: Lobby {lobbyId} is full or doesn't exist.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        await connectionManager.HandleConnectionAsync(playerId, lobbyId, webSocket, router, context.RequestAborted);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.MapLobbyEndpoints();
app.Run();