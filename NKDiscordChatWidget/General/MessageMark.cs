using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using NKDiscordChatWidget.DiscordBot.Classes;

namespace NKDiscordChatWidget.General
{
    public static class MessageMark
    {
        public static string RenderAsHTML(
            string text,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            string guildID
        )
        {
            // ReSharper disable once UseNullPropagation
            if (text == null)
            {
                return null;
            }

            return text
                .Split("\n")
                .Aggregate("", (current, line) =>
                    current + string.Format("<div>{0}</div>",
                        RenderLineAsHTML(line.TrimEnd('\r'), chatOption, mentions, guildID)));
        }

        private static readonly Regex rLink = new Regex(
            @"(^|\W)([a-z]+)://([a-z0-9.-]+)([a-z0-9/%().+&=_-]+)?(\?[a-z0-9/%().+&=_-]*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex rWithoutMark = new Regex(
            @"`(.+?)`",
            RegexOptions.Compiled
        );

        private static readonly Regex rSpoilerMark = new Regex(
            @"\\|\\|(.+?)\\|\\|",
            RegexOptions.Compiled
        );

        private static readonly Regex rEmojiWithinText = new Regex(
            @"<(a)?\:(.+?)\:([0-9]+)>",
            RegexOptions.Compiled
        );

        private static readonly Regex rMentionNick = new Regex(
            @"<@!?([0-9]+)>",
            RegexOptions.Compiled
        );

        private static readonly Regex rMentionRole = new Regex(
            @"<@&([0-9]+)>",
            RegexOptions.Compiled
        );

        private static readonly Regex rBold = new Regex(
            @"\*\*(.+?)\*\*",
            RegexOptions.Compiled
        );

        private static readonly Regex rEm = new Regex(
            @"\*\*(.+?)\*\*",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Обработка разметки текста ВСЕГО сообщения
        /// </summary>
        /// <param name="text"></param>
        /// <param name="chatOption"></param>
        /// <param name="mentions"></param>
        /// <param name="guildID"></param>
        /// <returns></returns>
        private static string RenderLineAsHTML(
            string text,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            string guildID
        )
        {
            // var waitDictionary = new Dictionary<string, string>();
            // @todo Цитаты (в них тоже должна быть полная маркировка)

            // Спойлеры
            /*
            text = rSpoilerMark.Replace(text, m1 =>
            {
                var wait = GetWaitString();
                waitDictionary[wait] = string.Format(
                    "<span class='spoiler {1}'><span class='content'>{0}</span></span>",
                    HttpUtility.HtmlEncode(m1.Groups[1].Value), // @todo тут должна быть полная маркировка
                    (chatOption.text_spoiler == 1) ? "spoiler-show" : ""
                );

                return wait;
            });
            */

            return RenderLineAsHTMLInnerBlock(text, chatOption, mentions, guildID);
        }

        /// <summary>
        /// Обработка разметки сообщения внутри логического блока сообщения (root-сообщение, цитата, спойлер)
        /// </summary>
        /// <param name="text"></param>
        /// <param name="chatOption"></param>
        /// <param name="mentions"></param>
        /// <param name="guildID"></param>
        /// <returns></returns>
        private static string RenderLineAsHTMLInnerBlock(
            string text,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            string guildID
        )
        {
            var waitDictionary = new Dictionary<string, string>();

            // Format (no mark)
            text = rWithoutMark.Replace(text, m1 =>
            {
                // @todo Тут есть ошибка, из-за которой код частично интерпретируется как код (или нет?)
                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<span class='without-mark'>{0}</span>",
                    HttpUtility.HtmlEncode(m1.Groups[1].Value)
                );

                return wait;
            });

            // Ссылка
            text = rLink.Replace(text, m1 =>
            {
                var wait = GetWaitString();
                var url = string.Format("{0}://{1}{2}{3}",
                    m1.Groups[2].Value,
                    m1.Groups[3].Value,
                    m1.Groups[4].Value,
                    m1.Groups[5].Value
                );
                waitDictionary[wait] = string.Format("<a href='{0}' target='_blank'>{0}</a>",
                    HttpUtility.HtmlEncode(url)
                );

                return m1.Groups[1].Value + wait;
            });

            // Emoji (Unicode)
            if (UnicodeEmojiEngine.emojiList[chatOption.unicode_emoji_displaying].Any())
            {
                var activeEmoji = new List<long>();

                var longs = Utf8ToUnicode.ToUnicodeCode(text);
                var textAfter = "";
                var containEmoji = false;
                foreach (var code in longs)
                {
                    if (!UnicodeEmojiEngine.IsInIntervalEmoji(code, chatOption.unicode_emoji_displaying))
                    {
                        // Этот символ НЕ является unicode emoji
                        if (activeEmoji.Any())
                        {
                            // У нас не пустой буффер emoji символов, надо их записать в строку
                            var localEmojiList = UnicodeEmojiEngine.RenderEmojiAsStringList(
                                chatOption.unicode_emoji_displaying, activeEmoji);
                            textAfter += RenderEmojiStringListAsHtml(
                                localEmojiList,
                                chatOption.unicode_emoji_displaying,
                                waitDictionary,
                                chatOption.emoji_relative
                            );
                            activeEmoji = new List<long>();
                        }

                        textAfter += Utf8ToUnicode.UnicodeCodeToString(code);

                        continue;
                    }

                    // Этот символ ЯВЛЯЕТСЯ unicode emoji
                    containEmoji = true;
                    activeEmoji.Add(code);
                } // foreach

                if (activeEmoji.Any())
                {
                    // У нас не пустой буффер emoji символов, надо их записать в строку
                    var localEmojiList = UnicodeEmojiEngine.RenderEmojiAsStringList(
                        chatOption.unicode_emoji_displaying, activeEmoji);
                    textAfter += RenderEmojiStringListAsHtml(
                        localEmojiList,
                        chatOption.unicode_emoji_displaying,
                        waitDictionary,
                        chatOption.emoji_relative
                    );
                }

                if (containEmoji)
                {
                    text = textAfter;
                }
            }

            // Emoji (картинки)
            var thisGuildEmojis = new HashSet<string>();
            var guild = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID];
            foreach (var emoji in guild.emojis)
            {
                thisGuildEmojis.Add(emoji.id);
            }

