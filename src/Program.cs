using System.Configuration;
using System.Globalization;
using CliWrap;
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
        await resoHelperFp.MainLoop();
        return 0;
    }
}

internal class ResoHelperFp
{
    private bool _running = true;
    private SkyFrostHelperInterface? _skyFrost;
    private DiscordInterface? _discordInterface;
    private SessionDataReceiver? _dataReceiver;
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
        _dataReceiver = new SessionDataReceiver();
        try
        {
            await _skyFrost.Initialize();
            await _discordInterface.MainAsync();
            _discordInterface.SlashCommandReceived += HandleSlashCommands;
            _skyFrost.NewContactRequest += OnNewContactRequest;
            _dataReceiver.SessionsUpdated += _discordInterface.SetSessionDataBuffered;

            _ = Task.Run(async () =>
            {
                while (_running)
                {
                    try
                    {
                        await _dataReceiver.HandleConnection();
                    }
                    catch (Exception e)
                    {
                        UniLog.Error("Failed to receive status message: " + e.Message);
                    }
                }
            });
            while (_running)
            {
                _skyFrost.Update();
                await Task.Delay(5000);
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
            case "sessions":
            {
                var sessions = _discordInterface?.CurrentSessions ??
                               new Dictionary<string, Dictionary<string, SessionData>>();
                string response;
                if (!sessions.Any())
                {
                    response = "There are currently no sessions running.";
                }
                else
                {
                    response = string.Join("\n",
                            sessions.Select(pair =>
                                $"{pair.Key}\n{string.Join("\n", pair.Value
                                    .Where(info => info.Key != "Userspace" && info.Key != "Local")
                                    .Select(valuePair => $"- {valuePair.Key}: {valuePair.Value.ActiveUserCount} ({valuePair.Value.UserCount})"))}"))
                        .Trim();
                }

                await command.RespondAsync(response);
                break;
            }
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
            case "restart":
            {
                var opt = command.Data.Options.FirstOrDefault();
                var instances = _config?.InstanceNames ?? new List<string>();
                if (opt == null)
                {
                    await command.RespondAsync(
                        $"Please specify which instance to restart:\n{string.Join("\n", instances.Select(s => $"- {s}"))}"
                            .Trim());
                    return;
                }

                var instance = opt.Value.ToString() ?? "";
                if (!instances.Contains(instance))
                {
                    await command.RespondAsync($"Instance '{instance}' does not exist.");
                    return;
                }

                await command.RespondAsync(
                    $"Instance '{instance}' restarting, please allow up to five minutes before yelling at your local server administrator.");
                
                try
                {
                    await Cli.Wrap("/usr/bin/podman")
                        .WithArguments(new[] { "restart", instance })
                        .ExecuteAsync();
                }
                catch (Exception e)
                {
                    UniLog.Warning($"Instance restarted with errors: {e}");
                }

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
                var contacts = _skyFrost.GetPendingFriendRequests().Where(pair => pair.Value.Any()).ToList();
                var count = contacts.Select(pair => pair.Value.Count).Sum();
                if (count == 0)
                {
                    await command.RespondAsync("There are currently no pending friend requests.");
                    return;
                }

                var response = "The following user" + (count == 1 ? " is " : "s are ") +
                               "waiting to get approved:\n";

                await command.RespondAsync(
                    string.Join("\n",
                            contacts.Select(pair =>
                                $"{pair.Key.Session.CurrentUser.Username}: \n{string.Join("\n", pair.Value.Select(contact => $"- {contact.ContactUsername}"))}"))
                        .Trim());
                break;
            }
            case "accept":
            {
                var username = command.Data.Options.First().Options.First().Value as string;
                var hits = _skyFrost.GetPendingFriendRequests()
                    .Where(pair => pair.Value.Any(contact => contact.ContactUsername == username)).ToList();

                if (!hits.Any())
                {
                    await command.RespondAsync($"Friend request for user {username} not found.");
                    return;
                }

                foreach (var hit in hits)
                {
                    var contact = hit.Value.First(c => c.ContactUsername == username);
                    await _skyFrost.AcceptFriendRequest(hit.Key, contact);
                    await command.RespondAsync(
                        $"Accepted friend request from user {contact.ContactUsername} on instance '{hit.Key.Session.CurrentUser.Username}'");
                }

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
        _dataReceiver?.Dispose();
        _skyFrost?.Dispose();
        _discordInterface?.Dispose();
    }
}