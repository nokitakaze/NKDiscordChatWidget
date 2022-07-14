using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NKDiscordChatWidget.General;

namespace NKDiscordChatWidget.BackgroundService
{
    /// <summary>
    /// Чистка сообщений в каналах для освобождения памяти и ускорения работы
    /// </summary>
    // todo Перенести в BackgroundService
    public class ClearChatTimer : IHostedService
    {
        /// <summary>
        /// Время (в секундах) между очисткой сообщений в каналах
        /// </summary>
        public const long DelayBetweenClearing = 300;

        /// <summary>
        /// Максимальное количество сохранённых сообщений на один канал
        /// </summary>
        public const int MaximumMessagesPerChannelCount = 40;

        private readonly ProgramOptions ProgramOptions;

        public ClearChatTimer(
            ProgramOptions programOptions
        )
        {
            ProgramOptions = programOptions;
        }

        #region IHostedService

        private readonly CancellationTokenSource CancellationSource = new CancellationTokenSource();
        private CancellationToken CancellationToken => CancellationSource.Token;
        private Task MainTask;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        public Task StartAsync(CancellationToken cancellationToken)
        {
            MainTask = Task.Run(StartTask);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            CancellationSource.Cancel();
            await MainTask;
        }

        #endregion

        public void StartTask()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                // Ожидаем следующего раза
                var nextTime = DateTime.Now.ToUniversalTime().Add(TimeSpan.FromSeconds(DelayBetweenClearing));
                while ((DateTime.Now.ToUniversalTime() < nextTime) &&
                       !CancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                SingleIteration();
            }
        }

        private void SingleIteration()
        {
            // Перебор всех гильдий (серверов) и каналов внутри них
            foreach (var (guildID, messagesInGuild) in NKDiscordChatWidget.BackgroundService.Bot.messages)
            {
                foreach (var (channelID, messagesInChannel) in messagesInGuild)
                {
                    if (messagesInChannel.Count <= MaximumMessagesPerChannelCount)
                    {
                        continue;
                    }

                    // Очищаем сообщения
                    var messages = messagesInChannel.Values.ToList();
                    messages.Sort((a, b) => a.timestampAsDT.CompareTo(b.timestampAsDT));
                    for (int i = 0; i < messages.Count - MaximumMessagesPerChannelCount; i++)
                    {
                        messagesInChannel.Remove(messages[i].id, out _);
                    }

                    Console.WriteLine("{0}\tServer {1} channel {2}. {3} messages deleted",
                        DateTime.Now.ToUniversalTime(),
                        guildID,
                        channelID,
                        messages.Count - MaximumMessagesPerChannelCount + 1
                    );
                }
            }
        }
    }
}