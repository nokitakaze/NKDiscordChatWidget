using System.Collections.Generic;

namespace NKDiscordChatWidget.DiscordBot.Classes
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/guild#guild-object
    /// </summary>
    public class EventGuildCreate
    {
        public string system_channel_id;
        public string id;
        public List<dynamic> presences;
        public string owner_id;
        public string name;
        public string icon;
        public string description;
        public List<EventGuildCreate_Channel> channels;
        public List<EventGuildCreate_PermissionOverwrite> permission_overwrites;
        public List<EventGuildCreate_Role> roles;
        public List<NKDiscordChatWidget.DiscordBot.Classes.Emoji> emojis;

        public string GetIconURL => string.Format("https://cdn.discordapp.com/icons/{0}/{1}.png", this.id, this.icon);

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#channel-object
        /// </summary>
        public class EventGuildCreate_Channel
        {
            /// <summary>
            /// https://discordapp.com/developers/docs/resources/channel#channel-object-channel-types
            /// </summary>
            public int type;

            public int? position;
            public string parent_id;
            public bool nsfw;
            public string id;
            public string name;
            public string last_message_id;
            public int? rate_limit_per_user;

            public override string ToString()
            {
                return string.Format("[{0}]", this.name);
            }
        }

        public class EventChannelCreate : EventGuildCreate_Channel
        {
            public string guild_id;
        }

        public class EventGuildCreate_PermissionOverwrite
        {
            public string type;
            public string id;
            public int deny;
            public int allow;
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/topics/permissions#role-object
        /// </summary>
        public class EventGuildCreate_Role
        {
            public string id;
            public string name;
            public long color;
            public bool hoist;
            public int position;
            public long permissions;
            public bool managed;
            public bool mentionable;
        }
    }
}