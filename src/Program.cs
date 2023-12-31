using System.Configuration;
using System.Globalization;
using Discord.WebSocket;
using Elements.Core;
using Newtonsoft.Json;
using SkyFrost.Base;

namespace ResoHelperFP;

internal static class Program
{
    private static async Task<int> Main(string[] _)
    {
        var resoHelperFp = new ResoHelperFp();
        try
        {
            await resoHelperFp.MainLoop();
        }
        catch
        {
            return 1;
        }

        return 0;
    }
}

internal class ResoHelperFp
{
    private bool _running = true;
    private SkyFrostHelperInterface? _skyFrost;
    private DiscordInterface? _discordInterface;
    private BotConfig? _config;

    public async Task MainLoop()
    {
        Console.CancelKeyPress += OnCancel;

        _config = JsonConvert.DeserializeObject<BotConfig>(await File.ReadAllTextAsync("config.json"));

        if (_config == null)
        {
            throw new ConfigurationErrorsException("Failed to load config.json");
        }

        _skyFrost = new SkyFrostHelperInterface(_config);
        _discordInterface = new DiscordInterface(_config);

        _discordInterface.SlashCommandReceived += HandleSlashCommands;
        _skyFrost.NewContactRequest += OnNewContactRequest;

        try
        {
            await _skyFrost.Initialize();
            await _discordInterface.MainAsync();
            _skyFrost.CurrentSessionStatusChanged += _discordInterface.SetSessionInfoBuffered;
            while (_running)
            {
                _skyFrost.Update();
                await Task.Delay(1000);
            }
        }
        catch (Exception e)
        {
            UniLog.Error($"Bot died: {e.Message}");
            throw;
        }
    }

    private void OnNewContactRequest(ContactData contact)
    {
        _discordInterface!.SendMessage(
            $"New contact request to the headless from user '{contact.Contact.ContactUsername}' with ID '{contact.Contact.ContactUserId}'." +
            $"\nYou can accept this request with the following command: `/contact accept {contact.Contact.ContactUsername}`");
    }

    private async Task HandleSlashCommands(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "week":
            {
                var now = DateTime.Now;
                var week = GetIso8601WeekOfYear(now);
                var sessionType = week % 2 == 1 ? "Movie Night" : "Funny Friday";
                string response;
                if (now.DayOfWeek < DayOfWeek.Friday)
                {
                    response = $"The coming weekend is **{sessionType}**!";
                }
                else
                {
                    var offweekType = week % 2 == 0 ? "Movie Night" : "Funny Friday";
                    response =
                        $"The current weekend is **{sessionType}**! Next weekend will be {offweekType}";
                }

                await command.RespondAsync(response);
                break;
            }
            case "contact":
            {
                await HandleContactsCommand(command);
                break;
            }
            default:
            {
                return;
            }
        }
    }

    private async Task HandleContactsCommand(SocketSlashCommand command)
    {
        if (_skyFrost == null)
        {
            await command.RespondAsync("Connection to Resonite API has not been initialized, please try again later.");
            return;
        }

        var subCommandName = command.Data.Options.First().Name;
        switch (subCommandName)
        {
            case "requests":
            {
                var contacts = _skyFrost.GetPendingFriendRequests();
                if (contacts.Count == 0)
                {
                    await command.RespondAsync("There are currently no pending friend requests.");
                    return;
                }

                await command.RespondAsync("The following user" + (contacts.Count == 1 ? " is " : "s are ") +
                                           "waiting to get approved:\n" +
                                           string.Join("\n", contacts.Select(contact => contact.ContactUsername)));
                break;
            }
            case "accept":
            {
                var username = command.Data.Options.First().Options.First().Value as string;
                var contact = _skyFrost.GetPendingFriendRequests().FirstOrDefault(c => c.ContactUsername == username);
                if (contact == null)
                {
                    await command.RespondAsync($"Friend request for user {username} not found.");
                    return;
                }

                await _skyFrost.AcceptFriendRequest(contact);
                await command.RespondAsync($"Accepted friend request from user {contact.ContactUsername}");
                break;
            }
        }
    }

    private static int GetIso8601WeekOfYear(DateTime time)
    {
        // Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll 
        // be the same week# as whatever Thursday, Friday or Saturday are,
        // and we always get those right
        var day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
        if (day is >= DayOfWeek.Monday and <= DayOfWeek.Wednesday)
        {
            time = time.AddDays(3);
        }

        // Return the week of our adjusted day
        return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }

    private void OnCancel(object? sender, ConsoleCancelEventArgs args)
    {
        _running = false;
        _skyFrost?.Dispose();
        _discordInterface?.Dispose();
    }
}