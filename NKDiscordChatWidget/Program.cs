using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NKDiscordChatWidget.General;

namespace NKDiscordChatWidget
{
    class Program
    {
        private static CancellationTokenSource globalCancellationToken;

        static void Main(string[] args)
        {
            globalCancellationToken = new CancellationTokenSource();
            Global.globalCancellationToken = globalCancellationToken.Token;

            var tasks = new List<Task>
            {
                Task.Run(() => { NKDiscordChatWidget.General.ClearChatTimer.StartTask(); }),
            };

            try
            {
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(RunOptionsAndReturnExitCode);
                tasks.Add(Task.Factory.StartNew(DiscordBot.Bot.StartTask, TaskCreationOptions.LongRunning));
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
            Global.options = (Options) rawOptions;
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost
                .CreateDefaultBuilder(args)
                .UseKestrel()
                .UseStartup<WidgetServer.Startup>()
                .UseUrls(string.Format("http://localhost:{0}", Global.options.HttpPort))
                .ConfigureLogging(logging =>
                {
                    // https://docs.microsoft.com/ru-ru/aspnet/core/fundamentals/logging/?view=aspnetcore-2.2
                    logging.ClearProviders();
                })
                .Build();
    }
}