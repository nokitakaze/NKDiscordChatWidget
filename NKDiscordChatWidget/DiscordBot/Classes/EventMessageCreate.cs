using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace NKDiscordChatWidget.DiscordBot.Classes
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#message-object
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    public class EventMessageCreate
    {
        public int type;
        public bool tts;
        public string timestamp;
        public string edited_timestamp;
        public bool pinned;
        public string nonce;
        public List<EventMessageCreate_Mention> mentions;
        public List<string> mention_roles;
        public EventMessageCreate_Member member;

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#embed-object
        /// </summary>
        public List<EventMessageCreate_Embed> embeds;

        public bool mention_everyone;
        public string id;
        public string content;
        public string channel_id;
        public EventMessageCreate_Author author;
        public List<EventMessageCreate_Attachment> attachments;
        public string guild_id;

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#reaction-object
        /// </summary>
        public List<EventMessageCreate_Reaction> reactions;

        public DateTime timestampAsDT => DateTime.TryParse(this.timestamp, out var dt)
            ? dt.ToUniversalTime()
            : DateTime.MinValue;

        public DateTime edited_timestampAsDT => DateTime.TryParse(this.edited_timestamp, out var dt)
            ? dt.ToUniversalTime()
            : DateTime.MinValue;

        public void FixUp()
        {
            if (this.reactions == null)
            {
                this.reactions = new List<EventMessageCreate_Reaction>();
            }

            foreach (var reaction in this.reactions)
            {
                reaction.__count_offset = reaction.count;
            }
        }

        public void AddReaction(Reaction reaction)
        {
            EventMessageCreate_Reaction foundReaction = this.reactions
                .FirstOrDefault(existReaction => existReaction.emoji.IsEqual(reaction.emoji));

            if (foundReaction == null)
            {
                foundReaction = new EventMessageCreate_Reaction
                {
                    __count_offset = 0, count = 0, emoji = reaction.emoji, me = false,
                };
                this.reactions.Add(foundReaction);
            }

            bool found = foundReaction.userID.Any(userId => userId == reaction.user_id);

            if (!found)
            {
                foundReaction.count++;
                foundReaction.userID.Add(reaction.user_id);
            }
        }

        public void RemoveReaction(Reaction reaction)
        {
            EventMessageCreate_Reaction foundReaction = this.reactions
                .FirstOrDefault(existReaction => existReaction.emoji.IsEqual(reaction.emoji));

            if (foundReaction == null)
            {
                return;
            }

            foundReaction.count = Math.Max(foundReaction.count - 1, 0);
            bool found = foundReaction.userID.Any(userId => userId == reaction.user_id);

            if (found)
            {
                foundReaction.userID.Remove(reaction.user_id);
            }
            else
            {
                // Реакция не найдена, очевидно она была добавлена раньше начала чтения,
                // поэтому просто уменьшаем начальный оффсет количества 
                foundReaction.__count_offset--;
            }

            if (foundReaction.count == 0)
            {
                this.reactions.Remove(foundReaction);
            }
        }

        // ReSharper disable once ClassNeverInstantiated.Global
        public class EventMessageCreate_Mention : User
        {
            public GuildMember member;
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

            public string avatarURL
            {
                get
                {
                    if ((this.avatar == null) || (this.avatar == ""))
                    {
                        // Нет аватарки, отображаем Дефолтную
                        return "https://discordapp.com/assets/322c936a8c8be1b803cd94861bdfa868.png";
                    }

                    return string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png",
                        this.id,
                        this.avatar
                    );
                }
            }
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#attachment-object
        /// </summary>
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

            public override string ToString()
            {
                return this.id + " / " + this.url;
            }
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/guild#guild-member-object
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class EventMessageCreate_Member
        {
            public dynamic user;
            public string nick;
            public List<string> roles;
            public string joined_at;
            public bool deaf;
            public bool mute;

            public override string ToString()
            {
                return this.nick;
            }
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

            public override string ToString()
            {
                return this.title;
            }
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

            public override string ToString()
            {
                return this.proxy_url;
            }
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

            public override string ToString()
            {
                return this.proxy_url;
            }
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-provider-structure
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Global
        public class Embed_Provider
        {
            public string url;
            public string name;

            public override string ToString()
            {
                return this.name;
            }
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

            public override string ToString()
            {
                return this.name;
            }
        }

        /// <summary>
        /// https://discordapp.com/developers/docs/resources/channel#reaction-object
        /// </summary>
        public class EventMessageCreate_Reaction
        {
            public int __count_offset;
            public readonly List<string> userID = new List<string>();

            public int count;
            public bool me;
            public NKDiscordChatWidget.DiscordBot.Classes.Emoji emoji;

            public override string ToString()
            {
                return emoji.ToString();
            }
        }
    }
}