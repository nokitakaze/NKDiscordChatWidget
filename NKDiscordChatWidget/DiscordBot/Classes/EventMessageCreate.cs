using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NKDiscordChatWidget.DiscordBot.Classes
{
    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class EventMessageCreate
    {
        public int type;
        public bool tts;
        public string timestamp;
        public string edited_timestamp;
        public bool pinned;
        public string nonce;
        public List<EventMessageCreate_Mention> mentions;
        public List<dynamic> mention_roles;
        public Dictionary<string, object> member;

        // https://discordapp.com/developers/docs/resources/channel#embed-object
        public List<EventMessageCreate_Embed> embeds;
        public bool mention_everyone;
        public string id;
        public string content;
        public string channel_id;
        public EventMessageCreate_Author author;
        public List<EventMessageCreate_Attachment> attachments;
        public string guild_id;

        // https://discordapp.com/developers/docs/resources/channel#reaction-object
        public List<dynamic> reactions;

        public DateTime timestampAsDT => DateTime.TryParse(this.timestamp, out var dt)
            ? dt.ToUniversalTime()
            : DateTime.MinValue;

        public DateTime edited_timestampAsDT => DateTime.TryParse(this.edited_timestamp, out var dt)
            ? dt.ToUniversalTime()
            : DateTime.MinValue;

        // ReSharper disable once ClassNeverInstantiated.Global
        public class EventMessageCreate_Mention
        {
            public string username;
            public dynamic member;
            public string id;
            public string discriminator;
            public bool bot;
            public string avatar;
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/user#user-object
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class EventMessageCreate_Author
        {
            public string username;
            public string id;
            public string discriminator;
            public string avatar;

            public string avatarURL =>
                string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png",
                    this.id,
                    this.avatar
                );
        }

        // ReSharper disable once ClassNeverInstantiated.Global
        public class EventMessageCreate_Attachment
        {
            public int width;
            public int height;
            public string url;
            public int size;
            public string proxy_url;
            public string id;
            public string avatar;
            public string filename;

            // @todo url
            /*
            public string avatarURL =>
                string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png",
                    this.id,
                    this.avatar
                );
            */
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-structure
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class EventMessageCreate_Embed
        {
            public string url;
            public string type;
            public string title;
            public string description;
            public long color;
            public Embed_Author author;
            public Embed_Provider provider;
            public Embed_Thumbnail thumbnail;
            public Embed_Video video;
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-video-structure
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class Embed_Video
        {
            public string url;
            public string proxy_url;
            public int width;
            public int height;
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-thumbnail-structure
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class Embed_Thumbnail
        {
            public string url;
            public string proxy_url;
            public int width;
            public int height;
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-provider-structure
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class Embed_Provider
        {
            public string url;
            public string name;
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-author-structure
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class Embed_Author
        {
            public string url;
            public string name;
            public string icon_url;
            public string proxy_icon_url;
        }
    }
}