using System;
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

namespace NKDiscordChatWidget.DiscordBot
{
    public static class Bot
    {
        private static ClientWebSocket wsClient;
        private static ulong websocketSequenceId;
        private static string sessionID;
        private static volatile int msBetweenPing = 10000;
        private static DateTime lastIncomingMessageTime = DateTime.MinValue;
        private static DateTime lastIncomingPingTime = DateTime.MinValue;
        private static readonly List<EventGuildCreate> guilds = new List<EventGuildCreate>();

        public static async void StartTask()
        {
            var options = NKDiscordChatWidget.General.Global.options;
            string wsBaseUrl;
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
                    var rawAnswer = await response.Content.ReadAsStringAsync();
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawAnswer);
                    wsBaseUrl = dict["url"] as string;
                }
            }

            Console.WriteLine("Discord websocket base url: {0}", wsBaseUrl);
#pragma warning disable 4014
            Task.Run(async () => { await heartBeat(); });
#pragma warning restore 4014

            do
            {
                using (wsClient = new ClientWebSocket())
                {
                    var fullConnectURI = string.Format("{0}?v=6&encoding=json", wsBaseUrl);
                    await wsClient.ConnectAsync(new Uri(fullConnectURI), Global.globalCancellationToken);
                    await ProcessWebSocket();
                }
            } while (true);

            Console.WriteLine("...");
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

            Console.WriteLine("{0}\top = {1,3}\t{2}\n\t{3}",
                DateTime.Now.ToUniversalTime(),
                message.op,
                message.t,
                JsonConvert.SerializeObject(message.d)
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
                                ["token"] = Global.options.DiscordBotToken,
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
                                ["token"] = Global.options.DiscordBotToken,
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
                            sessionID = message.d.session_id;
                            JsonConvert.SerializeObject(message.d);
                            break;
                        }
                        case "GUILD_CREATE":
                        {
                            var guild = JsonConvert.DeserializeObject<EventGuildCreate>(message.dAsString);
                            guilds.Add(guild);
                            break;
                        }
                        case "MESSAGE_CREATE":
                        case "MESSAGE_UPDATE":
                        {
                            var messageCreate = JsonConvert.DeserializeObject<EventMessageCreate>(message.dAsString);

                            // https://discordapp.com/developers/docs/topics/gateway#message-create
                            Console.WriteLine("channel_id {0} user {1} say: {2}",
                                messageCreate.channel_id,
                                messageCreate.author.username,
                                messageCreate.content
                            );
                            break;
                        }
                        // @todo message delete
                    }

                    break;
            }
        }
    }
}