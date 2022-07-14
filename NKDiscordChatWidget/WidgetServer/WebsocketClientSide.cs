using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NKDiscordChatWidget.DiscordModel;
using System.Linq;
using NKDiscordChatWidget.General;
using NKDiscordChatWidget.Services;

namespace NKDiscordChatWidget.WidgetServer
{
    /// <summary>
    /// На этот end point приходит фактически один запрос: смена настроек отображения чата
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public class WebsocketClientSide : Hub
    {
        public readonly ChatDrawOption ChatDrawOption = new ChatDrawOption();
        public readonly WebsocketClientSidePool Pool;
        private readonly DiscordRepository Repository;
        private readonly MessageArtist MessageArtist;

        public WebsocketClientSide(
            WebsocketClientSidePool pool,
            DiscordRepository repository,
            MessageArtist messageArtist
        )
        {
            Pool = pool;
            Repository = repository;
            MessageArtist = messageArtist;
        }

        public string GuildID { get; private set; }
        public string ChannelID { get; private set; }

        #region ClientSideSignals

        /// <summary>
        /// Смена настроек отображения чата
        /// </summary>
        /// <param name="jsonOptions"></param>
        /// <returns></returns>
        // ReSharper disable once UnusedMember.Global
        public void ChangeDrawOptionAndGetAllMessages(string jsonOptions)
        {
            Pool._clients[this.Context.ConnectionId] = this;

            Dictionary<string, object> newChatDrawOption;
            try
            {
                newChatDrawOption = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonOptions);
            }
            catch (JsonException e)
            {
                Console.Error.WriteLine(e);
                return;
            }

            ChatDrawOption.SetOptionsFromDictionary(newChatDrawOption);

            GuildID = newChatDrawOption["guild"] as string;
            ChannelID = newChatDrawOption["channel"] as string;

            {
                var answer = new AnswerFull
                {
                    channel_title = string.Format("Discord Widget Chat: {0}-{1} [nkt]", GuildID, ChannelID),
                };

                List<EventMessageCreate> messages;
                if (Repository.messages.ContainsKey(GuildID) &&
                    (Repository.messages[GuildID].ContainsKey(ChannelID)))
                {
                    messages = Repository.messages[GuildID][ChannelID].Values.ToList();
                }
                else
                {
                    // Такого сервера/канала не существует или там нет ни одного сообщения и никогда не было
                    messages = new List<EventMessageCreate>();
                }

                messages.Sort((a, b) => a.timestampAsDT.CompareTo(b.timestampAsDT));
                for (var i = Math.Max(messages.Count - 1000, 0); i < messages.Count; i++)
                {
                    answer.existedID.Add(messages[i].id);
                }

                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < messages.Count; i++)
                {
                    // @todo не работает мердж сообщений от одного пользователя
                    var message = messages[i];
                    var localAnswerMessage = MessageArtist.DrawMessage(message, ChatDrawOption);
                    answer.messages.Add(localAnswerMessage);
                }

                answer.time_answer = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds() * 0.001d;

                Pool.HubContext.Clients.Client(this.Context.ConnectionId)
                    .SendCoreAsync("ReceiveFullMessageList", new[]
                    {
                        answer,
                    });
            }
        }

        #endregion
    }
}