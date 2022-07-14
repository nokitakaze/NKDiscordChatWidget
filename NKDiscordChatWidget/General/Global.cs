using System;

namespace NKDiscordChatWidget.General
{
    [Obsolete]
    public static class Global
    {
        public static readonly long TimeStart;

        static Global()
        {
            TimeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}