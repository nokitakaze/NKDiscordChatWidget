using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using NKDiscordChatWidget.DiscordBot.Classes;

namespace NKDiscordChatWidget.General
{
    /// <summary>
    /// Парсер markdown
    /// </summary>
    /// <description>
    /// Дискорд меняет правила, поэтому парсер не такой как по спеке
    /// https://daringfireball.net/projects/markdown/syntax
    /// </description>
    [SuppressMessage("ReSharper", "UseStringInterpolation")]
    public static class MessageMarkdownParser
    {
        /// <summary>
        /// Получаем сырой текст с маркдауном и превращаем его в HTML-код
        /// </summary>
        /// <param name="text">Многострочный сырой текст с маркдауном</param>
        /// <param name="chatOption"></param>
        /// <param name="mentions"></param>
        /// <param name="guildID"></param>
        /// <param name="usedEmbedsUrls"></param>
        /// <returns>HTML-код, который можно безопасно рендерить в чате</returns>
        public static string RenderMarkdownAsHTML(
            string text,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            string guildID,
            HashSet<string> usedEmbedsUrls
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
                        RenderLineAsHTML(trimmedLine.Substring(2), chatOption, mentions, guildID, usedEmbedsUrls));
                    isInQuote = true;
                    continue;
                }

                if (isInQuote)
                {
                    // Только что шёл режим цитаты, но на этой строке он кончился

                    // hint: цитата может быть пустой, но это всё равно цитата, поэтому тут не просто
                    // проверка на currentQuoteHTML != ""
                    result += string.Format(
                        "<div class='quote-block'><div class='quote-border'></div><div class='quote-content'>{0}</div></div>",
                        currentQuoteHTML
                    );
                    currentQuoteHTML = "";
                    isInQuote = false;
                }

