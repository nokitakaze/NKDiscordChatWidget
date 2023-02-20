using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel;
public class Reaction
{
    [JsonProperty(PropertyName = "user_id")]
    public string UserId;
        
    [JsonProperty(PropertyName = "channel_id")]
    public string ChannelId;
        
    [JsonProperty(PropertyName = "message_id")]
    public string MessageId;
        
    [JsonProperty(PropertyName = "guild_id")]
    public string GuildId;
        
    [JsonProperty(PropertyName = "emoji")]
    public Emoji Emoji;
}