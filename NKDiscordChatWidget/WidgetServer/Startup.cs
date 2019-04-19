using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

                if (httpContext.Request.Query.TryGetValue("option_emoji_relative", out s))
                {
                    chatOption.emoji_relative = int.Parse(s.ToString());
                }

                if (httpContext.Request.Query.TryGetValue("option_emoji_stranger", out s))
                {
                    chatOption.emoji_stranger = int.Parse(s.ToString());
                }
            }

            httpContext.Response.ContentType = "application/javascript; charset=utf-8";
            var outerMessages = new List<AnswerMessage>();
            var existedMessageIDs = new HashSet<string>();
            var answer = new Dictionary<string, object>()
            {
                ["messages"] = outerMessages,
                ["existedID"] = existedMessageIDs,
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
                existedMessageIDs.Add(messages[i].id);
            }

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
                    "</div><div style='clear: both;'></div><hr>",
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
            var guildID = message.guild_id;
            var guild = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID];
            var thisGuildEmojis = new HashSet<string>(guild.emojis.Select(emoji => emoji.id).ToList());

            // Основной текст
            string directContentHTML = NKDiscordChatWidget.General.MessageMark.RenderAsHTML(
                message.content, chatOption, guildID);
            bool containOnlyUnicodeAndSpace;
            {
                var rEmojiWithinText = new Regex(@"<\:(.+?)\:([0-9]+)>", RegexOptions.Compiled);
                var longs = Utf8ToUnicode.ToUnicode(rEmojiWithinText.Replace(message.content, ""));
                containOnlyUnicodeAndSpace = Utf8ToUnicode.ContainOnlyUnicodeAndSpace(longs);
            }
            string html = string.Format("<div class='content-direct {1}'>{0}</div>",
                directContentHTML, containOnlyUnicodeAndSpace ? "only-emoji" : "");

            // attachments
            if (message.attachments.Any())
            {
                string attachmentHTML = "";

                foreach (var attachment in message.attachments)
                {
                    attachmentHTML += string.Format(
                        "<div class='attachment'><img src='{0}' data-width='{1}' data-height='{2}'></div>",
                        HttpUtility.HtmlEncode(attachment.proxy_url),
                        attachment.width,
                        attachment.height
                    );
                }

                html += string.Format("<div class='attachment-block'>{0}</div>", attachmentHTML);
            }

            // @todo preview
            // Реакции
            if (
                message.reactions.Any() &&
                ((chatOption.message_relative_reaction != 2) || (chatOption.message_stranger_reaction != 2))
            )
            {
                // Реакции
                var reactionHTMLs = new List<string>();
                foreach (var reaction in message.reactions)
                {
                    bool isRelative = ((reaction.emoji.id == null) || thisGuildEmojis.Contains(reaction.emoji.id));
                    int emojiShow = isRelative
                        ? chatOption.message_relative_reaction
                        : chatOption.message_stranger_reaction;
                    if (emojiShow == 2)
                    {
                        continue;
                    }

                    if (reaction.emoji.id != null)
                    {
                        reactionHTMLs.Add(string.Format(
                            "<div class='emoji {2}'><img src='{0}' alt=':{1}:'><span class='count'>{3}</span></div>",
                            HttpUtility.HtmlEncode(reaction.emoji.URL),
                            HttpUtility.HtmlEncode(reaction.emoji.name),
                            (emojiShow == 1) ? "blur" : "",
                            reaction.count
                        ));
                    }
                    else
                    {
                        reactionHTMLs.Add(string.Format(
                            "<div class='emoji {1}'>{0}<span class='count'>{2}</span></div>",
                            HttpUtility.HtmlEncode(reaction.emoji.name),
                            (emojiShow == 1) ? "blur" : "",
                            reaction.count
                        ));
                    }
                }

                var s = reactionHTMLs.Aggregate("", (current, s1) => current + s1);
                html += string.Format("<div class='content-reaction'>{0}</div>", s);
            }

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
    }
}