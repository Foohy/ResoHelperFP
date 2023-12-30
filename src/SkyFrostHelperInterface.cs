using Elements.Core;
using Newtonsoft.Json;
using SkyFrost.Base;

namespace ResoHelperFP;

public class SkyFrostHelperInterface
{
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private SkyFrostInterface? _skyFrostInterface;

    public event Action<Dictionary<string, SessionInfo>>? CurrentSessionStatusChanged;
    private readonly BotConfig _config;

    public SkyFrostHelperInterface(BotConfig config)
    {
        _config = config;
    }

    public async Task Initialize()
    {
        _skyFrostInterface = new SkyFrostInterface(uid: UID.Compute(), SkyFrostConfig.DEFAULT_PRODUCTION);

        var machineId = Guid.NewGuid().ToString();
        var cloudResult = await _skyFrostInterface.Session.Login(_config.ResoniteUsername,
            new PasswordLogin(_config.ResonitePassword), machineId, false, null);

        if (cloudResult.Content == "TOTP")
        {
            throw new Exception("Please disable 2FA");
        }

        if (cloudResult.IsError)
        {
            throw new Exception($"Something went wrong: {cloudResult.Content}");
        }

        await _skyFrostInterface.Session.UpdateCurrentUserInfo();
        UniLog.Log("Login successful!");

        _skyFrostInterface.Contacts.ContactStatusChanged += ContactStatusChanged;
        _skyFrostInterface.Sessions.SessionUpdated += SessionUpdated;
        _skyFrostInterface.Sessions.SessionRemoved += SessionRemoved;
        _skyFrostInterface.Sessions.SessionAdded += SessionAdded;
    }

    public void Dispose()
    {
        _skyFrostInterface?.Session.Logout(true);
    }

    private void SessionUpdated(SessionInfo sessionInfo)
    {
        if (sessionInfo.HostUserId != _skyFrostInterface?.CurrentUserID) return;
        SessionInfo? existing;
        _sessions.TryGetValue(sessionInfo.SessionId, out existing);
        if (existing != null && existing.ActiveUsers == sessionInfo.ActiveUsers)
        {
            return;
        }
        _sessions[sessionInfo.SessionId] = sessionInfo;
        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    private void SessionRemoved(SessionInfo sessionInfo)
    {
        if (sessionInfo.HostUserId != _skyFrostInterface?.CurrentUserID) return;
        _sessions.Remove(sessionInfo.SessionId);
        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    private void SessionAdded(SessionInfo sessionInfo)
    {
        if (sessionInfo.HostUserId != _skyFrostInterface?.CurrentUserID) return;
        _sessions[sessionInfo.SessionId] = sessionInfo;
        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    private void ContactStatusChanged(ContactData contact)
    {
        if (contact.UserId != _skyFrostInterface?.CurrentUserID) return;
        var sessions = new HashSet<SessionInfo>();
        contact.DecodeSessions(sessions);
        foreach (var session in sessions)
        {
            _sessions[session.SessionId] = session;
        }

        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    public void Update()
    {
        _skyFrostInterface?.Update();
    }
}