            // Эмодзи внутри текста
            text = rEmojiWithinText.Replace(text, m1 =>
            {
                string emojiID = m1.Groups[3].Value;
                bool isRelative = thisGuildEmojis.Contains(emojiID);
                int emojiShow = isRelative ? chatOption.emoji_relative : chatOption.emoji_stranger;
                if (emojiShow == 2)
                {
                    return " ";
                }

                var wait = GetWaitString();
                var url = string.Format("https://cdn.discordapp.com/emojis/{0}.{1}",
                    emojiID,
                    (m1.Groups[1].Value == "a") ? "gif" : "png"
                );

                waitDictionary[wait] = string.Format("<span class='emoji {2}'><img src='{0}' alt=':{1}:'></span>",
                    HttpUtility.HtmlEncode(url),
                    HttpUtility.HtmlEncode(m1.Groups[2].Value),
                    (emojiShow == 1) ? "blur" : ""
                );

                return wait;
            });

            // Упоминание человека
            text = rMentionNick.Replace(text, m1 =>
            {
                string mentionID = m1.Groups[1].Value;
                if (mentions == null)
                {
                    // Приколы дискорда
                    return string.Format("<Пользователь Unknown #{0}>", mentionID);
                }

                EventMessageCreate.EventMessageCreate_Mention mention =
                    mentions.FirstOrDefault(eMention => eMention.id == mentionID);

                if (mention == null)
                {
                    return string.Format("<Пользователь Unknown #{0}>", mentionID);
                }

                // Выбираем наиболее приоритетную роль
                var nickColor = "inherit";
                if ((guild.roles != null) && (mention.member?.roles != null) &&
                    (chatOption.message_mentions_style == 1))
                {
                    var mention_roles_local =
                        guild.roles.Where(t => mention.member.roles.Contains(t.id)).ToList();
                    mention_roles_local.Sort((a, b) => b.position.CompareTo(a.position));
                    var role = mention_roles_local.Any() ? mention_roles_local.First() : null;
                    if (role != null)
                    {
                        nickColor = role.color.ToString("X");
                        nickColor = "#" + nickColor.PadLeft(6, '0');
                    }
                }
                //

                var wait = GetWaitString();

                waitDictionary[wait] = string.Format("<span class='user mention' {1}>@{0}</span>",
                    HttpUtility.HtmlEncode(mention.member?.nick ?? mention.username),
                    (chatOption.message_mentions_style == 1) ? string.Format(" style='color: {0};'", nickColor) : ""
                );

                return wait;
            });

            // Упоминание роли
            text = rMentionRole.Replace(text, m1 =>
            {
                string roleID = m1.Groups[1].Value;

                //
                Role role = guild.roles.FirstOrDefault(t => t.id == roleID);
                if (role == null)
                {
                    return string.Format("<Role Unknown #{0}>", roleID);
                }

                string nickColor = role.color.ToString("X");
                nickColor = "#" + nickColor.PadLeft(6, '0');
                var wait = GetWaitString();

                waitDictionary[wait] = string.Format("<span class='role mention' style='color: {1};'>@{0}</span>",
                    HttpUtility.HtmlEncode(role.name),
                    nickColor
                );

                return wait;
            });

            // mark
            // Strong
            string html = rBold.Replace(text,
                m1 => string.Format("<strong>{0}</strong>", m1.Groups[1].Value));

            // Em
            html = rEm.Replace(html,
                m1 => string.Format("<em>{0}</em>", m1.Groups[1].Value));

            // Меняем все wait'ы внутри текста на значения из словаря
            foreach (var (wait, replace) in waitDictionary)
            {
                html = html.Replace(wait, replace);
            }

            return html;
        }

        public static string RenderEmojiStringListAsHtml(
            IEnumerable<UnicodeEmojiEngine.EmojiRenderResult> codes,
            EmojiPackType pack,
            Dictionary<string, string> waitDictionary,
            int emojiShow
        )
        {
            var text = "";
            var emojiSubFolderName = UnicodeEmojiEngine.GetImageSubFolder(pack);
            var emojiExtension = UnicodeEmojiEngine.GetImageExtension(pack);

            foreach (var item in codes)
            {
                if (item.isSuccess)
                {
                    var wait = GetWaitString();
                    var url = string.Format("/images/emoji/{0}/{1}.{2}",
                        emojiSubFolderName,
                        item.emojiCode,
                        emojiExtension
                    );

                    waitDictionary[wait] = string.Format(
                        "<span class='emoji unicode-emoji {0}'><img src='{1}' alt=':{2}:'></span>",
                        (emojiShow == 1) ? "blur" : "",
                        HttpUtility.HtmlEncode(url),
                        HttpUtility.HtmlEncode(item.emojiCode)
                    );

                    text += wait;
                }
                else
                {
                    text += item.rawText;
                }
            }

            return text;
        }

        #region UnionRandom

        private static readonly Random _stdRandom = new Random();
        private static readonly object _stdRandomLock = new object();

        public static string GetWaitString()
        {
            lock (_stdRandomLock)
            {
                // @todo добавить больше данных в код
                return string.Format("{1}wait:{0:F5}{2}", _stdRandom.NextDouble(), '{', '}');
            }
        }

        #endregion
    }
}