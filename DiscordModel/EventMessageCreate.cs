using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;

namespace NKDiscordChatWidget.DiscordModel;

// ReSharper disable ClassNeverInstantiated.Global
/// <summary>
/// https://discordapp.com/developers/docs/resources/channel#message-object
/// </summary>
[SuppressMessage("ReSharper", "UnassignedField.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class EventMessageCreate
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#message-object-message-types
    /// </summary>
    [JsonProperty(PropertyName = "type")]
    public int Type;

    /// <summary>
    /// Text-to-speech. Встроенная в Discord говорилка прочитает текст
    /// </summary>
    [JsonProperty(PropertyName = "tts")]
    public bool Tts;
        
    [JsonProperty(PropertyName = "timestamp")]
    public string Timestamp;
        
    [JsonProperty(PropertyName = "edited_timestamp")]
    public string EditedTimestamp;
        
    [JsonProperty(PropertyName = "pinned")]
    public bool Pinned;
        
    [JsonProperty(PropertyName = "nonce")]
    public string Nonce;
        
    [JsonProperty(PropertyName = "mentions")]
    public List<EventMessageCreate_Mention> Mentions;
        
    [JsonProperty(PropertyName = "mention_roles")]
    public List<string> MentionRoles;
        
    [JsonProperty(PropertyName = "member")]
    public EventMessageCreate_Member Member;

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#embed-object
    /// </summary>
    [JsonProperty(PropertyName = "embeds")]
    public List<EventMessageCreate_Embed> Embeds;
        
    [JsonProperty(PropertyName = "mention_everyone")]
    public bool MentionEveryone;
        
    [JsonProperty(PropertyName = "id")]
    public string Id;
        
    [JsonProperty(PropertyName = "content")]
    public string Content;
        
    [JsonProperty(PropertyName = "channel_id")]
    public string ChannelId;
        
    [JsonProperty(PropertyName = "author")]
    public EventMessageCreate_Author Author;
        
    [JsonProperty(PropertyName = "attachments")]
    public List<EventMessageCreate_Attachment> Attachments;
        
    [JsonProperty(PropertyName = "guild_id")]
    public string GuildId;

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#reaction-object
    /// </summary>
    [JsonProperty(PropertyName = "reactions")]
    public List<EventMessageCreate_Reaction> Reactions;

    [JsonProperty(PropertyName = "sticker_items")]
    public List<EventMessageCreate_Sticker> StickerItems;

    public DateTime timestampAsDT 
        => DateTime.TryParse(Timestamp, out var dt) 
            ? dt.ToUniversalTime() 
            : DateTime.MinValue;

    public DateTime edited_timestampAsDT 
        => DateTime.TryParse(EditedTimestamp, out var dt) 
            ? dt.ToUniversalTime() 
            : DateTime.MinValue;

    public void FixUp()
    {
        Reactions ??= new List<EventMessageCreate_Reaction>();

        foreach (var reaction in Reactions)
        {
            reaction.CountOffset = reaction.Count;
        }
    }

    public void AddReaction(Reaction reaction)
    {
        EventMessageCreate_Reaction foundReaction = Reactions
            .FirstOrDefault(existReaction => existReaction.Emoji.IsEqual(reaction.Emoji));

        if (foundReaction == null)
        {
            foundReaction = new EventMessageCreate_Reaction
            {
                CountOffset = 0, Count = 0, Emoji = reaction.Emoji, Me = false,
            };
            Reactions.Add(foundReaction);
        }

        var isFound = foundReaction.UserId.Any(userId => userId == reaction.UserId);

        if (isFound) return;
            
        foundReaction.Count++;
        foundReaction.UserId.Add(reaction.UserId);
    }

    public void RemoveReaction(Reaction reaction)
    {
        var foundReaction = Reactions
            .FirstOrDefault(existReaction => existReaction.Emoji.IsEqual(reaction.Emoji));

        if (foundReaction == null)
        {
            return;
        }

        foundReaction.Count = Math.Max(foundReaction.Count - 1, 0);
        var isFound = foundReaction.UserId.Any(userId => userId == reaction.UserId);

        if (isFound)
        {
            foundReaction.UserId.Remove(reaction.UserId);
        }
        else
        {
            // Реакция не найдена, очевидно она была добавлена раньше начала чтения,
            // поэтому просто уменьшаем начальный оффсет количества 
            foundReaction.CountOffset--;
        }

        if (foundReaction.Count == 0)
        {
            Reactions.Remove(foundReaction);
        }
    }

    public class EventMessageCreate_Mention : User
    {
        [JsonProperty(PropertyName = "member")]
        public GuildMember Member;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/user#user-object
    /// </summary>
    public class EventMessageCreate_Author
    {
        [JsonProperty(PropertyName = "username")]
        public string Username;
            
        [JsonProperty(PropertyName = "id")]
        public string Id;
            
        [JsonProperty(PropertyName = "discriminator")]
        public string Discriminator;

        /// <summary>
        /// Avatar hash
        /// </summary>
        /// <description>
        /// In the case of endpoints that support GIFs, the hash will begin with a_
        /// if it is available in GIF format. (example: a_1269e74af4df7417b13759eae50c83dc)
        /// </description>
        [JsonProperty(PropertyName = "avatar")]
        public string Avatar;

        // Нет аватарки, отображаем Дефолтную
        // todo Найти оригинальный URL с CDN Дискорда
        public string avatarURL
            => Avatar.IsNullOrEmpty()
                ? "https://nktkz.s3.eu-central-1.amazonaws.com/cdn/discord-widget/322c936a8c8be1b803cd94861bdfa868.png"
                : Avatar[..2] == "a_"
                    ? $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.gif"
                    : $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.png";
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#attachment-object
    /// </summary>
    public class EventMessageCreate_Attachment
    {
        [JsonProperty(PropertyName = "width")]
        public int Width;
            
        [JsonProperty(PropertyName = "height")]
        public int Height;
            
        [JsonProperty(PropertyName = "url")]
        public string Url;
            
        [JsonProperty(PropertyName = "size")]
        public int Size;
            
        [JsonProperty(PropertyName = "proxy_url")]
        public string ProxyUrl;
            
        [JsonProperty(PropertyName = "id")]
        public string Id;
            
        [JsonProperty(PropertyName = "avatar")]
        public string Avatar;
            
        [JsonProperty(PropertyName = "filename")]
        public string Filename;

        // @todo url
        /*
        public string avatarURL =>
            string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png",
                this.id,
                this.avatar
            );
        */

        public bool IsSpoiler => Url
            .Split('/')
            .Last()
            .StartsWith("SPOILER_");

        public override string ToString() 
            => Id + " / " + Url;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/guild#guild-member-object
    /// </summary>
    public class EventMessageCreate_Member
    {
        [JsonProperty(PropertyName = "user")]
        public dynamic User;
            
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

        public override string ToString()
            => Nick;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-structure
    /// </summary>
    public class EventMessageCreate_Embed
    {
        [JsonProperty(PropertyName = "url")]
        public string Url;
            
        [JsonProperty(PropertyName = "type")]
        public string Type;
            
        [JsonProperty(PropertyName = "title")]
        public string Title;
            
        [JsonProperty(PropertyName = "description")]
        public string Description;
            
        [JsonProperty(PropertyName = "color")]
        public long Color;
            
        [JsonProperty(PropertyName = "author")]
        public Embed_Author Author;
            
        [JsonProperty(PropertyName = "provider")]
        public Embed_Provider Provider;
            
        [JsonProperty(PropertyName = "thumbnail")]
        public Embed_Thumbnail Thumbnail;
            
        [JsonProperty(PropertyName = "video")]
        public Embed_Video Video;

        public override string ToString()
            => Title;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-video-structure
    /// </summary>
    public class Embed_Video
    {
        [JsonProperty(PropertyName = "url")]
        public string Url;
            
        [JsonProperty(PropertyName = "proxy_url")]
        public string ProxyUrl;
            
        [JsonProperty(PropertyName = "width")]
        public int Width;
            
        [JsonProperty(PropertyName = "height")]
        public int Height;

        public override string ToString()
            => ProxyUrl;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-thumbnail-structure
    /// </summary>
    public class Embed_Thumbnail
    {
        [JsonProperty(PropertyName = "url")]
        public string Url;
            
        [JsonProperty(PropertyName = "proxy_url")]
        public string ProxyUrl;
            
        [JsonProperty(PropertyName = "width")]
        public int Width;
            
        [JsonProperty(PropertyName = "height")]
        public int Height;

        public override string ToString()
            => ProxyUrl;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-provider-structure
    /// </summary>
    public class Embed_Provider
    {
        [JsonProperty(PropertyName = "url")]
        public string Url;
            
        [JsonProperty(PropertyName = "name")]
        public string Name;

        public override string ToString() 
            => Name;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#embed-object-embed-author-structure
    /// </summary>
    public class Embed_Author
    {
        [JsonProperty(PropertyName = "url")]
        public string Url;
            
        [JsonProperty(PropertyName = "name")]
        public string Name;
            
        [JsonProperty(PropertyName = "icon_url")]
        public string IconUrl;
            
        [JsonProperty(PropertyName = "proxy_icon_url")]
        public string ProxyIconUrl;

        public override string ToString()
            => Name;
    }

    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#reaction-object
    /// </summary>
    public class EventMessageCreate_Reaction
    {
        [JsonProperty(PropertyName = "__count_offset")]
        public int CountOffset;
            
        [JsonProperty(PropertyName = "userID")]
        public readonly List<string> UserId = new();

        [JsonProperty(PropertyName = "count")]
        public int Count;
            
        [JsonProperty(PropertyName = "me")]
        public bool Me;
            
        [JsonProperty(PropertyName = "emoji")]
        public Emoji Emoji;

        public override string ToString()
            => Emoji.ToString();
    }

    /// <summary>
    /// https://discord.com/developers/docs/resources/sticker#sticker-item-object
    /// </summary>
    public class EventMessageCreate_Sticker
    {
        [JsonProperty(PropertyName = "id")]
        public string Id;
            
        [JsonProperty(PropertyName = "name")]
        public string Name;
            
        [JsonProperty(PropertyName = "format_type")]
        public Type FormatType;

        public string Url
            => FormatType != Type.PNG 
                ? null // todo: другие форматы
                : $"https://media.discordapp.net/stickers/{Id}.png?size=512";
            

        public enum Type
        {
            PNG = 1,
            APNG = 2,
            LOTTIE = 3,
        }
    }
}
// ReSharper restore ClassNeverInstantiated.Global