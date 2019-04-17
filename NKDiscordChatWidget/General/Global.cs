using System.Threading;

namespace NKDiscordChatWidget.General
{
    public static class Global
    {
        public static CancellationToken globalCancellationToken;
        public static Options options;
    }
}