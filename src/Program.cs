using System.Configuration;
using Elements.Core;
using Newtonsoft.Json;

namespace ResoHelperFP;

internal static class Program
{
    private static bool _running = true;
    private static SkyFrostHelperInterface? _skyFrost;
    private static DiscordInterface? _discordInterface;

    private static async Task<int> Main(string[] _)
    {
        Console.CancelKeyPress += OnCancel;
        
        var config = JsonConvert.DeserializeObject<BotConfig>(await File.ReadAllTextAsync("config.json"));

        if (config == null)
        {
            throw new ConfigurationErrorsException("Failed to load config.json");
        }

        _skyFrost = new SkyFrostHelperInterface(config);
        _discordInterface = new DiscordInterface(config);
        
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
            return 1;
        }

        return 0;
    }

    private static void OnCancel(object? sender, ConsoleCancelEventArgs args)
    {
        _running = false;
        _skyFrost?.Dispose(); 
        _discordInterface?.Dispose();
    }
}