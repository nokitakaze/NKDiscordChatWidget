using System;
using System.Threading;

namespace NKDiscordChatWidget.General
{
    [Obsolete]
    public static class Global
    {
        public static CancellationToken globalCancellationToken;
        public static ProgramOptions ProgramOptions;
        public static readonly long TimeStart;

        static Global()
        {
            TimeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}