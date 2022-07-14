using System.Diagnostics.CodeAnalysis;

namespace NKDiscordChatWidget.DiscordModel
{
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    public class Reaction
    {
        public string user_id;
        public string channel_id;
        public string message_id;
        public string guild_id;
        public Emoji emoji;
    }
}