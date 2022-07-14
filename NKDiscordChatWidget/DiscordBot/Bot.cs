using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NKDiscordChatWidget.DiscordBot.Classes;
using NKDiscordChatWidget.General;
using NKDiscordChatWidget.WidgetServer;

namespace NKDiscordChatWidget.DiscordBot
{
    // todo Перенести в BackgroundService
    public static class Bot
    {
        private static ClientWebSocket wsClient;
        private static ulong websocketSequenceId;
        private static string sessionID;
        private static volatile int msBetweenPing = 10000;
        private static DateTime lastIncomingMessageTime = DateTime.MinValue;
        private static DateTime lastIncomingPingTime = DateTime.MinValue;

        public static ConcurrentDictionary<string, EventGuildCreate> guilds { get; } =
            new ConcurrentDictionary<string, EventGuildCreate>();

        public static ConcurrentDictionary<string,
            ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>> channels { get; } =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>>();

        public static ConcurrentDictionary<string,
            ConcurrentDictionary<string, ConcurrentDictionary<string, EventMessageCreate>>> messages { get; } =
            new ConcurrentDictionary<string,
                ConcurrentDictionary<string, ConcurrentDictionary<string, EventMessageCreate>>>();

        public static async Task StartTask()
        {
            var options = NKDiscordChatWidget.General.Global.ProgramOptions;
            string wsBaseUrl;
            {
                Console.WriteLine("Load https://discordapp.com/api/gateway/bot");
                while (true)
                {
                    try
                    {
                        // https://discordapp.com/developers/docs/topics/gateway#get-gateway-bot
                        using (var client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("Authorization",
                                "Bot " + options.DiscordBotToken);

                            client.DefaultRequestHeaders
                                .Accept
                                .Add(new MediaTypeWithQualityHeaderValue("application/json")); //ACCEPT header

                            var response = await client.GetAsync("https://discordapp.com/api/gateway/bot");
                            Console.WriteLine("Load gateway/bot loaded");
                            var rawAnswer = await response.Content.ReadAsStringAsync();
                            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawAnswer);
                            if (!dict.ContainsKey("url"))
                            {
                                var message = dict["message"] as string;
                                Console.WriteLine("Can not connect to bot {0}", message);
                            }
                            else
                            {
                                wsBaseUrl = dict["url"] as string;
                                break;
                            }
                        }
                    }
                    catch (Exception wsException)
                    {
                        Console.Error.WriteLine("wsException: {0}", wsException);
                    }
                }
            }

            Console.WriteLine("Discord websocket base url: {0}", wsBaseUrl);
#pragma warning disable 4014
            Task.Run(async () => { await heartBeat(); });
#pragma warning restore 4014

