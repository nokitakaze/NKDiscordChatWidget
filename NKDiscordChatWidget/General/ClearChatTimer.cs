using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NKDiscordChatWidget.General
{
    /// <summary>
    /// Чистка сообщений в каналах для освобождения памяти и ускорения работы
    /// </summary>
    public static class ClearChatTimer
    {
        /// <summary>
        /// Время (в секундах) между очисткой сообщений в каналах
        /// </summary>
        public const long DelayBetweenClearing = 300;

        /// <summary>
        /// Максимальное количество сохранённых сообщений на один канал
        /// </summary>
        public const int MaximumMessagesPerChannelCount = 40;

        public static void StartTask()
        {
            while (!General.Global.globalCancellationToken.IsCancellationRequested)
            {
                // Ожидаем следующего раза
                var nextTime = DateTime.Now.ToUniversalTime().Add(TimeSpan.FromSeconds(DelayBetweenClearing));
                while ((DateTime.Now.ToUniversalTime() < nextTime) &&
                       !General.Global.globalCancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                if (General.Global.globalCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                SingleIteration();
            }
        }

        private static void SingleIteration()
        {
            // Перебор всех гильдий (серверов) и каналов внутри них
            foreach (var (guildID, messagesInGuild) in NKDiscordChatWidget.DiscordBot.Bot.messages)
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