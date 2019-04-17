using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
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

            try
            {
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(RunOptionsAndReturnExitCode);
                // Task.Factory.StartNew(BackgroundUpdate.UpdateAllCounters, TaskCreationOptions.LongRunning);
                // @todo Запускаем бота
            }
            catch (Exception)
            {
                globalCancellationToken.Cancel();
                throw;
            }

            BuildWebHost(args).Run();

            // Тред отменили, закрываемся
            globalCancellationToken.Cancel();
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
                .Build();
    }
}