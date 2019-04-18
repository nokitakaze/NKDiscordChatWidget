using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                throw new Exception("GoogleAuthToken is empty");
            }
        }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
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
                    var dics = new Dictionary<string, List<EventGuildCreate.EventGuildCreate_Channel>>();
                    foreach (var channel in channels.Values)
                    {
                        if (channel.type == 0)
                        {
                            if (!dics.ContainsKey(channel.parent_id))
                            {
                                dics[channel.parent_id] = new List<EventGuildCreate.EventGuildCreate_Channel>();
                            }

                            dics[channel.parent_id].Add(channel);
                        }
                    }

                    foreach (var (parentChannelId, localChannels) in dics)
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
                                "<li class='item-sub'><a href='/chat.cgi?guild={1}&channel={2}' target='_blank'>{0}</a></li>",
                                HttpUtility.HtmlEncode(realChannel.name), guildID, realChannel.id));
                    }

                    guildHTML = string.Format(
                        "<div class='block-guild'><h2>{1} <img src='{2}'></h2><ul>{0}</ul></div>",
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

            /*
            if (path == "/current.cgi")
            {
                // Текущее значение
                GetCurrentCgi(httpContext);
                return;
            }
            */

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
            foreach (var message in messages)
            {
                var htmlContent = HttpUtility.HtmlEncode(message.content);
                string nickColor = "inherit";
                {
                    var roleID = message.member.roles.First();
                    EventGuildCreate.EventGuildCreate_Role role = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].roles
                        .FirstOrDefault(t => t.id == roleID);
                    if (role != null)
                    {
                        nickColor = role.color.ToString("X");
                        nickColor = "#" + nickColor.PadLeft(6, '0');
                    }
                }

                var html = string.Format(
                    "<div class='user'><img src='{0}' alt='{1}'></div>" +
                    "<div class='content'><span class='content-user' style='color: {4};'>{1}</span><span class='content-time'>{3:hh:mm:ss dd.MM.yyyy}</span>" +
                    "<div class='content-message'>{2}</div>" +
                    "</div><hr>",
                    HttpUtility.HtmlEncode(message.author.avatarURL),
                    HttpUtility.HtmlEncode(message.author.username),
                    htmlContent,
                    message.timestampAsDT,
                    nickColor
                );

                var timeUpdate = (message.edited_timestampAsDT != DateTime.MinValue)
                    ? message.edited_timestampAsDT
                    : message.timestampAsDT;

                outerMessages.Add(new AnswerMessage()
                {
                    id = message.id,
                    time = ((DateTimeOffset) message.timestampAsDT).ToUnixTimeMilliseconds() * 0.001d,
                    time_update = ((DateTimeOffset) timeUpdate).ToUnixTimeMilliseconds() * 0.001d,
                    html = html,
                });
            }

            await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(answer));
        }

        protected class AnswerMessage
        {
            public string id;
            public double time;
            public double time_update;
            public string html;
        }
    }
}