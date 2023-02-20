using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using NKDiscordChatWidget.DiscordModel;
using NKDiscordChatWidget.Services.General;
using NKDiscordChatWidget.Services.Services;

namespace NKDiscordChatWidget.BackgroundService
{
    public class Bot : IHostedService
    {
        private ClientWebSocket wsClient;
        private ulong websocketSequenceId;
        private string sessionID;
        private volatile int msBetweenPing = 10000;
        private DateTime lastIncomingMessageTime = DateTime.MinValue;
        private DateTime lastIncomingPingTime = DateTime.MinValue;

        private readonly ProgramOptions ProgramOptions;
        private readonly WebsocketClientSidePool Pool;
        private readonly DiscordRepository Repository;

        public Bot(
            ProgramOptions programOptions,
            WebsocketClientSidePool pool,
            DiscordRepository repository
        )
        {
            ProgramOptions = programOptions;
            Pool = pool;
            Repository = repository;
        }

        #region IHostedService

        private readonly CancellationTokenSource CancellationSource = new CancellationTokenSource();
        private CancellationToken CancellationToken => CancellationSource.Token;
        private Task MainTask;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        public Task StartAsync(CancellationToken cancellationToken)
        {
            MainTask = Task.Run(StartTask);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            CancellationSource.Cancel();
            await MainTask;
        }

        #endregion

