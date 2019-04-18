using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
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

                        foreach (var realChannel in localChannels.ToArray())
                        {
                            guildHTML += string.Format(
                                "<li class='item-sub'><a href='/chat.cgi?guild={1}&channel={2}' target='_blank'>{0}</a></li>",
                                HttpUtility.HtmlEncode(realChannel.name),
                                guildID,
                                realChannel.id
                            );
                        }
                    }

                    guildHTML = string.Format(
                        "<div class='block-guild'><h2>{1}</h2><ul>{0}</ul></div>",
                        guildHTML,
                        HttpUtility.HtmlEncode(NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].name)
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

        /*
        private static async void GetCurrentCgi(Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            string id;
            {
                if (!httpContext.Request.Query.TryGetValue("id", out var id1))
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("Bad request");
                    return;
                }

                id = id1.ToString();
            }

            httpContext.Response.ContentType = "application/json; charset=utf-8";
            BackgroundUpdate.AddYouTubeChannel(id);
            var dictionary = BackgroundUpdate.YouTubeValues;
            if (dictionary.ContainsKey(id))
            {
                await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(
                    new Dictionary<string, object>()
                    {
                        ["status"] = "ok",
                        ["sub_status"] = "exist",
                        ["value"] = dictionary[id],
                    }));
            }
            else
            {
                await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(
                    new Dictionary<string, object>()
                    {
                        ["status"] = "ok",
                        ["sub_status"] = "new",
                        ["value"] = null,
                    }));
            }
        }
        */
    }
}