namespace ResoHelperFP;

public class BotConfig
{
    public required List<ResoniteConfig> ResoniteConfigs { get; set; }
    public required string DiscordToken { get; set; }
    public ulong DiscordServerId { get; set; }
    public ulong DiscordLogChannelId { get; set; }
    public required List<string> InstanceNames { get; set; }
}