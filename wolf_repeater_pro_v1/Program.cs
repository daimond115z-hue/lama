using WolfLive.Api;
using WolfLive.Api.Commands;

var b = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});

b.Configuration.Sources
    .OfType<Microsoft.Extensions.Configuration.Json.JsonConfigurationSource>()
    .ToList()
    .ForEach(x => x.ReloadOnChange = false);

b.WebHost.UseUrls("http://0.0.0.0:5000");

b.Services.AddSingleton<IWolfService, WolfService>();

b.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields =
        Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPropertiesAndHeaders |
        Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponsePropertiesAndHeaders;
});

var app = b.Build();

app.UseHttpLogging();
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = { "index.html" }
});
app.UseStaticFiles();

app.MapGet("/api/status", (IWolfService service) =>
{
    return Results.Ok(service.Status());
});

app.MapPost("/api/connect", async (Credentials r, IWolfService service) =>
{
    Console.WriteLine($"[API] /api/connect called email={r.Email}");

    var result = await service.ConnectAsync(r.Email, r.Password);

    Console.WriteLine($"[API] /api/connect response={result}");

    return Results.Ok(result);
});

app.MapPost("/api/disconnect", async (IWolfService service) =>
{
    var result = await service.DisconnectAsync();
    return Results.Ok(result);
});

app.MapPost("/api/room", async (RoomRequest r, IWolfService service) =>
{
    var result = await service.JoinRoomAsync(
        r.RoomId,
        r.RoomPassword ?? ""
    );

    return Results.Ok(result);
});

app.MapPost("/api/message", async (MessageRequest r, IWolfService service) =>
{
    var result = await service.SendGroupMessageAsync(r.Content);
    return Results.Ok(result);
});

app.Run();

record Credentials(string Email, string Password);
record RoomRequest(string RoomId, string? RoomPassword);
record MessageRequest(string Content);