                // Это не кусок цитаты
                result += string.Format("<div class='line'>{0}</div>",
                    RenderLineAsHTML(trimmedLine, chatOption, mentions, guildID, usedEmbedsUrls));
            }

            if (isInQuote)
            {
                // Последняя строка текста это цитата
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

        private static readonly Regex rDoubleUnderscore = new Regex(
            @"__(.+?)__",
            RegexOptions.Compiled
        );

        private static readonly Regex rTripleUnderscore = new Regex(
            @"___(.+?)___",
            RegexOptions.Compiled
        );

        private static readonly Regex rBold = new Regex(
            @"\*\*(.+?)\*\*",
            RegexOptions.Compiled
        );

        private static readonly Regex rSingleAsterisk = new Regex(
            @"\*(.+)\*",
            RegexOptions.Compiled
        );

        private static readonly Regex rItalicUnderscore = new Regex(
            @"_(.+?)_",
            RegexOptions.Compiled
        );

        private static readonly Regex rBoldItalic = new Regex(
            @"\*\*\*(.+?)\*\*\*",
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
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns>Возвращает санированный HTML</returns>
        private static string RenderLineAsHTML(
            string text,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            string guildID,
            HashSet<string> usedEmbedsUrls
        )
        {
            // У нас есть блоки, порождающие саб-блоки:
            // - цитаты (они уже обработаны, вложенных быть не может)
            // - спойлеры
            // Другие подтипы не порождают саб-блоки, даже удаление
            var guild = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID];

            var waitDictionary = new Dictionary<string, string>();
            // todo mentions -> wait
            // todo HttpUtility.HtmlEncode

            var textWithWaiting =
                RenderMarkdownBlockAsHTMLInnerBlock(text, chatOption, waitDictionary, guild, mentions, usedEmbedsUrls);

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
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        private static string RenderMarkdownBlockAsHTMLInnerBlock(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            while (true)
            {
                var patternOptions = new List<( Regex regexp, Func<string> deleg )>
                {
                    (rWithoutMark, () => MarkNoFormatting(text, chatOption, waitDictionary)),
                    (rLink, () => MarkLinks(text, chatOption, waitDictionary, usedEmbedsUrls)),
                    (rEmojiWithinText, () => MarkEmojiImages(text, chatOption, waitDictionary, guild)),

                    // mentions
                    (rMentionNick, () => MarkMentionsPeople(text, chatOption, waitDictionary, guild, mentions)),
                    (rMentionRole, () => MarkMentionsRole(text, chatOption, waitDictionary, guild)),

                    // simple mark
                    (rSpoilerMark, () => MarkSpoilers(text, chatOption, waitDictionary, guild,
                        mentions, usedEmbedsUrls)),
                    (rBoldItalic, () => MarkBoldItalic(text, chatOption, waitDictionary, guild,
                        mentions, usedEmbedsUrls)),
                    (rBold, () => MarkBold(text, chatOption, waitDictionary, guild,
                        mentions, usedEmbedsUrls)),

                    (rTripleUnderscore, () => MarkUnderscoreItalic(text, chatOption, waitDictionary, guild,
                        mentions, usedEmbedsUrls)),
                    (rDoubleUnderscore, () => MarkUnderscore(text, chatOption, waitDictionary, guild,
                        mentions, usedEmbedsUrls)),
                    (rSingleAsterisk, () => MarkItalicViaAsterisk(text, chatOption, waitDictionary, guild,
                        mentions, usedEmbedsUrls)),
                    (rItalicUnderscore, () => MarkItalicViaUnderscore(text, chatOption, waitDictionary, guild,
                        mentions, usedEmbedsUrls)),
                    (rDelete, () => MarkDelete(text, chatOption, waitDictionary, guild, mentions, usedEmbedsUrls)),
                };

                var matches = new List<(int index, Func<string> deleg)>();
                foreach (var (regex, deleg) in patternOptions)
                {
                    var m = regex.Match(text);
                    if (!m.Success)
                    {
                        continue;
                    }

                    matches.Add((m.Index, deleg: deleg));
                    if (m.Index == 0)
                    {
                        break;
                    }
                }

                if (!matches.Any())
                {
                    // Не осталось маркировки, выходим

                    break;
                }

                matches.Sort((a, b) => a.index.CompareTo(b.index));

                var u = false;
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (var (_, delegateLocal) in matches)
                {
                    var result = delegateLocal.Invoke();
                    // ReSharper disable once InvertIf
                    if (result != text)
                    {
                        text = result;
                        u = true;
                        break;
                    }
                }

                if (!u)
                {
                    // hint: Это warning однозначный
                    break;
                }
            }

            text = MarkEmojiUnicode(text, chatOption, waitDictionary);

            return text;
        }

        #region AtomicMarking

        /// <summary>
        /// Format (no mark)
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <returns></returns>
        public static string MarkNoFormatting(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary
        )
        {
            // TODO: double backtick

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
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkLinks(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            HashSet<string> usedEmbedsUrls
        )
        {
            var r = new Regex("(%[0-9a-f]{2,2})+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            text = rLink.Replace(text, m1 =>
            {
                var wait = GetWaitString();
                var url = string.Format("{0}://{1}{2}{3}",
                    m1.Groups[2].Value,
                    m1.Groups[3].Value,
                    m1.Groups[4].Value,
                    m1.Groups[5].Value
                );

                if ((chatOption.hide_used_embed_links == 1) && (usedEmbedsUrls.Contains(url)))
                {
                    return m1.Groups[1].Value;
                }

                var rawPath = m1.Groups[4].Value;
                while (true)
                {
                    var m = r.Match(rawPath);
                    if (!m.Success)
                    {
                        break;
                    }

                    var s = m.Groups[0].Value;
                    var bytes = new List<byte>();
                    while (s != "")
                    {
                        var hex = s.Substring(1, 2);
                        s = s.Substring(3);

                        var b = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                        bytes.Add(b);
                    }

                    var s1 = rawPath.Substring(0, m.Index);
                    var s2 = Encoding.UTF8.GetString(bytes.ToArray());
                    var s3 = rawPath.Substring(m.Index + m.Length);
                    rawPath = s1 + s2 + s3;
                }

                var anchor = HttpUtility.HtmlEncode(string.Format("{0}://{1}{2}{3}",
                    m1.Groups[2].Value,
                    m1.Groups[3].Value,
                    rawPath,
                    m1.Groups[5].Value
                ));

                if ((chatOption.short_anchor == 1) && (anchor.Length > 40))
                {
                    anchor = anchor.Substring(0, 37) + "...";
                }

                // TODO: Иногда стирать всю ссылку, потому что она просто для превью картинки/видео
                waitDictionary[wait] = string.Format(
                    "<a href='{0}' target='_blank'>{1}</a>",
                    HttpUtility.HtmlEncode(url),
                    HttpUtility.HtmlEncode(anchor)
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
        /// <returns></returns>
        public static string MarkEmojiUnicode(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary
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
                        // У нас непустой буффер emoji символов, надо их записать в строку
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
        /// <returns></returns>
        public static string MarkEmojiImages(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild
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
                string emojiID = m1.Groups[3].Value;
                bool isRelative = thisGuildEmojis.Contains(emojiID);
                int emojiShow = isRelative ? chatOption.emoji_relative : chatOption.emoji_stranger;
                if (emojiShow == 2)
                {
                    return ":" + m1.Groups[2].Value + ":";
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
        /// Упоминание в сообщении конкретного человека
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
                    return string.Format("&lt;User Unknown #{0}&gt;", mentionID);
                }

                // ReSharper disable once SuggestVarOrType_SimpleTypes
                EventMessageCreate.EventMessageCreate_Mention mention =
                    mentions.FirstOrDefault(eMention => eMention.id == mentionID);

                if (mention == null)
                {
                    return string.Format("&lt;User Unknown #{0}&gt;", mentionID);
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
        /// Упоминание в тексте целой роли с сервера
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <returns></returns>
        public static string MarkMentionsRole(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild
        )
        {
            text = rMentionRole.Replace(text, m1 =>
            {
                string roleID = m1.Groups[1].Value;

                //
                var role = guild.roles.FirstOrDefault(t => t.id == roleID);
                if (role == null)
                {
                    return string.Format("&lt;Role Unknown #{0}&gt;", roleID);
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
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkSpoilers(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rSpoilerMark.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
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
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkBold(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rBold.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<strong>{0}</strong>", subBlock);
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
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkUnderscoreItalic(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rTripleUnderscore.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<em><u>{0}</u></em>", subBlock);
                return wait;
            });

            return text;
        }

        /// <summary>
        /// Наклонный (курсивный) текст через звёздочки
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkUnderscore(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rDoubleUnderscore.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<u>{0}</u>", subBlock);
                return wait;
            });

            return text;
        }

        /// <summary>
        /// Наклонный (курсивный) текст через звёздочки
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkItalicViaAsterisk(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rSingleAsterisk.Replace(text, m1 =>
            {
                var firstChar = m1.Groups[1].Value.Substring(0, 1);
                if (firstChar == " ")
                {
                    return m1.Groups[0].Value;
                }

                var lastChar = m1.Groups[1].Value.Substring(m1.Groups[1].Value.Length - 1);
                if (lastChar == " ")
                {
                    return m1.Groups[0].Value;
                }

                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<em>{0}</em>", subBlock);
                return wait;
            });

            return text;
        }

        /// <summary>
        /// Наклонный (курсивный) текст через одиночное подчёркивание
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkItalicViaUnderscore(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rItalicUnderscore.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<em>{0}</em>", subBlock);
                return wait;
            });

            return text;
        }

        /// <summary>
        /// Жирный наклонный текст
        /// </summary>
        /// <param name="text">Текст с сырым Markdown</param>
        /// <param name="chatOption">Опции чата, заданные стримером для виджета</param>
        /// <param name="waitDictionary">Dictionary для саб-блоков</param>
        /// <param name="guild">Гильдия (сервер), внутри которого написано сообщение</param>
        /// <param name="mentions">Список упоминаний, сделанных в сообщении</param>
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkBoldItalic(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rBoldItalic.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
                );

                var wait = GetWaitString();
                waitDictionary[wait] = string.Format("<strong><em>{0}</em></strong>", subBlock);
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
        /// <param name="usedEmbedsUrls">Список использованных Url'ов в embed</param>
        /// <returns></returns>
        public static string MarkDelete(
            string text,
            ChatDrawOption chatOption,
            Dictionary<string, string> waitDictionary,
            EventGuildCreate guild,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            HashSet<string> usedEmbedsUrls
        )
        {
            text = rDelete.Replace(text, m1 =>
            {
                var subBlock = RenderMarkdownBlockAsHTMLInnerBlock(
                    m1.Groups[1].Value,
                    chatOption,
                    waitDictionary,
                    guild,
                    mentions,
                    usedEmbedsUrls
                );

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

        private static readonly List<char> _waitAlphabet;

        static MessageMarkdownParser()
        {
            _waitAlphabet = new List<char>();
            for (int c = '0'; c <= '9'; c++)
            {
                _waitAlphabet.Add((char)c);
            }

            for (int c = 'a'; c <= 'z'; c++)
            {
                _waitAlphabet.Add((char)c);
            }

            for (int c = 'A'; c <= 'Z'; c++)
            {
                _waitAlphabet.Add((char)c);
            }
        }

        public static string GetWaitString()
        {
            var rnd = new Random();

            var s = "";
            int n = _waitAlphabet.Count;
            for (int i = 0; i < 40; i++)
            {
                var j = rnd.Next(0, n);
                s += _waitAlphabet[j];
            }

            return string.Format("{1}wait:{0}{2}", s, '{', '}');
        }

        #endregion
    }
}