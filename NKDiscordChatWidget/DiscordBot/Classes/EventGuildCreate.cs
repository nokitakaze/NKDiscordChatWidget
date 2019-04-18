using System.Collections.Generic;

namespace NKDiscordChatWidget.DiscordBot.Classes
{
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

        public class EventGuildCreate_Channel
        {
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
    }
}