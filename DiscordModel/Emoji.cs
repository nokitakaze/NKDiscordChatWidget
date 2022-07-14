using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NKDiscordChatWidget.DiscordModel
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/emoji
    /// </summary>
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    public class Emoji
    {
        public string id;
        public string name;
        public List<dynamic> roles;
        public dynamic user;
        public bool require_colons;
        public bool managed;
        public bool animated;

        public string URL => string.Format("https://cdn.discordapp.com/emojis/{0}.{1}",
            this.id, this.animated ? "gif" : "png");

        public bool IsEqual(Emoji other)
        {
            if (this.id == null)
            {
                return ((other.id == null) && (this.name == other.name));
            }

            return (other.id == this.id);
        }

        public override string ToString()
        {
            return id ?? this.name;
        }
    }
}