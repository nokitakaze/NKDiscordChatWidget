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
            @"(^|\W)([a-z]+)://([a-z0-9.-]+)([a-z0-9/%().+&=-]+)?(\?[a-z0-9/%().+&=-]*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex rWithoutMark = new Regex(
            @"`(.+?)`",
            RegexOptions.Compiled
        );

        private static readonly Regex rEmojiWithinText = new Regex(
            @"<\:(.+?)\:([0-9]+)>",
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

        private static string RenderLineAsHTML(
            string text,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            string guildID
        )
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
                var url = string.Format("{0}://{1}{2}{3}",
                    m1.Groups[2].Value,
                    m1.Groups[3].Value,
                    m1.Groups[4].Value,
                    m1.Groups[5].Value
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
            text = rMentionNick.Replace(text, m1 =>
            {
                string mentionID = m1.Groups[1].Value;
                EventMessageCreate.EventMessageCreate_Mention mention =
                    mentions.FirstOrDefault(eMention => eMention.id == mentionID);

                if (mention == null)
                {
                    return string.Format("<Пользователь Unknown #{0}>", mentionID);
                }

                var nickColor = "inherit";
                {
                    var mention_roles_local =
                        guild.roles.Where(t => mention.member.roles.Contains(t.id)).ToList();
                    mention_roles_local.Sort((a, b) => a.position.CompareTo(b.position));
                    var role = mention_roles_local.Any() ? mention_roles_local.First() : null;
                    if (role != null)
                    {
                        nickColor = role.color.ToString("X");
                        nickColor = "#" + nickColor.PadLeft(6, '0');
                    }
                }
                //

                var wait = string.Format("{1}wait:{0:F5}{2}", rnd.NextDouble(), '{', '}');

                waitDictionary[wait] = string.Format("<span class='user mention' style='color: {1};'>@{0}</span>",
                    HttpUtility.HtmlEncode(mention.username),
                    nickColor
                );

                return wait;
            });
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
                var wait = string.Format("{1}wait:{0:F5}{2}", rnd.NextDouble(), '{', '}');

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

            // wait
            foreach (var (wait, replace) in waitDictionary)
            {
                html = html.Replace(wait, replace);
            }

            return html;
        }
    }
}