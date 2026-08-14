using WolfLive.Api;
using WolfLive.Api.Commands;

public record WolfResponse(
    bool Ok,
    string State,
    string Account,
    string Room,
    string Message
);

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

    private WolfResponse _current =
        new(
            false,
            "غير متصل",
            string.Empty,
            string.Empty,
            "البرنامج جاهز"
        );

    private string _lastEmail = string.Empty;
    private string _lastPassword = string.Empty;

    private string _lastRoom = string.Empty;
    private string _lastRoomPassword = string.Empty;

    private bool _explicitDisconnect;
    private int _reconnectAttempts;

    public WolfResponse Status()
    {
        lock (_sync)
        {
            return _current;
        }
    }

    // =========================
    // تسجيل الدخول
    // =========================

    public async Task<WolfResponse> ConnectAsync(
        string email,
        string password)
    {
        _lastEmail = email?.Trim() ?? string.Empty;
        _lastPassword = password ?? string.Empty;

        _explicitDisconnect = false;
        _reconnectAttempts = 0;

        string savedRoom;

        lock (_sync)
        {
            savedRoom = _lastRoom;

            _current = _current with
            {
                State = "جاري الاتصال...",
                Account = _lastEmail,
                Message = "جاري تحضير الاتصال"
            };
        }

        await DisconnectInternalAsync(
            keepRoom: true
        );

        try
        {
            var client = new WolfClient()
                .SetupCommands()
                .WithSerilog()
                .Done();

            client.OnConnected += _ =>
            {
                UpdateState(
                    "متصل",
                    "تم الاتصال بـ WOLF"
                );
            };

            client.OnDisconnected += async (_, error) =>
            {
                UpdateState(
                    "غير متصل",
                    string.IsNullOrWhiteSpace(error)
                        ? "تم قطع الاتصال"
                        : $"تم قطع الاتصال: {error}"
                );

                await TryReconnectAsync();
            };

            await client.Connect();

            var loginOk =
                await client.Login(
                    _lastEmail,
                    _lastPassword
                );

            Console.WriteLine(
                $"[WolfService] Login result={loginOk}"
            );

            if (!loginOk)
            {
                await DisconnectInternalAsync(
                    keepRoom: true
                );

                return UpdateCurrent(
                    false,
                    "فشل",
                    _lastEmail,
                    savedRoom,
                    "فشل تسجيل الدخول. تحقق من بياناتك."
                );
            }

            _client = client;

            // إعادة الدخول للروم السابقة بعد تسجيل الدخول
            if (!string.IsNullOrWhiteSpace(savedRoom))
            {
                try
                {
                    await _client.JoinGroup(
                        savedRoom,
                        _lastRoomPassword
                    );

                    Console.WriteLine(
                        $"[WolfService] Rejoined room={savedRoom}"
                    );
                }
                catch (Exception roomEx)
                {
                    Console.WriteLine(
                        $"[WolfService] Rejoin exception={roomEx}"
                    );
                }
            }

            return UpdateCurrent(
                true,
                "متصل",
                _lastEmail,
                savedRoom,
                string.IsNullOrWhiteSpace(savedRoom)
                    ? "تم تسجيل الدخول بنجاح."
                    : $"تم تسجيل الدخول. الروم الحالية: {savedRoom}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[WolfService] Connect exception={ex}"
            );

            await DisconnectInternalAsync(
                keepRoom: true
            );

            return UpdateCurrent(
                false,
                "خطأ",
                _lastEmail,
                savedRoom,
                ex.Message
            );
        }
    }

    // =========================
    // قطع الاتصال
    // =========================

    public async Task<WolfResponse> DisconnectAsync()
    {
        _explicitDisconnect = true;
        _reconnectAttempts = 0;

        await DisconnectInternalAsync(
            keepRoom: false
        );

        return Status();
    }

    // =========================
    // دخول الروم
    // =========================

    public async Task<WolfResponse> JoinRoomAsync(
        string roomId,
        string roomPassword)
    {
        roomId = roomId?.Trim() ?? string.Empty;
        roomPassword ??= string.Empty;

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return UpdateCurrent(
                false,
                _current.State,
                _current.Account,
                _current.Room,
                "يرجى إدخال معرف الروم."
            );
        }

        if (_client == null)
        {
            return UpdateCurrent(
                false,
                "غير متصل",
                _current.Account,
                _current.Room,
                "يجب تسجيل الدخول أولاً."
            );
        }

        try
        {
            Console.WriteLine(
                $"[WolfService] Joining room={roomId}"
            );

            await _client.JoinGroup(
                roomId,
                roomPassword
            );

            _lastRoom = roomId;
            _lastRoomPassword = roomPassword;

            return UpdateCurrent(
                true,
                "متصل",
                _current.Account,
                roomId,
                $"تم تحديد الروم {roomId}."
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[WolfService] JoinRoom exception={ex}"
            );

            return UpdateCurrent(
                false,
                _current.State,
                _current.Account,
                _current.Room,
                ex.Message
            );
        }
    }

    // =========================
    // إرسال رسالة
    // =========================

    public async Task<WolfResponse> SendGroupMessageAsync(
        string content)
    {
        content = content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            return UpdateCurrent(
                false,
                _current.State,
                _current.Account,
                _current.Room,
                "يرجى إدخال نص الرسالة."
            );
        }

        if (_client == null)
        {
            return UpdateCurrent(
                false,
                "غير متصل",
                _current.Account,
                _current.Room,
                "يجب تسجيل الدخول أولاً."
            );
        }

        string room;

        lock (_sync)
        {
            room = _current.Room;

            // احتياط: استخدم آخر روم محفوظة إذا كانت الحالة فارغة
            if (string.IsNullOrWhiteSpace(room))
            {
                room = _lastRoom;
            }
        }

        if (string.IsNullOrWhiteSpace(room))
        {
            return UpdateCurrent(
                false,
                _current.State,
                _current.Account,
                string.Empty,
                "يجب تحديد الغرفة أولاً."
            );
        }

        try
        {
            Console.WriteLine(
                $"[WolfService] Sending message to room={room}"
            );

            await _client.GroupMessage(
                room,
                content
            );

            return UpdateCurrent(
                true,
                "متصل",
                _current.Account,
                room,
                $"تم إرسال الرسالة: {content}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[WolfService] SendMessage exception={ex}"
            );

            return UpdateCurrent(
                false,
                _current.State,
                _current.Account,
                room,
                ex.Message
            );
        }
    }

    // =========================
    // قطع الاتصال الداخلي
    // =========================

    private Task DisconnectInternalAsync(
        bool keepRoom)
    {
        lock (_sync)
        {
            _client = null;

            _current = _current with
            {
                Ok = false,
                State = "غير متصل",
                Account = _lastEmail,
                Room = keepRoom
                    ? _lastRoom
                    : string.Empty,
                Message = "تم قطع الاتصال"
            };

            if (!keepRoom)
            {
                _lastRoom = string.Empty;
                _lastRoomPassword = string.Empty;
            }
        }

        return Task.CompletedTask;
    }

    // =========================
    // إعادة الاتصال
    // =========================

    private async Task TryReconnectAsync()
    {
        if (
            _explicitDisconnect ||
            string.IsNullOrWhiteSpace(_lastEmail) ||
            string.IsNullOrWhiteSpace(_lastPassword)
        )
        {
            return;
        }

        if (_reconnectAttempts >= 5)
        {
            UpdateState(
                "غير متصل",
                "توقف إعادة المحاولة بعد 5 محاولات. تحقق من الشبكة أو بيانات الحساب."
            );

            return;
        }

        _reconnectAttempts++;

        UpdateState(
            "جاري إعادة الاتصال...",
            $"محاولة إعادة الاتصال {_reconnectAttempts}/5"
        );

        await Task.Delay(
            TimeSpan.FromSeconds(
                5 * _reconnectAttempts
            )
        );

        try
        {
            await ConnectAsync(
                _lastEmail,
                _lastPassword
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[WolfService] Reconnect exception={ex}"
            );

            UpdateState(
                "غير متصل",
                "فشل إعادة الاتصال."
            );
        }
    }

    // =========================
    // تحديث الحالة
    // =========================

    private WolfResponse UpdateCurrent(
        bool ok,
        string state,
        string account,
        string room,
        string message)
    {
        lock (_sync)
        {
            _current =
                new WolfResponse(
                    ok,
                    state,
                    account,
                    room,
                    message
                );

            return _current;
        }
    }

    private void UpdateState(
        string state,
        string message)
    {
        lock (_sync)
        {
            _current = _current with
            {
                State = state,
                Message = message
            };
        }
    }
}
