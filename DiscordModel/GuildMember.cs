using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NKDiscordChatWidget.DiscordModel
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/guild#guild-member-object
    /// </summary>
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    public class GuildMember
    {
        public User user;
        public string nick;
        public List<string> roles;
        public string joined_at;
        public bool deaf;
        public bool mute;
    }
}