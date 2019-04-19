using System.Collections.Generic;

namespace NKDiscordChatWidget.DiscordBot.Classes
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/emoji
    /// </summary>
    public class Emoji
    {
        public string id;
        public string name;
        public List<dynamic> roles;
        public dynamic user;
        public bool require_colons;
        public bool managed;
        public bool animated;

        public string URL => string.Format("https://cdn.discordapp.com/emojis/{0}.png", this.id);

        public bool IsEqual(Emoji other)
        {
            if (this.id == null)
            {
                return ((other.id == null) && (this.name == other.name));
            }

            return (other.id == this.id);
        }
    }
}