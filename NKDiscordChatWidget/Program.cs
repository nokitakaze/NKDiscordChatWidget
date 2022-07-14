using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NKDiscordChatWidget.BackgroundService;
using NKDiscordChatWidget.General;

namespace NKDiscordChatWidget
{
    internal static class Program
    {
        private static CancellationTokenSource globalCancellationToken;

        static void Main(string[] args)
        {
            globalCancellationToken = new CancellationTokenSource();
            Global.globalCancellationToken = globalCancellationToken.Token;

            var tasks = new List<Task>();
            try
            {
                tasks.Add(Task.Run(() => { ClearChatTimer.StartTask(); }));
                tasks.Add(Task.Run(() => { ResourceFileWatch.StartTask(); }));

                Parser.Default.ParseArguments<ProgramOptions>(args)
                    .WithParsed(RunOptionsAndReturnExitCode);
                tasks.Add(Task.Factory.StartNew(NKDiscordChatWidget.BackgroundService.Bot.StartTask,
                    TaskCreationOptions.LongRunning));
            }
            catch (Exception)
            {
                globalCancellationToken.Cancel();
                throw;
            }

            BuildWebHost(args).Run();

            // Тред отменили, закрываемся
            globalCancellationToken.Cancel();

            // Ждём всех
            Task.WaitAll(tasks.ToArray());
        }

        public static void RunOptionsAndReturnExitCode(object rawOptions)
        {
            Global.ProgramOptions = (ProgramOptions)rawOptions;
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost
                .CreateDefaultBuilder(args)
                .UseKestrel()
                .UseStartup<WidgetServer.Startup>()
                .UseUrls(string.Format("http://localhost:{0}", Global.ProgramOptions.HttpPort))
                .ConfigureLogging(logging =>
                {
                    // https://docs.microsoft.com/ru-ru/aspnet/core/fundamentals/logging/?view=aspnetcore-2.2
                    logging.ClearProviders();
                })
                .Build();
    }
}