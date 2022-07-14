using System;
using System.Threading;
using CommandLine;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NKDiscordChatWidget.Services.General;
using NKDiscordChatWidget.WidgetServer;

namespace NKDiscordChatWidget
{
    internal static class Program
    {
        private static CancellationTokenSource globalCancellationToken;

        static void Main(string[] args)
        {
            globalCancellationToken = new CancellationTokenSource();

            try
            {
                Parser.Default.ParseArguments<ProgramOptions>(args)
                    .WithParsed(RunOptionsAndReturnExitCode);
            }
            catch (Exception)
            {
                globalCancellationToken.Cancel();
                throw;
            }

            BuildWebHost(args).Run();
        }

        public static void RunOptionsAndReturnExitCode(object rawOptions)
        {
            Startup.ProgramOptions = (ProgramOptions)rawOptions;
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost
                .CreateDefaultBuilder(args)
                .UseKestrel()
                .UseStartup<WidgetServer.Startup>()
                .UseUrls(string.Format("http://localhost:{0}", Startup.ProgramOptions.HttpPort))
                .ConfigureLogging(logging =>
                {
                    // https://docs.microsoft.com/ru-ru/aspnet/core/fundamentals/logging/?view=aspnetcore-2.2
                    logging.ClearProviders();
                })
                .Build();
    }
}