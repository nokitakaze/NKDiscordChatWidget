using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using NKDiscordChatWidget.DiscordBot.Classes;

namespace NKDiscordChatWidget.General
{
    /// <summary>
    /// Парсер markdown
    /// </summary>
    public static class MessageMarkdownParser
    {
        /// <summary>
        /// Получаем сырой текст с маркдауном и превращаем его в HTML-код
        /// </summary>
        /// <param name="text">Многострочный сырой текст с маркдауном</param>
        /// <param name="chatOption"></param>
        /// <param name="mentions"></param>
        /// <param name="guildID"></param>
        /// <returns>HTML-код, который можно безопасно рендерить в чате</returns>
        public static string RenderMarkdownAsHTML(
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

            // hint: Цитаты в Дискорде ТОЛЬКО одноуровневые, поэтому парсер цитат нерекурсивный
            string result = "";
            bool isInQuote = false;
            var currentQuoteHTML = "";
            foreach (var line in text.Split("\n"))
            {
                var trimmedLine = line.TrimEnd('\r');
                if ((trimmedLine.Length >= 2) && (trimmedLine.Substring(0, 2) == "> "))
                {
                    // Это кусок цитаты
                    currentQuoteHTML += string.Format("<div class='line'>{0}</div>",
                        RenderLineAsHTML(trimmedLine.Substring(2), chatOption, mentions, guildID));
                    isInQuote = true;
                }
                else
                {
                    if (isInQuote)
                    {
                        // hint: цитата может быть пустой, но это всё равно цитата, поэтому тут не просто
                        // проверка на currentQuoteHTML != ""
                        result += string.Format(
                            "<div class='quote-block'><div class='quote-border'></div><div class='quote-content'>{0}</div></div>",
                            currentQuoteHTML
                        );
                        currentQuoteHTML = "";
                        isInQuote = false;
                    }

                    result += string.Format("<div class='line'>{0}</div>",
                        RenderLineAsHTML(trimmedLine, chatOption, mentions, guildID));
                }
            }

            if (isInQuote)
            {
                // Текст заканчивается цитатой
                result += string.Format(
                    "<div class='quote-block'><div class='quote-border'></div><div class='quote-content'>{0}</div></div>",
                    currentQuoteHTML
                );
            }

            return result;
        }

        #region Regexps

        private static readonly Regex rLink = new Regex(
            @"(^|\W)([a-z]+)://([a-z0-9.-]+)([a-z0-9/%().+&=_:-]+)?(\?[a-z0-9/%().+&=_-]*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex rWithoutMark = new Regex(
            @"`(.+?)`",
            RegexOptions.Compiled
        );

        private static readonly Regex rSpoilerMark = new Regex(
            @"\|\|(.+?)\|\|",
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
            @"\*\*(.+)\*\*",
            RegexOptions.Compiled
        );

        private static readonly Regex rEm = new Regex(
            @"\*(.+)\*",
            RegexOptions.Compiled
        );

        private static readonly Regex rDelete = new Regex(
            @"\~\~(.+?)\~\~",
            RegexOptions.Compiled
        );

        #endregion

        /// <summary>
        /// Обработка разметки текста ОДНОЙ СТРОКИ. Без использования цитат
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <param name="guildID">ID гильдии (сервера), в котором написано сообщение</param>
        /// <returns></returns>
        private static string RenderLineAsHTML(
            string text,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            string guildID
        )
        {
            // У нас есть блоки, порождающие саб-блоки:
            // - цитаты (они уже обработаны, вложенных быть не может)
            // - спойлеры
            // Другие подтипы не порождают саб-блоки, даже удаление
            var guild = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID];

            var waitDictionary = new Dictionary<string, string>();

            var textWithWaiting =
                RenderMarkdownBlockAsHTMLInnerBlock(text, chatOption, waitDictionary, guild, mentions);

            // Меняем все wait'ы внутри текста на значения из словаря
            // hint: Мы делаем это в бесконечном цикле, потому что маркировки могут быть вложенными
            // в произвольном порядке
            while (true)
            {
                // hint: Это можно сделать другим способом: Обходить waitDictionary, если смена произошла,
                // то удалять включение в waitDictionary и делать это пока waitDictionary.Any(),
                // это решение более наглядно, но более затратно, потому что приходится перестраивать Dictionary
                bool u = false;
                foreach (var (wait, replace) in waitDictionary)
                {
                    var textWithWaitingNew = textWithWaiting.Replace(wait, replace);
                    if (textWithWaitingNew != textWithWaiting)
                    {
                        textWithWaiting = textWithWaitingNew;
                        u = true;
                    }
                }

                if (!u)
                {
                    // Ни одной смены в цикле не было
                    break;
                }
            }

            return textWithWaiting;
        }

