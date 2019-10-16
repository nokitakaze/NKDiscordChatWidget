using NKDiscordChatWidget.General;

namespace NKDiscordChatWidget
{
    public class ChatDrawOption
    {
        public bool merge_same_user_messages; // @todo Временно игнорируется
        public int attachments;
        public int link_preview;
        public int message_relative_reaction;
        public int message_stranger_reaction;
        public int emoji_stranger;
        public int emoji_relative;
        public int text_spoiler;
        public int message_mentions_style;

        /// <summary>
        /// Какой пак эмодзи отображать
        /// </summary>
        public EmojiPackType unicode_emoji_displaying = EmojiPackType.Twemoji;
    }
}