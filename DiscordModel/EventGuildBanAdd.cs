using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace NKDiscordChatWidget.DiscordModel
{
    /// <summary>
    /// https://discordapp.com/developers/docs/resources/channel#guild-ban-add
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    public class EventGuildBanAdd
    {
        public string guild_id;
        public User user;
    }
}