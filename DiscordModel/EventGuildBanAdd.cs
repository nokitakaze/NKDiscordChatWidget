using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel;

/// <summary>
/// https://discordapp.com/developers/docs/resources/channel#guild-ban-add
/// </summary>
public class EventGuildBanAdd
{
    [JsonProperty(PropertyName = "guild_id")]
    public string GuildId;
        
    [JsonProperty(PropertyName = "user")]
    public User User;
}