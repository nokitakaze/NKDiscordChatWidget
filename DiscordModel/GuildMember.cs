using System.Collections.Generic;
using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel;

/// <summary>
/// https://discordapp.com/developers/docs/resources/guild#guild-member-object
/// </summary>
public class GuildMember
{
    [JsonProperty(PropertyName = "user")]
    public User User;
        
    [JsonProperty(PropertyName = "nick")]
    public string Nick;
        
    [JsonProperty(PropertyName = "roles")]
    public List<string> Roles;
        
    [JsonProperty(PropertyName = "joined_at")]
    public string JoinedAt;
        
    [JsonProperty(PropertyName = "deaf")]
    public bool Deaf;
        
    [JsonProperty(PropertyName = "mute")]
    public bool Mute;
}