            do
            {
                try
                {
                    using (wsClient = new ClientWebSocket())
                    {
                        var fullConnectURI = string.Format("{0}?v=6&encoding=json", wsBaseUrl);
                        await wsClient.ConnectAsync(new Uri(fullConnectURI), Global.globalCancellationToken);
                        await ProcessWebSocket();
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            } while (!Global.globalCancellationToken.IsCancellationRequested);
        }

        private static async Task SendMessageToWebSocket(object data)
        {
            await SendMessageToWebSocket(JsonConvert.SerializeObject(data));
        }

        private static async Task SendMessageToWebSocket(string message)
        {
            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            await wsClient.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task ProcessWebSocket()
        {
            byte[] b = new byte[10 * 1024 * 1024];
            do
            {
                if (wsClient.State != WebSocketState.Open)
                {
                    Console.WriteLine("socket closed");
                    break;
                }

                ArraySegment<byte> bytesReceived = new ArraySegment<byte>(b);
                WebSocketReceiveResult result = await wsClient.ReceiveAsync(bytesReceived, CancellationToken.None);

                if (result.Count > 0)
                {
                    var dest = new byte[result.Count];
                    Array.Copy(b, dest, result.Count);
                    var s = Encoding.UTF8.GetString(dest);
                    var message = JsonConvert.DeserializeObject<DiscordWebSocketMessage>(s);
                    lastIncomingMessageTime = DateTime.Now.ToUniversalTime();
#pragma warning disable 4014
                    Ws_OnMessage(message);
#pragma warning restore 4014
                }
                else
                {
                    Thread.Sleep(100);
                }
            } while (true);
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class DiscordWebSocketMessage
        {
#pragma warning disable 649
            public string t;
            public int? s;
            public int op;
            public dynamic d;
#pragma warning restore 649

            public string dAsString => JsonConvert.SerializeObject(this.d);
        }

        private static async Task heartBeat()
        {
            while (!Global.globalCancellationToken.IsCancellationRequested)
            {
                if (wsClient.State == WebSocketState.Open)
                {
                    string id = string.Format("{0}\"op\": 1,\"d\": {1}{2}", '{', websocketSequenceId, '}');
                    await SendMessageToWebSocket(id);
                    Thread.Sleep(msBetweenPing);
                    continue;
                }

                Thread.Sleep(100);
            }
        }

        private static async Task Ws_OnMessage(DiscordWebSocketMessage message)
        {
            if (message.s != null)
            {
                websocketSequenceId = Convert.ToUInt64(message.s);
            }

            Console.WriteLine("{0}\top = {1,3}\t{2,-30}\t{3} chars",
                DateTime.Now.ToUniversalTime(),
                message.op,
                message.t,
                message.d.Lenght
            );

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (message.op)
            {
                case 10:
                    int interval = message.d.heartbeat_interval;
                    msBetweenPing = interval;
                    Dictionary<string, object> helloMessageData;
                    if ((sessionID != null) && false)
                    {
                        helloMessageData = new Dictionary<string, object>()
                        {
                            ["op"] = 2,
                            ["d"] = new Dictionary<string, object>()
                            {
                                ["token"] = Global.ProgramOptions.DiscordBotToken,
                                ["session_id"] = sessionID,
                                ["seq"] = websocketSequenceId,
                            },
                        };
                    }
                    else
                    {
                        helloMessageData = new Dictionary<string, object>()
                        {
                            ["op"] = 2,
                            ["d"] = new Dictionary<string, object>()
                            {
                                ["token"] = Global.ProgramOptions.DiscordBotToken,
                                ["properties"] = new Dictionary<string, object>()
                                {
                                    ["$os"] = System.Environment.OSVersion.ToString(),
                                },
                                ["compress"] = false,
                                ["large_threshold"] = 250,
                            },
                        };
                    }

                    await SendMessageToWebSocket(helloMessageData);
                    break;
                case 11:
                    // Incoming ping
                    lastIncomingPingTime = DateTime.Now.ToUniversalTime();
                    break;
                case 0:
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (message.t)
                    {
                        case "READY":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#ready
                            sessionID = message.d.session_id;
                            JsonConvert.SerializeObject(message.d);
                            break;
                        }
                        case "GUILD_CREATE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#guild-create
                            var guild = JsonConvert.DeserializeObject<EventGuildCreate>(message.dAsString);
                            guilds[guild.id] = guild;
                            if (!channels.ContainsKey(guild.id))
                            {
                                channels[guild.id] =
                                    new ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>();
                            }

                            foreach (var channel in guild.channels)
                            {
                                channels[guild.id][channel.id] = channel;
                            }

                            break;
                        }
                        case "MESSAGE_CREATE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-create
                            // https://discordapp.com/developers/docs/resources/channel#message-object
                            var messageCreate = JsonConvert.DeserializeObject<EventMessageCreate>(message.dAsString);
                            messageCreate.FixUp();
                            if (messageCreate.type != 0)
                            {
                                break;
                            }

                            if (!messages.ContainsKey(messageCreate.guild_id))
                            {
                                messages[messageCreate.guild_id] = new ConcurrentDictionary<string,
                                    ConcurrentDictionary<string, EventMessageCreate>>();
                            }

                            if (!messages[messageCreate.guild_id].ContainsKey(messageCreate.channel_id))
                            {
                                messages[messageCreate.guild_id][messageCreate.channel_id] =
                                    new ConcurrentDictionary<string, EventMessageCreate>();
                            }

                            messages[messageCreate.guild_id][messageCreate.channel_id][messageCreate.id] =
                                messageCreate;
                            WebsocketClientSide.UpdateMessage(messageCreate);

                            Console.WriteLine("channel_id {0} user {1} say: {2}",
                                messageCreate.channel_id,
                                messageCreate.author.username,
                                messageCreate.content
                            );
                            break;
                        }
                        case "MESSAGE_UPDATE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-update
                            // https://discordapp.com/developers/docs/resources/channel#message-object
                            var messageUpdate = JsonConvert.DeserializeObject<EventMessageCreate>(message.dAsString);
                            if (
                                !messages.ContainsKey(messageUpdate.guild_id) ||
                                !messages[messageUpdate.guild_id].ContainsKey(messageUpdate.channel_id) ||
                                !messages[messageUpdate.guild_id][messageUpdate.channel_id]
                                    .ContainsKey(messageUpdate.id)
                            )
                            {
                                // Такого сообщения нет. Возможно, оно было создано раньше. Это не проблема 
                                break;
                            }

                            var existedMessage =
                                messages[messageUpdate.guild_id][messageUpdate.channel_id][messageUpdate.id];

                            // При обновлении сообщения Дискорд пропускает контент, если контент не обновляется,
                            // поэтому тут нужен ===null предикат
                            var fields = new[] { "content", "embeds", "attachments", "mention_roles", "mentions" };
                            foreach (var fieldName in fields)
                            {
                                var field = typeof(EventMessageCreate).GetField(fieldName);
                                var newValue = field.GetValue(messageUpdate);
                                if (newValue == null)
                                {
                                    continue;
                                }

                                field.SetValue(existedMessage, newValue);
                            }

                            existedMessage.edited_timestamp = messageUpdate.edited_timestamp;
                            existedMessage.mention_everyone = messageUpdate.mention_everyone;
                            existedMessage.pinned = messageUpdate.pinned;
                            WebsocketClientSide.UpdateMessage(existedMessage);

                            Console.WriteLine("channel_id {0} user {1} edited: {2}",
                                messageUpdate.channel_id,
                                messageUpdate.author.username,
                                messageUpdate.content
                            );
                            break;
                        }
                        case "CHANNEL_CREATE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#channel-create
                            var messageChannelCreate =
                                JsonConvert.DeserializeObject<EventGuildCreate.EventChannelCreate>(message.dAsString);
                            if (!channels.ContainsKey(messageChannelCreate.guild_id))
                            {
                                channels[messageChannelCreate.guild_id] =
                                    new ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>();
                            }

                            channels[messageChannelCreate.guild_id][messageChannelCreate.id] = messageChannelCreate;

                            break;
                        }
                        case "MESSAGE_DELETE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-delete
                            var guild_id = message.d.guild_id.ToString() as string;
                            var channel_id = message.d.channel_id.ToString() as string;
                            if (
                                messages.ContainsKey(guild_id) &&
                                messages[guild_id].ContainsKey(channel_id)
                            )
                            {
                                var messageId = message.d.id.ToString() as string;
                                messages[guild_id][channel_id].TryRemove(messageId, out _);
                                WebsocketClientSide.RemoveMessage(guild_id, channel_id, messageId);
                            }

                            // Такого сообщения нет. Возможно, оно было создано раньше. Это не проблема 
                            break;
                        }
                        case "MESSAGE_REACTION_ADD":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-reaction-add
                            var reaction = JsonConvert.DeserializeObject<Reaction>(message.dAsString);
                            if (
                                !messages.ContainsKey(reaction.guild_id) ||
                                !messages[reaction.guild_id].ContainsKey(reaction.channel_id) ||
                                !messages[reaction.guild_id][reaction.channel_id].ContainsKey(reaction.message_id)
                            )
                            {
                                break;
                            }

