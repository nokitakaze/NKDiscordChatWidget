using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
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