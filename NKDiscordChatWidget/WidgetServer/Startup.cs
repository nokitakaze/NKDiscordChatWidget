using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NKDiscordChatWidget.BackgroundService;
using NKDiscordChatWidget.DiscordModel;
using NKDiscordChatWidget.Services;
using NKDiscordChatWidget.Services.General;
using NKDiscordChatWidget.Services.Services;
using NKDiscordChatWidget.Services.Util;

namespace NKDiscordChatWidget.WidgetServer
{
    public class Startup
    {
        public static ProgramOptions ProgramOptions;
        private static readonly DiscordRepository DiscordRepository = new DiscordRepository();

        public Startup(IConfiguration configuration)
        {
            if (string.IsNullOrEmpty(ProgramOptions.DiscordBotToken))
            {
                throw new Exception("DiscordBotToken is empty");
            }
        }

        [SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod")]
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
            services.AddSingleton<WebsocketClientSidePool>();
            services.AddSingleton<ProgramOptions>(_ => ProgramOptions);
            services.AddSingleton(_ => DiscordRepository);
            services.AddSingleton<MessageMarkdownParser>();
            services.AddSingleton<MessageArtist>();

            services.AddHostedService<ResourceFileWatch>();
            services.AddHostedService<ClearChatTimer>();
            services.AddHostedService<Bot>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            // Можно решить это через nginx
            Console.WriteLine("WWWRoot: {0}", ProgramOptions.WWWRoot);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(ProgramOptions.WWWRoot, "images")),
                RequestPath = "/images"
            });
            app.UseDeveloperExceptionPage();
            UnicodeEmojiEngine.LoadAllEmojiPacks(ProgramOptions.WWWRoot);

            app.UseSignalR(routes =>
            {
                routes.MapHub<WebsocketClientSide>("/websocketChat");
            });
            app.Run(Request);
        }

        #region low level HTTP responder

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

            httpContext.Response.StatusCode = 404;
            await httpContext.Response.WriteAsync("Not found");
        }

        private static async Task MainPage(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            // Главная
            var html = File.ReadAllText(ProgramOptions.WWWRoot + "/index.html");
            html = replaceLinksInHTML(html);
            string guildsHTML = "";
            foreach (var (guildID, channels) in DiscordRepository.channels)
            {
                string guildHTML = "";
                var channelsByGroup = new Dictionary<string, List<EventGuildCreate.EventGuildCreate_Channel>>();
                // Каналы без групп отображаются выше всех
                var groupPositions = new Dictionary<string, int> { [""] = -2 };
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
                    HttpUtility.HtmlEncode(DiscordRepository.guilds[guildID].name),
                    HttpUtility.HtmlEncode(DiscordRepository.guilds[guildID].GetIconURL)
                );
                guildsHTML += guildHTML;
            }

            html = html.Replace("{#wait:guilds#}", guildsHTML);

            httpContext.Response.ContentType = "text/html; charset=utf-8";

            await httpContext.Response.WriteAsync(html);
        }

        private static readonly Regex rHTMLIncludeLink =
            new Regex("{#include_link:(.+?)#}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static async Task ChatHTML(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            var html = await File.ReadAllTextAsync(ProgramOptions.WWWRoot + "/chat.html");
            html = replaceLinksInHTML(html);
            await httpContext.Response.WriteAsync(html);
        }

        private static string replaceLinksInHTML(string html)
        {
            return rHTMLIncludeLink.Replace(html, (m) =>
            {
                var filename = m.Groups[1].Value;
                // hint: тут специально не проверяется путь до файла
                var bytes = File.ReadAllBytes(ProgramOptions.WWWRoot + filename);

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

        #endregion
    }
}