using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using NKDiscordChatWidget.DiscordModel;

namespace NKDiscordChatWidget.Services.Services
{
    public class WebsocketClientSidePool
    {
        public readonly ConcurrentDictionary<string, WebsocketClientSide> _clients =
            new ConcurrentDictionary<string, WebsocketClientSide>();

        /// <summary>
        /// Контекст всех подключенных по WebSocket клиентов к end-point'у /websocketChat
        /// </summary>
        public IHubContext<WebsocketClientSide> HubContext { get; }

        private readonly MessageArtist MessageArtist;

        public WebsocketClientSidePool(
            IHubContext<WebsocketClientSide> hubContext,
            MessageArtist messageArtist
        )
        {
            HubContext = hubContext;
            MessageArtist = messageArtist;
        }

        /// <summary>
        /// Отправка сообщения всем клиентам
        ///
        /// Этот метод (event) вызывается, когда у нас создано/отредактировано сообщение. В редактирование
        /// входит также изменение реакций
        /// </summary>
        public void UpdateMessage(EventMessageCreate message)
        {
            UpdateMessages(new[] { message });
        }

        /// <summary>
        /// Отправка нескольких сообщений всем клиентам
        ///
        /// Этот метод (event) вызывается, когда у нас создано/отредактировано сообщение. В редактирование
        /// входит также изменение реакций
        /// </summary>
        public void UpdateMessages(ICollection<EventMessageCreate> messages)
        {
            if (!messages.Any())
            {
                return;
            }

            foreach (var (connectionId, client) in _clients.Select(item => (item.Key, item.Value)))
            {
                var localMessages = messages
                    .Where(message =>
                        (client.GuildID == message.guild_id) && (client.ChannelID == message.channel_id))
                    .ToArray();

                if (!localMessages.Any())
                {
                    continue;
                }

                // Сообщение для этого сервера (гильдии) и канала

                var answer = new AnswerFull
                {
                    channel_title = string.Format("Discord Widget Chat: {0}-{1} [nkt]",
                        client.GuildID, client.ChannelID),
                };

                foreach (var localAnswerMessage in localMessages.Select(message =>
                             MessageArtist.DrawMessage(message, client.ChatDrawOption)))
                {
                    answer.messages.Add(localAnswerMessage);
                }

                answer.time_answer = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds() * 0.001d;
                HubContext.Clients.Client(connectionId).SendCoreAsync("ReceiveMessage", new[]
                {
                    answer,
                });
            }
        }

        /// <summary>
        /// Этот метод (event) вызывается, когда у нас сообщение было удалено
        /// </summary>
        public void RemoveMessage(string guildId, string channelId, string messageId)
        {
            foreach (var (connectionId, client) in _clients.Select(item => (item.Key, item.Value)))
            {
                if ((client.GuildID != guildId) || (client.ChannelID != channelId))
                {
                    continue;
                }

                HubContext.Clients.Client(connectionId).SendCoreAsync("RemoveMessage", new[]
                {
                    messageId
                });
            }
        }

        /// <summary>
        /// Поменялся один из основных ресурсов, надо перезагрузить страницу
        /// </summary>
        /// <param name="filename">Название изменённого файла</param>
        public void ChangeResource(string filename)
        {
            foreach (var connectionId in _clients.Keys)
            {
                HubContext.Clients.Client(connectionId).SendCoreAsync("ChangeResource", new[]
                {
                    filename
                });
            }
        }
    }
}