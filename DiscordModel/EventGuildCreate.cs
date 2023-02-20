using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel;

/// <summary>
/// https://discordapp.com/developers/docs/resources/guild#guild-object
/// </summary>
[SuppressMessage("ReSharper", "UnassignedField.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class EventGuildCreate
{
    [JsonProperty(PropertyName = "system_channel_id")]
    public string SystemChannelId;
        
    [JsonProperty(PropertyName = "id")]
    public string Id;
        
    [JsonProperty(PropertyName = "presences")]
    public List<dynamic> Presences;
        
    [JsonProperty(PropertyName = "owner_id")]
    public string OwnerId;
        
    [JsonProperty(PropertyName = "name")]
    public string Name;
        
    [JsonProperty(PropertyName = "icon")]
    public string Icon;
        
    [JsonProperty(PropertyName = "description")]
    public string Description;
        
    [JsonProperty(PropertyName = "channels")]
    public List<EventGuildCreate_Channel> Channels;
        
    [JsonProperty(PropertyName = "permission_overwrites")]
    public List<EventGuildCreate_PermissionOverwrite> PermissionOverwrites;
        
    [JsonProperty(PropertyName = "roles")]
    public List<Role> Roles;
        
    [JsonProperty(PropertyName = "emojis")]
    public List<Emoji> Emojis;
        
    [JsonProperty(PropertyName = "members")]
    public List<GuildMember> Members;

    public string GetIconURL => $"https://cdn.discordapp.com/icons/{Id}/{Icon}.png";

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#channel-object
    /// </summary>
    public class EventGuildCreate_Channel
    {
        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#channel-object-channel-types
        /// </summary>
        [JsonProperty(PropertyName = "type")]
        public int Type;

        [JsonProperty(PropertyName = "position")]
        public int? Position;
            
        [JsonProperty(PropertyName = "parent_id")]
        public string ParentId;
            
        [JsonProperty(PropertyName = "nsfw")]
        public bool Nsfw;
            
        [JsonProperty(PropertyName = "id")]
        public string Id;
            
        [JsonProperty(PropertyName = "name")]
        public string Name;
            
        [JsonProperty(PropertyName = "last_message_id")]
        public string LastMessageId;
            
        [JsonProperty(PropertyName = "rate_limit_per_user")]
        public int? RateLimitPerUser;

        public override string ToString()
            => $"[{Name}]";
    }

    public class EventChannelCreate : EventGuildCreate_Channel
    {
        [JsonProperty(PropertyName = "guild_id")]
        public string GuildId;
    }

    public class EventGuildCreate_PermissionOverwrite
    {
        [JsonProperty(PropertyName = "id")]
        public string Id;
            
        [JsonProperty(PropertyName = "type")]
        public string Type;
            
        [JsonProperty(PropertyName = "deny")]
        public int Deny;
            
        [JsonProperty(PropertyName = "allow")]
        public int Allow;
    }
}