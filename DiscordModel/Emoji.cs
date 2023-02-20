using System.Collections.Generic;
using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel;

/// <summary>
/// https://discordapp.com/developers/docs/resources/emoji
/// </summary>
public class Emoji
{
    [JsonProperty(PropertyName = "id")]
    public string Id;
        
    [JsonProperty(PropertyName = "name")]
    public string Name;
        
    [JsonProperty(PropertyName = "roles")]
    public List<dynamic> Roles;
        
    [JsonProperty(PropertyName = "user")]
    public dynamic User;
        
    [JsonProperty(PropertyName = "require_colons")]
    public bool RequireColons;
        
    [JsonProperty(PropertyName = "managed")]
    public bool Managed;
        
    [JsonProperty(PropertyName = "animated")]
    public bool Animated;

    public string URL => $"https://cdn.discordapp.com/emojis/{Id}.{(Animated ? "gif" : "png")}";

    public bool IsEqual(Emoji other) 
        => Id == null 
            ? other.Id == null && Name == other.Name
            : other.Id == Id;
    
    public override string ToString()
        => Id ?? Name;
}