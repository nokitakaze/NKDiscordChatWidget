namespace NKDiscordChatWidget.DiscordBot.Classes
{
    /// <summary>
    /// https://discordapp.com/developers/docs/topics/permissions#role-object
    /// </summary>
    public class Role
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