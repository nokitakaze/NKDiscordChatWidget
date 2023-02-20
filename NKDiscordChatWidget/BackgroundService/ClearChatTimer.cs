using System.Diagnostics.CodeAnalysis;
using NKDiscordChatWidget.Services.General;
using NKDiscordChatWidget.Services.Services;

namespace NKDiscordChatWidget.BackgroundService
{
    /// <summary>
    /// Чистка сообщений в каналах для освобождения памяти и ускорения работы
    /// </summary>
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
        private readonly DiscordRepository Repository;

        public ClearChatTimer(
            ProgramOptions programOptions,
            DiscordRepository repository
        )
        {
            ProgramOptions = programOptions;
            Repository = repository;
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

        [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        public async Task StartTask()
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                // Ожидаем следующего раза
                var nextTime = DateTime.Now.ToUniversalTime().Add(TimeSpan.FromSeconds(DelayBetweenClearing));
                while ((DateTime.Now.ToUniversalTime() < nextTime) &&
                       !CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100);
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
            foreach (var (guildID, messagesInGuild) in Repository.messages)
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
                        messagesInChannel.Remove(messages[i].Id, out _);
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