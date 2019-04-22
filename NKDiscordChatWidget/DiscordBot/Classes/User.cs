using System.Diagnostics.CodeAnalysis;

namespace NKDiscordChatWidget.DiscordBot.Classes
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/user#user-object
    /// </summary>
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    public class User
    {
        public string id;
        public string username;
        public string discriminator;
        public string avatar;
        public bool bot;
        public bool mfa_enabled;
        public string locale;
        public string email;
        public bool verified;
        public long flags;
        public long premium_type;
    }
}