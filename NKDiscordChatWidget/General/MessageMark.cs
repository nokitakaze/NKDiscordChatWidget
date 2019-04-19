using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace NKDiscordChatWidget.General
{
    public static class MessageMark
    {
        public static string RenderAsHTML(string text)
        {
            return text
                .Split("\n")
                .Aggregate("", (current, line) => current + RenderLineAsHTML(line.TrimEnd('\r')));
        }

        private static readonly Regex rLink = new Regex(
            @"(^|\W)([a-z]+)://([a-z0-9.-]+)([a-z0-9/%().+-]+)?",
            RegexOptions.Compiled
        );

        private static readonly Regex rWithoutMark = new Regex(
            @"`(.+?)`",
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

        private static string RenderLineAsHTML(string text)
        {
            var links = new Dictionary<string, string>();
            var rnd = new Random();

            // Format
            text = rWithoutMark.Replace(text, m1 =>
            {
                var wait = string.Format("{1}wait:{0:F5}{2}", rnd.NextDouble(), '{', '}');
                links[wait] = string.Format("<div class='without-mark'>{0}</div>",
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
                links[wait] = string.Format("<a href='{0}' target='_blank'>{1}</a>",
                    HttpUtility.HtmlEncode(url),
                    HttpUtility.HtmlEncode(url)
                );

                return m1.Groups[1].Value + wait;
            });

            // mark
            // Strong
            string html = rBold.Replace(text,
                m1 =>
                {
                    // ReSharper disable once ConvertToLambdaExpression
                    return string.Format("<strong>{0}</strong>", m1.Groups[1].Value);
                });

            // Em
            html = rEm.Replace(html,
                m1 =>
                {
                    // ReSharper disable once ConvertToLambdaExpression
                    return string.Format("<em>{0}</em>", m1.Groups[1].Value);
                });

            // wait
            foreach (var (wait, replace) in links)
            {
                text = text.Replace(wait, replace);
            }

            return html;
        }
    }
}