                            messages[reaction.guild_id][reaction.channel_id][reaction.message_id]
                                .AddReaction(reaction);
                            WebsocketClientSide.UpdateMessage(
                                messages[reaction.guild_id][reaction.channel_id][reaction.message_id]);

                            break;
                        }
                        case "MESSAGE_REACTION_REMOVE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-reaction-remove
                            var reaction = JsonConvert.DeserializeObject<Reaction>(message.dAsString);
                            if (
                                !messages.ContainsKey(reaction.guild_id) ||
                                !messages[reaction.guild_id].ContainsKey(reaction.channel_id) ||
                                !messages[reaction.guild_id][reaction.channel_id].ContainsKey(reaction.message_id)
                            )
                            {
                                break;
                            }

                            messages[reaction.guild_id][reaction.channel_id][reaction.message_id]
                                .RemoveReaction(reaction);
                            WebsocketClientSide.UpdateMessage(
                                messages[reaction.guild_id][reaction.channel_id][reaction.message_id]);

                            break;
                        }
                        case "GUILD_MEMBER_ADD":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#guild-member-add
                            break;
                        }
                        case "GUILD_BAN_ADD":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#guild-ban-add
                            var banAdd = JsonConvert.DeserializeObject<EventGuildBanAdd>(message.dAsString);
                            if (!messages.ContainsKey(banAdd.guild_id))
                            {
                                break;
                            }

                            var userId = banAdd.user.id;

                            foreach (var (channelId, channelsMessages) in messages[banAdd.guild_id])
                            {
                                var forDeletions = new List<string>();
                                foreach (var (messageId, messageItem) in channelsMessages)
                                {
                                    if (messageItem.author.id == userId)
                                    {
                                        forDeletions.Add(messageId);
                                    }
                                }

                                foreach (var messageId in forDeletions)
                                {
                                    channelsMessages.TryRemove(messageId, out _);
                                    WebsocketClientSide.RemoveMessage(banAdd.guild_id, channelId, messageId);
                                }
                            }

                            break;
                        }
                        // 
                        // @todo GUILD_EMOJIS_UPDATE
                    }

                    break;
            }
        }
    }
}