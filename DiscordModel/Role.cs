using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel
{
    /// <summary>
    /// https://discordapp.com/developers/docs/topics/permissions#role-object
    /// </summary>
    public class Role
    {
        [JsonProperty(PropertyName = "id")]
        public string Id;
        
        [JsonProperty(PropertyName = "name")]
        public string Name;
        
        [JsonProperty(PropertyName = "color")]
        public long Color;
        
        [JsonProperty(PropertyName = "hoist")]
        public bool Hoist;
        
        [JsonProperty(PropertyName = "position")]
        public int Position;
        
        [JsonProperty(PropertyName = "permissions")]
        public long Permissions;
        
        [JsonProperty(PropertyName = "managed")]
        public bool Managed;
        
        [JsonProperty(PropertyName = "mentionable")]
        public bool Mentionable;
    }
}