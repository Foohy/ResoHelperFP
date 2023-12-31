using Elements.Core;
using SkyFrost.Base;

namespace ResoHelperFP;

public class SkyFrostHelperInterface
{
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private SkyFrostInterface? _skyFrostInterface;

    public event Action<Dictionary<string, SessionInfo>>? CurrentSessionStatusChanged;
    public event Action<ContactData>? NewContactRequest;
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
        _skyFrostInterface.Contacts.ContactAdded += ContactAdded;
        _skyFrostInterface.Sessions.SessionUpdated += SessionUpdated;
        _skyFrostInterface.Sessions.SessionRemoved += SessionRemoved;
        _skyFrostInterface.Sessions.SessionAdded += SessionAdded;
    }

    private void EnsureInitialized()
    {
        if (_skyFrostInterface == null)
        {
            throw new Exception("SkyFrost interface has not been initialized yet.");
        }
    }

    private void ContactAdded(ContactData contact)
    {
        NewContactRequest?.Invoke(contact);
        UniLog.Log("Contact added");
    }

    public void Dispose()
    {
        _skyFrostInterface?.Session.Logout(true);
    }

    private void SessionUpdated(SessionInfo sessionInfo)
    {
        EnsureInitialized();
        if (sessionInfo.HostUserId != _skyFrostInterface?.CurrentUserID) return;
        _sessions.TryGetValue(sessionInfo.SessionId, out var existing);
        if (existing != null && existing.ActiveUsers == sessionInfo.ActiveUsers)
        {
            return;
        }

        _sessions[sessionInfo.SessionId] = sessionInfo;
        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    private void SessionRemoved(SessionInfo sessionInfo)
    {
        EnsureInitialized();
        if (sessionInfo.HostUserId != _skyFrostInterface?.CurrentUserID) return;
        _sessions.Remove(sessionInfo.SessionId);
        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    private void SessionAdded(SessionInfo sessionInfo)
    {
        EnsureInitialized();
        if (sessionInfo.HostUserId != _skyFrostInterface!.CurrentUserID) return;
        _sessions[sessionInfo.SessionId] = sessionInfo;
        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    private void ContactStatusChanged(ContactData contact)
    {
        EnsureInitialized();
        if (contact.UserId != _skyFrostInterface!.CurrentUserID) return;
        var sessions = new HashSet<SessionInfo>();
        contact.DecodeSessions(sessions);
        foreach (var session in sessions)
        {
            _sessions[session.SessionId] = session;
        }

        CurrentSessionStatusChanged?.Invoke(_sessions);
    }

    public List<Contact> GetPendingFriendRequests()
    {
        EnsureInitialized();
        var contacts = new List<Contact>();
        _skyFrostInterface!.Contacts.GetContacts(contacts);
        return contacts.Where(contact => contact.IsContactRequest).ToList();
    }

    public async Task AcceptFriendRequest(Contact contact)
    {
        EnsureInitialized();
        await _skyFrostInterface!.Contacts.AddContact(contact);
    }

    public void Update()
    {
        _skyFrostInterface?.Update();
    }
}