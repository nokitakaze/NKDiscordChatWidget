using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using NKDiscordChatWidget.DiscordBot.Classes;
using NKDiscordChatWidget.General;

namespace NKDiscordChatWidget.WidgetServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            if (string.IsNullOrEmpty(Global.options.DiscordBotToken))
            {
                throw new Exception("DiscordBotToken is empty");
            }
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            // Можно решить это через nginx
            Console.WriteLine("WWWRoot: {0}", Options.WWWRoot);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Options.WWWRoot, "images")),
                RequestPath = "/images"
            });
            app.UseDeveloperExceptionPage();
            UnicodeEmojiEngine.LoadAllEmojiPacks(Options.WWWRoot);

            app.UseSignalR(routes =>
            {
                routes.MapHub<NKDiscordChatWidget.WidgetServer.WebsocketClientSide>("/websocketChat");

                // Берём контекст всех подключенных по WebSocket клиентов к end-point'у /websocketChat
                WebsocketClientSide.hubContext = app.ApplicationServices.GetService<IHubContext<WebsocketClientSide>>();
            });
            app.Run(Request);
        }

        public static async Task Request(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            var path = httpContext.Request.Path;
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (path == "/")
            {
                await MainPage(httpContext);
                return;
            }

            if (path == "/favicon.ico")
            {
                // Favicon
                httpContext.Response.StatusCode = 404;
                return;
            }

            if (path == "/chat.cgi")
            {
                await ChatHTML(httpContext);
                return;
            }

            if (path == "/chat_ajax.cgi")
            {
                // Этот код устарел и остаётся здесь только временно
                // @todo удалить весь этот код

                await GetMessages(httpContext);
                return;
            }

            httpContext.Response.StatusCode = 404;
            await httpContext.Response.WriteAsync("Not found");
        }

        private static async Task MainPage(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            // Главная
            var html = File.ReadAllText(Options.WWWRoot + "/index.html");
            html = replaceLinksInHTML(html);
            string guildsHTML = "";
            foreach (var (guildID, channels) in NKDiscordChatWidget.DiscordBot.Bot.channels)
            {
                string guildHTML = "";
                var channelsByGroup = new Dictionary<string, List<EventGuildCreate.EventGuildCreate_Channel>>();
                // Каналы без групп отображаются выше всех
                var groupPositions = new Dictionary<string, int> {[""] = -2};
                foreach (var channel in channels.Values)
                {
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (channel.type)
                    {
                        case 0:
                        {
                            string parentId = channel.parent_id ?? "";
                            if (!channelsByGroup.ContainsKey(parentId))
                            {
                                channelsByGroup[parentId] =
                                    new List<EventGuildCreate.EventGuildCreate_Channel>();
                            }

                            channelsByGroup[parentId].Add(channel);
                            break;
                        }

                        case 4:
                            groupPositions[channel.id] = channel.position ?? -1;
                            break;
                    }
                }

                var channelIDs = channelsByGroup.Keys.ToList();
                channelIDs.Sort((a, b) => groupPositions[a].CompareTo(groupPositions[b]));

                foreach (var parentChannelId in channelIDs)
                {
                    var localChannels = channelsByGroup[parentChannelId];
                    if (parentChannelId != "")
                    {
                        guildHTML += string.Format("<li class='item'>{0}</li>",
                            HttpUtility.HtmlEncode(channels[parentChannelId].name)
                        );
                    }
                    else
                    {
                        guildHTML += "<li class='item'>-----------------</li>";
                    }

                    localChannels.Sort((a, b) =>
                    {
                        if (a.position == null)
                        {
                            return -1;
                        }

                        // ReSharper disable once ConvertIfStatementToReturnStatement
                        if (b.position == null)
                        {
                            return 1;
                        }

                        return a.position.Value.CompareTo(b.position.Value);
                    });

                    guildHTML = localChannels.Aggregate(guildHTML, (current, realChannel) =>
                        current + string.Format(
                            "<li class='item-sub'><a href='/chat.cgi?guild={1}&channel={2}' " +
                            "data-guild-id='{1}' data-channel-id='{2}' target='_blank'>{0}</a></li>",
                            HttpUtility.HtmlEncode(realChannel.name), guildID, realChannel.id));
                }

                guildHTML = string.Format(
                    "<div class='block-guild'><h2><img src='{2}'> {1}</h2><ul>{0}</ul></div>",
                    guildHTML,
                    HttpUtility.HtmlEncode(NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].name),
                    HttpUtility.HtmlEncode(NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].GetIconURL)
                );
                guildsHTML += guildHTML;
            }

            html = html.Replace("{#wait:guilds#}", guildsHTML);

            httpContext.Response.ContentType = "text/html; charset=utf-8";

            await httpContext.Response.WriteAsync(html);
        }

        private static async Task GetMessages(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            string guildID, channelID;
            {
                if (!httpContext.Request.Query.TryGetValue("guild", out var a1))
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("Bad request");
                    return;
                }

                guildID = a1.ToString();

                if (!httpContext.Request.Query.TryGetValue("channel", out var a2))
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("Bad request");
                    return;
                }

                channelID = a2.ToString();
            }
            var chatOption = new ChatDrawOption();
            {
                if (httpContext.Request.Query.TryGetValue("option_merge_same_user_messages", out var s))
                {
                    chatOption.merge_same_user_messages = (int.Parse(s.ToString()) == 1);
                }

                if (httpContext.Request.Query.TryGetValue("option_attachments", out s))
                {
                    chatOption.attachments = int.Parse(s.ToString());
                }

                if (httpContext.Request.Query.TryGetValue("option_link_preview", out s))
                {
                    chatOption.link_preview = int.Parse(s.ToString());
                }

                if (httpContext.Request.Query.TryGetValue("option_message_relative_reaction", out s))
                {
                    chatOption.message_relative_reaction = int.Parse(s.ToString());
                }

                if (httpContext.Request.Query.TryGetValue("option_message_stranger_reaction", out s))
                {
                    chatOption.message_stranger_reaction = int.Parse(s.ToString());
                }

                if (httpContext.Request.Query.TryGetValue("option_emoji_relative", out s))
                {
                    chatOption.emoji_relative = int.Parse(s.ToString());
                }

                if (httpContext.Request.Query.TryGetValue("option_emoji_stranger", out s))
                {
                    chatOption.emoji_stranger = int.Parse(s.ToString());
                }

                if (httpContext.Request.Query.TryGetValue("option_unicode_emoji_displaying", out s))
                {
                    chatOption.unicode_emoji_displaying = (EmojiPackType) int.Parse(s.ToString());
                }
            }

            httpContext.Response.ContentType = "application/javascript; charset=utf-8";
            var answer = new AnswerFull
            {
                channel_title = string.Format("Discord Widget Chat: {0}-{1} [nkt]", guildID, channelID),
            };
            if (!NKDiscordChatWidget.DiscordBot.Bot.messages.ContainsKey(guildID))
            {
                await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(answer));
                return;
            }

            if (!NKDiscordChatWidget.DiscordBot.Bot.messages[guildID].ContainsKey(channelID))
            {
                await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(answer));
                return;
            }

            var messages = NKDiscordChatWidget.DiscordBot.Bot.messages[guildID][channelID].Values.ToList();
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
                var localAnswerMessage = MessageArtist.DrawMessage(message, chatOption);
                answer.messages.Add(localAnswerMessage);
            }

            answer.time_answer = ((DateTimeOffset) DateTime.Now).ToUnixTimeMilliseconds() * 0.001d;
            await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(answer));
        }

        private static readonly Regex rHTMLIncludeLink =
            new Regex("{#include_link:(.+?)#}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static async Task ChatHTML(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            var html = File.ReadAllText(Options.WWWRoot + "/chat.html");
            html = replaceLinksInHTML(html);
            await httpContext.Response.WriteAsync(html);
        }

        private static string replaceLinksInHTML(string html)
        {
            return rHTMLIncludeLink.Replace(html, (m) =>
            {
                var filename = m.Groups[1].Value;
                // hint: тут специально не проверяется путь до файла
                var bytes = File.ReadAllBytes(Options.WWWRoot + filename);

                string sha1hash;
                using (var hashA = SHA1.Create())
                {
                    byte[] data = hashA.ComputeHash(bytes);
                    sha1hash = data.Aggregate("", (current, c) => current + c.ToString("x2"));
                }

                return string.Format("{0}?hash={1}",
                    filename,
                    sha1hash.Substring(0, 6)
                );
            });
        }
    }
}