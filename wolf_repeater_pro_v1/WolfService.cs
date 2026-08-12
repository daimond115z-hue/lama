using WolfLive.Api;
using WolfLive.Api.Commands;

public record WolfResponse(bool Ok, string State, string Account, string Room, string Message);

public interface IWolfService
{
    WolfResponse Status();
    Task<WolfResponse> ConnectAsync(string email, string password);
    Task<WolfResponse> DisconnectAsync();
    Task<WolfResponse> JoinRoomAsync(string roomId, string roomPassword);
    Task<WolfResponse> SendGroupMessageAsync(string content);
}

public sealed class WolfService : IWolfService
{
    private IWolfClient? _client;
    private readonly object _sync = new();
    private WolfResponse _current = new(false, "غير متصل", string.Empty, string.Empty, "البرنامج جاهز");
    private string _lastEmail = string.Empty;
    private string _lastPassword = string.Empty;
    private bool _explicitDisconnect;
    private int _reconnectAttempts;

    public WolfResponse Status() => _current;

    public async Task<WolfResponse> ConnectAsync(string email, string password)
    {
        _lastEmail = email?.Trim() ?? string.Empty;
        _lastPassword = password ?? string.Empty;
        _explicitDisconnect = false;
        _reconnectAttempts = 0;

        lock (_sync)
        {
            _current = _current with { State = "جاري الاتصال...", Message = "جاري تحضير الاتصال" };
        }

        await DisconnectInternalAsync();

        try
        {
            var client = new WolfClient()
                .SetupCommands()
                .WithSerilog()
                .Done();

            client.OnConnected += client => UpdateState("متصل", "تم الاتصال بـ WOLF");
            client.OnDisconnected += async (client, error) =>
            {
                UpdateState("غير متصل", string.IsNullOrWhiteSpace(error) ? "تم قطع الاتصال" : $"تم قطع الاتصال: {error}");
                await TryReconnectAsync();
            };

            await client.Connect();

            var loginOk = await client.Login(email, password);
            if (!loginOk)
            {
                await DisconnectInternalAsync();
                return UpdateCurrent(false, "فشل", string.Empty, string.Empty, "فشل تسجيل الدخول. تحقق من بياناتك.");
            }

            _client = client;
            return UpdateCurrent(true, "متصل", email, string.Empty, "تم تسجيل الدخول بنجاح.");
        }
        catch (Exception ex)
        {
            await DisconnectInternalAsync();
            return UpdateCurrent(false, "خطأ", string.Empty, string.Empty, ex.Message);
        }
    }

    public async Task<WolfResponse> DisconnectAsync()
    {
        _explicitDisconnect = true;
        _reconnectAttempts = 0;
        await DisconnectInternalAsync();
        return _current;
    }

    public async Task<WolfResponse> JoinRoomAsync(string roomId, string roomPassword)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return UpdateCurrent(false, _current.State, _current.Account, string.Empty, "يرجى إدخال معرف الروم.");
        }

        if (_client == null)
        {
            return UpdateCurrent(false, "غير متصل", string.Empty, string.Empty, "يجب تسجيل الدخول أولاً.");
        }

        try
        {
            await _client.JoinGroup(roomId, roomPassword ?? string.Empty);
            return UpdateCurrent(true, "متصل", _current.Account, roomId, "تم إرسال طلب الدخول إلى الروم.");
        }
        catch (Exception ex)
        {
            return UpdateCurrent(false, _current.State, _current.Account, _current.Room, ex.Message);
        }
    }

    public async Task<WolfResponse> SendGroupMessageAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return UpdateCurrent(false, _current.State, _current.Account, _current.Room, "يرجى إدخال نص الرسالة.");
        }

        if (_client == null)
        {
            return UpdateCurrent(false, "غير متصل", string.Empty, string.Empty, "يجب تسجيل الدخول أولاً.");
        }

        if (string.IsNullOrWhiteSpace(_current.Room))
        {
            return UpdateCurrent(false, _current.State, _current.Account, string.Empty, "يجب تحديد الغرفة أولاً.");
        }

        try
        {
            await _client.GroupMessage(_current.Room, content);
            return UpdateCurrent(true, "متصل", _current.Account, _current.Room, $"تم إرسال الرسالة: {content}");
        }
        catch (Exception ex)
        {
            return UpdateCurrent(false, _current.State, _current.Account, _current.Room, ex.Message);
        }
    }

    private Task DisconnectInternalAsync()
    {
        lock (_sync)
        {
            _client = null;
            _current = _current with { Ok = false, State = "غير متصل", Room = string.Empty, Message = "تم قطع الاتصال" };
        }

        return Task.CompletedTask;
    }

    private async Task TryReconnectAsync()
    {
        if (_explicitDisconnect || string.IsNullOrWhiteSpace(_lastEmail) || string.IsNullOrWhiteSpace(_lastPassword))
        {
            return;
        }

        if (_reconnectAttempts >= 5)
        {
            UpdateState("غير متصل", "توقف إعادة المحاولة بعد 5 محاولات. تحقق من الشبكة أو بيانات الحساب.");
            return;
        }

        _reconnectAttempts++;
        UpdateState("جاري إعادة الاتصال...", $"محاولة إعادة الاتصال {_reconnectAttempts}/5");

        await Task.Delay(TimeSpan.FromSeconds(5 * _reconnectAttempts));

        try
        {
            await ConnectAsync(_lastEmail, _lastPassword);
        }
        catch
        {
            UpdateState("غير متصل", "فشل إعادة الاتصال. سيتم اعادة المحاولة لاحقًا.");
        }
    }

    private WolfResponse UpdateCurrent(bool ok, string state, string account, string room, string message)
    {
        lock (_sync)
        {
            _current = new WolfResponse(ok, state, account, room, message);
            return _current;
        }
    }

    private void UpdateState(string state, string message)
    {
        lock (_sync)
        {
            _current = _current with { State = state, Message = message };
        }
    }
}
