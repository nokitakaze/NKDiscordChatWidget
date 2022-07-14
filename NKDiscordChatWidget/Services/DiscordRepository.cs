using System.Collections.Concurrent;
using NKDiscordChatWidget.DiscordModel;

namespace NKDiscordChatWidget.Services
{
    public class DiscordRepository
    {
        public ConcurrentDictionary<string, EventGuildCreate> guilds { get; } =
            new ConcurrentDictionary<string, EventGuildCreate>();

        public ConcurrentDictionary<string,
            ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>> channels { get; } =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>>();

        public ConcurrentDictionary<string,
            ConcurrentDictionary<string, ConcurrentDictionary<string, EventMessageCreate>>> messages { get; } =
            new ConcurrentDictionary<string,
                ConcurrentDictionary<string, ConcurrentDictionary<string, EventMessageCreate>>>();
    }
}