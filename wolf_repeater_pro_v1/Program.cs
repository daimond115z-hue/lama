using System.Text.Json;
using WolfLive.Api;
using WolfLive.Api.Commands;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});

builder.Configuration.Sources
    .OfType<Microsoft.Extensions.Configuration.Json.JsonConfigurationSource>()
    .ToList()
    .ForEach(x => x.ReloadOnChange = false);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Services.AddSingleton<IWolfService, WolfService>();

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields =
        Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPropertiesAndHeaders |
        Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponsePropertiesAndHeaders;
});

var app = builder.Build();

app.UseHttpLogging();

app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = { "index.html" }
});

app.UseStaticFiles();


// =========================
// الحالة
// =========================

app.MapGet("/api/status", (IWolfService service) =>
{
    return Results.Ok(service.Status());
});


// =========================
// تسجيل الدخول
// =========================

app.MapPost("/api/connect", async (Credentials request, IWolfService service) =>
{
    Console.WriteLine($"[API] /api/connect email={request.Email}");

    var result = await service.ConnectAsync(
        request.Email,
        request.Password
    );

    Console.WriteLine(
        $"[API] /api/connect response={JsonSerializer.Serialize(result)}"
    );

    return Results.Ok(result);
});


// =========================
// قطع الاتصال
// =========================

app.MapPost("/api/disconnect", async (IWolfService service) =>
{
    var result = await service.DisconnectAsync();

    Console.WriteLine("[API] /api/disconnect");

    return Results.Ok(result);
});


// =========================
// دخول الروم
// =========================

app.MapPost("/api/room", async (RoomRequest request, IWolfService service) =>
{
    Console.WriteLine(
        $"[API] /api/room room={request.RoomId}"
    );

    var result = await service.JoinRoomAsync(
        request.RoomId,
        request.RoomPassword ?? string.Empty
    );

    return Results.Ok(result);
});


// =========================
// إرسال رسالة
// =========================

app.MapPost("/api/message", async (MessageRequest request, IWolfService service) =>
{
    Console.WriteLine(
        $"[API] /api/message content={request.Content}"
    );

    var result = await service.SendGroupMessageAsync(
        request.Content
    );

    return Results.Ok(result);
});


app.Run();


// =========================
// Models
// =========================

public record Credentials(
    string Email,
    string Password
);

public record RoomRequest(
    string RoomId,
    string? RoomPassword
);

public record MessageRequest(
    string Content
);
