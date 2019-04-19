using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

            app.Run(Request);
        }

        public static async Task Request(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            var path = httpContext.Request.Path;
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (path == "/")
            {
                // Главная
                var html = File.ReadAllText(Options.WWWRoot + "/index.html");
                string guildsHTML = "";
                foreach (var (guildID, channels) in NKDiscordChatWidget.DiscordBot.Bot.channels)
                {
                    string guildHTML = "";
                    var channelsByGroup = new Dictionary<string, List<EventGuildCreate.EventGuildCreate_Channel>>();
                    foreach (var channel in channels.Values)
                    {
                        if (channel.type == 0)
                        {
                            if (!channelsByGroup.ContainsKey(channel.parent_id))
                            {
                                channelsByGroup[channel.parent_id] =
                                    new List<EventGuildCreate.EventGuildCreate_Channel>();
                            }

                            channelsByGroup[channel.parent_id].Add(channel);
                        }
                    }

                    foreach (var (parentChannelId, localChannels) in channelsByGroup)
                    {
                        guildHTML += string.Format("<li class='item'>{0}</li>",
                            HttpUtility.HtmlEncode(channels[parentChannelId].name)
                        );

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
                var html = File.ReadAllText(Options.WWWRoot + "/chat.html");
                await httpContext.Response.WriteAsync(html);
                return;
            }

            if (path == "/chat_ajax.cgi")
            {
                await GetMessages(httpContext);
                return;
            }

            httpContext.Response.StatusCode = 404;
            await httpContext.Response.WriteAsync("Not found");
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
            }

            httpContext.Response.ContentType = "application/javascript; charset=utf-8";
            var outerMessages = new List<AnswerMessage>();
            var answer = new Dictionary<string, object>()
            {
                ["messages"] = outerMessages,
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
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                string htmlContent = string.Format("<div class='content-message' data-id='{1}'>{0}</div>",
                    DrawMessageContent(message, chatOption), message.id);
                var timeUpdate = (message.edited_timestampAsDT != DateTime.MinValue)
                    ? message.edited_timestampAsDT
                    : message.timestampAsDT;
                if (chatOption.merge_same_user_messages)
                {
                    for (var j = i + 1; j < messages.Count; j++)
                    {
                        if (messages[j].author.id == messages[i].author.id)
                        {
                            var localTimeUpdate = (messages[j].edited_timestampAsDT != DateTime.MinValue)
                                ? messages[j].edited_timestampAsDT
                                : messages[j].timestampAsDT;
                            if (localTimeUpdate > timeUpdate)
                            {
                                timeUpdate = localTimeUpdate;
                            }

                            htmlContent += string.Format("<div class='content-message' data-id='{1}'>{0}</div>",
                                DrawMessageContent(messages[j], chatOption), messages[j].id);
                            i = j;
                        }
                    }
                }

                string nickColor = "inherit";
                {
                    var roleID = message.member.roles.First();
                    EventGuildCreate.EventGuildCreate_Role role = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID]
                        .roles.FirstOrDefault(t => t.id == roleID);
                    if (role != null)
                    {
                        nickColor = role.color.ToString("X");
                        nickColor = "#" + nickColor.PadLeft(6, '0');
                    }
                }

                string sha1hash;
                using (var hashA = SHA1.Create())
                {
                    byte[] data = hashA.ComputeHash(Encoding.UTF8.GetBytes(htmlContent));
                    sha1hash = data.Aggregate("", (current, c) => current + c.ToString("x2"));
                }

                var html = string.Format(
                    "<div class='user'><img src='{0}' alt='{1}'></div>" +
                    "<div class='content'><span class='content-user' style='color: {4};'>{1}</span><span class='content-time'>{3:hh:mm:ss dd.MM.yyyy}</span>" +
                    "{2}" +
                    "</div><hr>",
                    HttpUtility.HtmlEncode(message.author.avatarURL),
                    HttpUtility.HtmlEncode(message.author.username),
                    htmlContent,
                    message.timestampAsDT,
                    nickColor
                );

                outerMessages.Add(new AnswerMessage()
                {
                    id = message.id,
                    time = ((DateTimeOffset) message.timestampAsDT).ToUnixTimeMilliseconds() * 0.001d,
                    time_update = ((DateTimeOffset) timeUpdate).ToUnixTimeMilliseconds() * 0.001d,
                    html = html,
                    hash = sha1hash,
                });
            }

            await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(answer));
        }

        private static string DrawMessageContent(EventMessageCreate message, ChatDrawOption chatOption)
        {
            // Основной текст
            string html = NKDiscordChatWidget.General.MessageMark.RenderAsHTML(message.content);

            // @todo attachments
            // @todo preview
            // @todo реакции

            return html;
        }

        protected class AnswerMessage
        {
            public string id;
            public double time;
            public double time_update;
            public string html;
            public string hash;
        }

        protected class ChatDrawOption
        {
            public bool merge_same_user_messages;
            public int attachments;
            public int link_preview;
            public int message_relative_reaction;
            public int message_stranger_reaction;
        }
    }
}