using System.Configuration;
using System.Globalization;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Elements.Core;
using Newtonsoft.Json;
using SkyFrost.Base;

namespace ResoHelperFP;

public class DiscordInterface
{
    private static float updateTimeout = 5;
    private Timer? _timeout;
    private Dictionary<string, SessionInfo>? _queuedSessionInfos;
    private readonly DiscordSocketClient _client = new();
    private readonly BotConfig _config;

    public DiscordInterface(BotConfig config)
    {
        _config = config;
    }

    public event Func<Task> Ready
    {
        add => _client.Ready += value;
        remove => _client.Ready -= value;
    }

    public async Task MainAsync()
    {
        _client.Log += Log;

        if (_config.DiscordToken == null)
        {
            throw new ConfigurationErrorsException("Failed to get discord bot token from config.json");
        }

        await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
        await _client.StartAsync();
        _client.SlashCommandExecuted += SlashCommandHandler;
        Ready += OnDiscordReady;
    }
    
    public static int GetIso8601WeekOfYear(DateTime time)
    {
        // Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll 
        // be the same week# as whatever Thursday, Friday or Saturday are,
        // and we always get those right
        DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            time = time.AddDays(3);
        }

        // Return the week of our adjusted day
        return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
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
            default:
            {
                return;
            }
        }
    }
    
    private async Task OnDiscordReady()
    {
        var guild = _client.GetGuild(_config.DiscordServerId);
        var weekCommand = new SlashCommandBuilder();
        weekCommand.WithName("week");
        weekCommand.WithDescription("Get the current week type for furpunch Resonite sessions.");

        try
        {
            await guild.CreateApplicationCommandAsync(weekCommand.Build());
        }
        catch(HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            UniLog.Error(json);
        }
    }
    
    public void Dispose()
    {
        _client.Dispose();
    }

    public void SetSessionInfoBuffered(Dictionary<string, SessionInfo> sessionInfos)
    {
        if (_timeout == null)
        {
            _queuedSessionInfos = sessionInfos;
            _timeout = new Timer(async _ =>
            {
                await SetSessionInfo(_queuedSessionInfos);
                _queuedSessionInfos = null;
                _timeout = null;
            }, null, dueTime: TimeSpan.FromSeconds(updateTimeout), period: Timeout.InfiniteTimeSpan);
            return;
        }

        _queuedSessionInfos = sessionInfos;
    }

    private async Task SetSessionInfo(Dictionary<string, SessionInfo>? sessionInfos)
    {
        var status = "";
        if (sessionInfos != null)
        {
            status = string.Join(" | ",
                sessionInfos.Select(info =>
                    $"{info.Value.Name.Replace("[fp]", "").Trim()}: {info.Value.ActiveUsers}"));
        }

        UniLog.Log($"Updating Status: {status}");
        await _client.SetActivityAsync(new CustomStatusGame(status));
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}