        public async Task StartTask()
        {
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
                                "Bot " + ProgramOptions.DiscordBotToken);

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
                        await wsClient.ConnectAsync(new Uri(fullConnectURI), CancellationToken);
                        await ProcessWebSocket();
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            } while (!CancellationToken.IsCancellationRequested);
        }

        private async Task SendMessageToWebSocket(object data)
        {
            await SendMessageToWebSocket(JsonConvert.SerializeObject(data));
        }

        private async Task SendMessageToWebSocket(string message)
        {
            var bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            await wsClient.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ProcessWebSocket()
        {
            byte[] b = new byte[10 * 1024 * 1024];
            do
            {
                if (wsClient.State != WebSocketState.Open)
                {
                    Console.WriteLine("socket closed");
                    break;
                }

                var bytesReceived = new ArraySegment<byte>(b);
                WebSocketReceiveResult result;
                try
                {
                    result = await wsClient.ReceiveAsync(bytesReceived, CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    throw;
                }

                if (result.Count == 0)
                {
                    // ReSharper disable once MethodSupportsCancellation
                    await Task.Delay(100);
                    continue;
                }

                var dest = new byte[result.Count];
                Array.Copy(b, dest, result.Count);
                var s = Encoding.UTF8.GetString(dest);
                var message = JsonConvert.DeserializeObject<DiscordWebSocketMessage>(s);
                lastIncomingMessageTime = DateTime.Now.ToUniversalTime();
#pragma warning disable 4014
                Ws_OnMessage(message);
#pragma warning restore 4014
            } while (!CancellationToken.IsCancellationRequested);
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

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        private async Task heartBeat()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                if (wsClient.State == WebSocketState.Open)
                {
                    string id = string.Format("{0}\"op\": 1,\"d\": {1}{2}", '{', websocketSequenceId, '}');
                    await SendMessageToWebSocket(id);
                    await Task.Delay(msBetweenPing);
                    continue;
                }

                await Task.Delay(100);
            }
        }

        private async Task Ws_OnMessage(DiscordWebSocketMessage message)
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
                        helloMessageData = new Dictionary<string, object>
                        {
                            ["op"] = 2,
                            ["d"] = new Dictionary<string, object>
                            {
                                ["token"] = ProgramOptions.DiscordBotToken,
                                ["session_id"] = sessionID,
                                ["seq"] = websocketSequenceId,
                            },
                        };
                    }
                    else
                    {
                        helloMessageData = new Dictionary<string, object>
                        {
                            ["op"] = 2,
                            ["d"] = new Dictionary<string, object>
                            {
                                ["token"] = ProgramOptions.DiscordBotToken,
                                ["properties"] = new Dictionary<string, object>
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
                            Repository.guilds[guild.Id] = guild;
                            if (!Repository.channels.ContainsKey(guild.Id))
                            {
                                Repository.channels[guild.Id] =
                                    new ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>();
                            }

                            foreach (var channel in guild.Channels)
                            {
                                Repository.channels[guild.Id][channel.Id] = channel;
                            }

                            break;
                        }
                        case "MESSAGE_CREATE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-create
                            // https://discordapp.com/developers/docs/resources/channel#message-object
                            var messageCreate = JsonConvert.DeserializeObject<EventMessageCreate>(message.dAsString);
                            messageCreate.FixUp();
                            if (messageCreate.Type != 0)
                            {
                                break;
                            }

                            if (!Repository.messages.ContainsKey(messageCreate.GuildId))
                            {
                                Repository.messages[messageCreate.GuildId] = new ConcurrentDictionary<string,
                                    ConcurrentDictionary<string, EventMessageCreate>>();
                            }

                            if (!Repository.messages[messageCreate.GuildId].ContainsKey(messageCreate.ChannelId))
                            {
                                Repository.messages[messageCreate.GuildId][messageCreate.ChannelId] =
                                    new ConcurrentDictionary<string, EventMessageCreate>();
                            }

                            Repository.messages[messageCreate.GuildId][messageCreate.ChannelId][messageCreate.Id] =
                                messageCreate;
                            Pool.UpdateMessage(messageCreate);

                            Console.WriteLine("channel_id {0} user {1} say: {2}",
                                messageCreate.ChannelId,
                                messageCreate.Author.Username,
                                messageCreate.Content
                            );
                            break;
                        }
                        case "MESSAGE_UPDATE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-update
                            // https://discordapp.com/developers/docs/resources/channel#message-object
                            var messageUpdate = JsonConvert.DeserializeObject<EventMessageCreate>(message.dAsString);
                            if (
                                !Repository.messages.ContainsKey(messageUpdate.GuildId) ||
                                !Repository.messages[messageUpdate.GuildId].ContainsKey(messageUpdate.ChannelId) ||
                                !Repository.messages[messageUpdate.GuildId][messageUpdate.ChannelId]
                                    .ContainsKey(messageUpdate.Id)
                            )
                            {
                                // Такого сообщения нет. Возможно, оно было создано раньше. Это не проблема 
                                break;
                            }

                            var existedMessage =
                                Repository.messages[messageUpdate.GuildId][messageUpdate.ChannelId][messageUpdate.Id];

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

                            existedMessage.EditedTimestamp = messageUpdate.EditedTimestamp;
                            existedMessage.MentionEveryone = messageUpdate.MentionEveryone;
                            existedMessage.Pinned = messageUpdate.Pinned;
                            Pool.UpdateMessage(existedMessage);

                            Console.WriteLine("channel_id {0} user {1} edited: {2}",
                                messageUpdate.ChannelId,
                                messageUpdate.Author.Username,
                                messageUpdate.Content
                            );
                            break;
                        }
                        case "CHANNEL_CREATE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#channel-create
                            var messageChannelCreate =
                                JsonConvert.DeserializeObject<EventGuildCreate.EventChannelCreate>(message.dAsString);
                            if (!Repository.channels.ContainsKey(messageChannelCreate.GuildId))
                            {
                                Repository.channels[messageChannelCreate.GuildId] =
                                    new ConcurrentDictionary<string, EventGuildCreate.EventGuildCreate_Channel>();
                            }

                            Repository.channels[messageChannelCreate.GuildId][messageChannelCreate.Id] =
                                messageChannelCreate;

                            break;
                        }
                        case "MESSAGE_DELETE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-delete
                            var guild_id = message.d.guild_id.ToString() as string;
                            var channel_id = message.d.channel_id.ToString() as string;
                            if (
                                Repository.messages.ContainsKey(guild_id) &&
                                Repository.messages[guild_id].ContainsKey(channel_id)
                            )
                            {
                                var messageId = message.d.id.ToString() as string;
                                Repository.messages[guild_id][channel_id].TryRemove(messageId, out _);
                                Pool.RemoveMessage(guild_id, channel_id, messageId);
                            }

                            // Такого сообщения нет. Возможно, оно было создано раньше. Это не проблема 
                            break;
                        }
                        case "MESSAGE_REACTION_ADD":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-reaction-add
                            var reaction = JsonConvert.DeserializeObject<Reaction>(message.dAsString);
                            if (
                                !Repository.messages.ContainsKey(reaction.GuildId) ||
                                !Repository.messages[reaction.GuildId].ContainsKey(reaction.ChannelId) ||
                                !Repository.messages[reaction.GuildId][reaction.ChannelId]
                                    .ContainsKey(reaction.MessageId)
                            )
                            {
                                break;
                            }

                            Repository.messages[reaction.GuildId][reaction.ChannelId][reaction.MessageId]
                                .AddReaction(reaction);
                            Pool.UpdateMessage(
                                Repository.messages[reaction.GuildId][reaction.ChannelId][reaction.MessageId]);

                            break;
                        }
                        case "MESSAGE_REACTION_REMOVE":
                        {
                            // https://discordapp.com/developers/docs/topics/gateway#message-reaction-remove
                            var reaction = JsonConvert.DeserializeObject<Reaction>(message.dAsString);
                            if (
                                !Repository.messages.ContainsKey(reaction.GuildId) ||
                                !Repository.messages[reaction.GuildId].ContainsKey(reaction.ChannelId) ||
                                !Repository.messages[reaction.GuildId][reaction.ChannelId]
                                    .ContainsKey(reaction.MessageId)
                            )
                            {
                                break;
                            }

                            Repository.messages[reaction.GuildId][reaction.ChannelId][reaction.MessageId]
                                .RemoveReaction(reaction);
                            Pool.UpdateMessage(
                                Repository.messages[reaction.GuildId][reaction.ChannelId][reaction.MessageId]);

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
                            if (!Repository.messages.ContainsKey(banAdd.GuildId))
                            {
                                break;
                            }

                            var userId = banAdd.User.Id;

                            foreach (var (channelId, channelsMessages) in Repository.messages[banAdd.GuildId])
                            {
                                var forDeletions = new List<string>();
                                foreach (var (messageId, messageItem) in channelsMessages)
                                {
                                    if (messageItem.Author.Id == userId)
                                    {
                                        forDeletions.Add(messageId);
                                    }
                                }

                                foreach (var messageId in forDeletions)
                                {
                                    channelsMessages.TryRemove(messageId, out _);
                                    Pool.RemoveMessage(banAdd.GuildId, channelId, messageId);
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