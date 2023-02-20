using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel;

/// <summary>
/// https://discordapp.com/developers/docs/resources/user#user-object
/// </summary>
public class User
{
    [JsonProperty(PropertyName = "id")]
    public string Id;
        
    [JsonProperty(PropertyName = "username")]
    public string Username;
        
    [JsonProperty(PropertyName = "discriminator")]
    public string Discriminator;
        
    [JsonProperty(PropertyName = "avatar")]
    public string Avatar;
        
    [JsonProperty(PropertyName = "bot")]
    public bool Bot;
        
    [JsonProperty(PropertyName = "mfa_enabled")]
    public bool MfaEnabled;
        
    [JsonProperty(PropertyName = "locale")]
    public string Locale;
        
    [JsonProperty(PropertyName = "email")]
    public string Email;
        
    [JsonProperty(PropertyName = "verified")]
    public bool Verified;
        
    [JsonProperty(PropertyName = "flags")]
    public long Flags;
        
    [JsonProperty(PropertyName = "premium_type")]
    public long PremiumType;
}