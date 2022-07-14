using System;
using System.Threading;

namespace NKDiscordChatWidget.General
{
    public static class Global
    {
        public static CancellationToken globalCancellationToken;
        public static Options options;
        public static readonly long TimeStart;

        static Global()
        {
            TimeStart = ((DateTimeOffset)(DateTime.Now)).ToUnixTimeSeconds();
        }
    }
}