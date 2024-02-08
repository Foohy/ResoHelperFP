using Elements.Core;
using SkyFrost.Base;

namespace ResoHelperFP;

public class SkyFrostHelperInterface
{
    private const string MachineIdPath = "./.secretMachineId.txt";
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private readonly List<SkyFrostInterface> _skyFrostInterfaces = new();

    public event Action<ContactData>? NewContactRequest;
    private readonly BotConfig _config;

    public SkyFrostHelperInterface(BotConfig config)
    {
        _config = config;
    }

    public async Task Initialize()
    {
        string secretMachineId;
        try
        {
            using var machineIdReader = new StreamReader(MachineIdPath);
            secretMachineId = await machineIdReader.ReadToEndAsync();
        }
        catch
        {
            File.Delete(MachineIdPath);
            secretMachineId = CryptoHelper.GenerateCryptoToken();
            await using var machineIdWriter = new StreamWriter(MachineIdPath);
            await machineIdWriter.WriteAsync(secretMachineId);
        }

        foreach (var resoniteConfig in _config.ResoniteConfigs)
        {
            var skyFrostInterface = new SkyFrostInterface(uid: UID.Compute(), secretMachineId: secretMachineId, config: SkyFrostConfig.DEFAULT_PRODUCTION);

            var machineId = Guid.NewGuid().ToString();
            var cloudResult = await skyFrostInterface.Session.Login(resoniteConfig.ResoniteUsername,
                new PasswordLogin(resoniteConfig.ResonitePassword), machineId, false, null);

            if (cloudResult.Content == "TOTP")
            {
                throw new Exception("Please disable 2FA");
            }

            if (cloudResult.IsError)
            {
                throw new Exception($"Something went wrong: {cloudResult.Content}");
            }

            await skyFrostInterface.Session.UpdateCurrentUserInfo();
            UniLog.Log("Login successful!");

            skyFrostInterface.Contacts.ContactAdded += ContactAdded;
            _skyFrostInterfaces.Add(skyFrostInterface);
        }
    }

    private void ContactAdded(ContactData contact)
    {
        NewContactRequest?.Invoke(contact);
    }

    public void Dispose()
    {
        foreach (var skyfrost in _skyFrostInterfaces)
        {
            skyfrost.Session.Logout(true);
        }
    }

    public List<SessionInfo> GetCurrentSessions()
    {
        return _sessions.Values.ToList();
    }

    public Dictionary<SkyFrostInterface, List<Contact>> GetPendingFriendRequests()
    {
        var result = new Dictionary<SkyFrostInterface, List<Contact>>();
        foreach (var skyFrostInterface in _skyFrostInterfaces)
        {
            var contacts = new List<Contact>();
            skyFrostInterface.Contacts.GetContacts(contacts);
            result.Add(skyFrostInterface, contacts.Where(contact => contact.IsContactRequest).ToList());
        }

        return result;
    }

    public async Task AcceptFriendRequest(SkyFrostInterface skyFrostInterface, Contact contact)
    {
        await skyFrostInterface.Contacts.AddContact(contact);
    }

    public async Task IgnoreFriendRequest(SkyFrostInterface skyFrostInterface, Contact contact)
    {
        await skyFrostInterface.Contacts.IgnoreRequest(contact);
    }

    public void Update()
    {
        foreach (var skyFrostInterface in _skyFrostInterfaces)
        {
            skyFrostInterface.Update();
        }
    }
}