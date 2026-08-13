using System.Text.Json;
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
b.Services.AddSingleton<WolfEngine>();
b.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPropertiesAndHeaders | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponsePropertiesAndHeaders;
});
var app = b.Build();
app.UseHttpLogging();
app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = { "index.html" } });
app.UseStaticFiles();
app.MapGet("/api/status", (WolfEngine e) => e.Status());
app.MapPost("/api/connect", async (Credentials r, WolfEngine e) =>
{
    Console.WriteLine($"[API] /api/connect called email={r.Email} room={e.Room}");
    var status = await e.Connect(r.Email, r.Password);
    Console.WriteLine($"[API] /api/connect response={JsonSerializer.Serialize(status)}");
    return Results.Ok(status);
});
app.MapPost("/api/disconnect", async (WolfEngine e) => { await e.Disconnect(); Console.WriteLine("[API] /api/disconnect called"); return Results.Ok(e.Status()); });
app.MapPost("/api/room", (RoomRequest r, WolfEngine e) => { e.Room = r.Room.Trim(); e.Message = "تم تحديد الغرفة."; Console.WriteLine($"[API] /api/room called room={e.Room}"); return Results.Ok(e.Status()); });
app.Run();
record Credentials(string Email, string Password);
record RoomRequest(string Room);
sealed class WolfEngine {
 IWolfClient? c; public string State { get; private set; } = "غير متصل"; public string Account { get; private set; } = ""; public string Room { get; set; } = ""; public string Message { get; set; } = "";
 public object Status() => new { state = State, account = Account, room = Room, message = Message };
 public async Task<object> Connect(string email, string password) { await Disconnect(); try { c = new WolfClient()
                .SetupCommands()
                .WithSerilog()
                .Done();
            c.OnConnected += (_) => { State = "متصل"; Message = "تم الاتصال بـ WOLF"; };
            var ok = await c.Login(email, password);
            Console.WriteLine($"[WolfEngine] Login result={ok} email={email}");
            if (!ok) { c = null; State = "فشل"; Message = "فشل تسجيل الدخول."; return Status(); }
            Account = email; State = "متصل"; Message = "تم تسجيل الدخول والاتصال.";
            return Status();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WolfEngine] Connect exception={ex}");
            c = null; State = "خطأ"; Message = ex.Message; return Status();
        }
    }
 public async Task Disconnect() { c = null; State = "غير متصل"; Message = "تم القطع يدويًا."; await Task.CompletedTask; }
}
