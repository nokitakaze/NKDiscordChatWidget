using System.Collections.Concurrent;
using NKDiscordChatWidget.DiscordModel;

namespace NKDiscordChatWidget.Services.Services
{
    public class DiscordRepository
    {
        public ConcurrentDictionary<string, EventGuildCreate> guilds { get; } = new();

        public ConcurrentDictionary<string,
            ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>> channels { get; } = new();

        public ConcurrentDictionary<string,
            ConcurrentDictionary<string, ConcurrentDictionary<string, EventMessageCreate>>> messages { get; } = new();
    }
}