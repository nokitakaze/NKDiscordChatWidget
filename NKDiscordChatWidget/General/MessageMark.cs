using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace NKDiscordChatWidget.General
{
    public static class MessageMark
    {
        public static string RenderAsHTML(string text, ChatDrawOption chatOption, string guildID)
        {
            return text
                .Split("\n")
                .Aggregate("", (current, line) =>
                    current + string.Format("<div>{0}</div>",
                        RenderLineAsHTML(line.TrimEnd('\r'), chatOption, guildID)));
        }

        private static readonly Regex rLink = new Regex(
            @"(^|\W)([a-z]+)://([a-z0-9.-]+)([a-z0-9/%().+-]+)?",
            RegexOptions.Compiled
        );

        private static readonly Regex rWithoutMark = new Regex(
            @"`(.+?)`",
            RegexOptions.Compiled
        );

        private static readonly Regex rEmojiWithinText = new Regex(
            @"<\:(.+?)\:([0-9]+)>",
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

        private static string RenderLineAsHTML(string text, ChatDrawOption chatOption, string guildID)
        {
            var waitDictionary = new Dictionary<string, string>();
            var rnd = new Random();

            // Format
            text = rWithoutMark.Replace(text, m1 =>
            {
                var wait = string.Format("{1}wait:{0:F5}{2}", rnd.NextDouble(), '{', '}');
                waitDictionary[wait] = string.Format("<span class='without-mark'>{0}</span>",
                    HttpUtility.HtmlEncode(m1.Groups[1].Value)
                );

                return wait;
            });

            // Ссылка
            text = rLink.Replace(text, m1 =>
            {
                var wait = string.Format("{1}wait:{0:F5}{2}", rnd.NextDouble(), '{', '}');
                var url = string.Format("{0}://{1}{2}",
                    m1.Groups[2].Value,
                    m1.Groups[3].Value,
                    m1.Groups[4].Value
                );
                waitDictionary[wait] = string.Format("<a href='{0}' target='_blank'>{1}</a>",
                    HttpUtility.HtmlEncode(url),
                    HttpUtility.HtmlEncode(url)
                );

                return m1.Groups[1].Value + wait;
            });

            // Emoji
            var thisGuildEmojis = new HashSet<string>();
            var guild = NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID];
            foreach (var emoji in guild.emojis)
            {
                thisGuildEmojis.Add(emoji.id);
            }

            text = rEmojiWithinText.Replace(text, m1 =>
            {
                string emojiID = m1.Groups[2].Value;
                bool isRelative = thisGuildEmojis.Contains(emojiID);
                int emojiShow = isRelative ? chatOption.emoji_relative : chatOption.emoji_stranger;
                if (emojiShow == 2)
                {
                    return "  ";
                }

                var wait = string.Format("{1}wait:{0:F5}{2}", rnd.NextDouble(), '{', '}');
                var url = string.Format("https://cdn.discordapp.com/emojis/{0}.png", emojiID);

                waitDictionary[wait] = string.Format("<span class='emoji {2}'><img src='{0}' alt=':{1}:'></span>",
                    HttpUtility.HtmlEncode(url), HttpUtility.HtmlEncode(m1.Groups[1].Value),
                    (emojiShow == 1) ? "blur" : "");

                return wait;
            });

            // mark
            // Strong
            string html = rBold.Replace(text,
                m1 => string.Format("<strong>{0}</strong>", m1.Groups[1].Value));

            // Em
            html = rEm.Replace(html,
                m1 => string.Format("<em>{0}</em>", m1.Groups[1].Value));

            // wait
            foreach (var (wait, replace) in waitDictionary)
            {
                html = html.Replace(wait, replace);
            }

            return html;
        }
    }
}