        /// <summary>
        /// Обработка разметки сообщения внутри логического блока сообщения (root-сообщение, цитата, спойлер)
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        private static string RenderMarkdownBlockAsHTMLInnerBlock(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            text = MarkNoFormatting(text, chatOption, waitDictionary, guild, mentions);
            text = MarkLinks(text, chatOption, waitDictionary, guild, mentions);
            text = MarkEmojiUnicode(text, chatOption, waitDictionary, guild, mentions);
            text = MarkEmojiImages(text, chatOption, waitDictionary, guild, mentions);
            text = MarkMentionsPeople(text, chatOption, waitDictionary, guild, mentions);
            text = MarkMentionsRole(text, chatOption, waitDictionary, guild, mentions);

            text = MarkSpoilers(text, chatOption, waitDictionary, guild, mentions);

            // simple mark

            text = MarkBold(text, chatOption, waitDictionary, guild, mentions);
            text = MarkItalic(text, chatOption, waitDictionary, guild, mentions);
            text = MarkDelete(text, chatOption, waitDictionary, guild, mentions);

            return text;
        }

        #region AtomicMarking

        /// <summary>
        /// Format (no mark)
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkNoFormatting(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            text = rWithoutMark.Replace(text, m1 =>
            {
                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<span class='without-mark'>{0}</span>",
                    HttpUtility.HtmlEncode(m1.Groups[1].Value));

                return wait;
            });

            return text;
        }

        /// <summary>
        /// Парсинг ссылок внутри текста
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkLinks(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            text = rLink.Replace(text, m1 =>
            {
                var wait = GetWaitString();
                var url = string.Format("{0}://{1}{2}{3}",
                    m1.Groups[2].Value,
                    m1.Groups[3].Value,
                    m1.Groups[4].Value,
                    m1.Groups[5].Value
                );
                // TODO: Иногда стирать всю ссылку, потому что она просто для превью
                // TODO: Править %D0%D... в нормальный текст
                waitDictionary[wait] = string.Format("<a href='{0}' target='_blank'>{0}</a>",
                    HttpUtility.HtmlEncode(url)
                );

                return m1.Groups[1].Value + wait;
            });

            return text;
        }

        /// <summary>
        /// Парсинг эмодзи из дополнительных plain'ов в Unicode
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkEmojiUnicode(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            if (!UnicodeEmojiEngine.emojiList[chatOption.unicode_emoji_displaying].Any())
            {
                return text;
            }

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

            return text;
        }

        /// <summary>
        /// Emoji (картинки)
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkEmojiImages(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            var thisGuildEmojis = new HashSet<string>();
            foreach (var emoji in guild.emojis)
            {
                thisGuildEmojis.Add(emoji.id);
            }

            // Эмодзи внутри текста
            text = rEmojiWithinText.Replace(text, m1 =>
            {
                // TODO: вычитать
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

            return text;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkMentionsPeople(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
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


            return text;
        }

        /// <summary>
        /// Упоминание роли в тексте
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkMentionsRole(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
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

            return text;
        }

        /// <summary>
        /// Спойлеры
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkSpoilers(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            text = rSpoilerMark.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format(
                    "<span class='spoiler {1}'><span class='spoiler-content'>{0}</span></span>",
                    subBlock,
                    (chatOption.text_spoiler == 1) ? "spoiler-show" : ""
                );

                return wait;
            });

            return text;
        }

        /// <summary>
        /// Жирный текст
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkBold(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            text = rBold.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<strong>{0}</strong>", subBlock);
                return wait;
            });

            return text;
        }

        /// <summary>
        /// Наклонный (курсивный) текст
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkItalic(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            text = rEm.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<em>{0}</em>", subBlock);
                return wait;
            });

            return text;
        }

        /// <summary>
        /// Зачёркнутый (удалённый) текст
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <returns></returns>
        public static string MarkDelete(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions
        )
        {
            text = rDelete.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions
                );

                // TODO: check if correct
                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<del>{0}</del>", subBlock);
                return wait;
            });

            return text;
        }

        #endregion

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
        private static readonly List<char> _waitAlphabet;

        static MessageMarkdownParser()
        {
            _waitAlphabet = new List<char>();
            for (int c = '0'; c <= '9'; c++)
            {
                _waitAlphabet.Add((char) c);
            }

            for (int c = 'a'; c <= 'z'; c++)
            {
                _waitAlphabet.Add((char) c);
            }

            for (int c = 'A'; c <= 'Z'; c++)
            {
                _waitAlphabet.Add((char) c);
            }
        }

        public static string GetWaitString()
        {
            lock (_stdRandomLock)
            {
                var s = "";
                int n = _waitAlphabet.Count - 1;
                for (int i = 0; i < 40; i++)
                {
                    var j = _stdRandom.Next(0, n);
                    s += _waitAlphabet[j];
                }

                return string.Format("{1}wait:{0}{2}", s, '{', '}');
            }
        }

        #endregion
    }
}