using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using NKDiscordChatWidget.DiscordModel;
using NKDiscordChatWidget.Services.General;
using NKDiscordChatWidget.Services.Util;

namespace NKDiscordChatWidget.Services.Services
{
    public class MessageArtist
    {
        private readonly DiscordRepository Repository;
        private readonly MessageMarkdownParser Parser;

        public MessageArtist(DiscordRepository repository, MessageMarkdownParser parser)
        {
            Repository = repository;
            Parser = parser;
        }

        public AnswerMessage DrawMessage(EventMessageCreate message, ChatDrawOption chatDrawOption)
        {
            string htmlContent = string.Format("<div class='content-message' data-id='{1}'>{0}</div>",
                DrawMessageContent(message, chatDrawOption), message.id);
            var timeUpdate = (message.edited_timestampAsDT != DateTime.MinValue)
                ? message.edited_timestampAsDT
                : message.timestampAsDT;
            /*
            if (chatOption.merge_same_user_messages)
            {
                // Соединяем сообщения одного и того же человека
                // @todo где-то здесь баг имплементации. Надо поправить
                for (var j = i + 1; j < messages.Count; j++)
                {
                    if (messages[j].author.id == messages[i].author.id)
                    {
                        var localTimeUpdate = (messages[j].edited_timestampAsDT != DateTime.MinValue)
                            ? messages[j].edited_timestampAsDT
                            : messages[j].timestampAsDT;
                        if (localTimeUpdate > timeUpdate)
                        {
                            timeUpdate = localTimeUpdate;
                        }

                        htmlContent += string.Format("<div class='content-message' data-id='{1}'>{0}</div>",
                            DrawMessageContent(messages[j], chatOption), messages[j].id);
                        i = j;
                    }
                }
            }
            
            */

            string nickColor = "inherit";
            if ((message.member != null) && (message.member.roles.Any()))
            {
                var roles = Repository.guilds[message.guild_id].roles.ToList();
                roles.Sort((a, b) => b.position.CompareTo(a.position));
                var role = roles.FirstOrDefault(t => message.member.roles.Contains(t.id));
                if (role != null)
                {
                    nickColor = role.color.ToString("X");
                    nickColor = "#" + nickColor.PadLeft(6, '0');
                }
            }

            string sha1hash;
            using (var hashA = SHA1.Create())
            {
                byte[] data = hashA.ComputeHash(Encoding.UTF8.GetBytes(htmlContent));
                sha1hash = data.Aggregate("", (current, c) => current + c.ToString("x2"));
            }

            var drawDateTime = message.timestampAsDT.AddMinutes(chatDrawOption.timezone);

            var html = string.Format(
                "<div class='user'><img src='{0}' alt='{1}'></div>" +
                "<div class='content'>" +
                "<div class='content-header'><span class='content-user' style='color: {4};'>{1}</span><span class='content-time'>{3:HH:mm:ss dd.MM.yyyy}</span></div>" +
                "{2}" +
                "</div><div style='clear: both;'></div><hr>",
                HttpUtility.HtmlEncode(message.author.avatarURL),
                HttpUtility.HtmlEncode(
                    (message.member != null) && !string.IsNullOrEmpty(message.member.nick)
                        ? message.member.nick
                        : message.author.username
                ),
                htmlContent,
                drawDateTime,
                nickColor
            );

            return new AnswerMessage()
            {
                id = message.id,
                time = ((DateTimeOffset)message.timestampAsDT).ToUnixTimeMilliseconds() * 0.001d,
                time_update = ((DateTimeOffset)timeUpdate).ToUnixTimeMilliseconds() * 0.001d,
                html = html,
                hash = sha1hash,
            };
        }

        private string DrawMessageContent(EventMessageCreate message, ChatDrawOption chatOption)
        {
            var usedEmbedsUrls = new HashSet<string>();
            foreach (var embed in message.embeds)
            {
                usedEmbedsUrls.Add(embed.url);
            }

            var guildID = message.guild_id;
            var guild = Repository.guilds[guildID];
            var thisGuildEmojis = new HashSet<string>(guild.emojis.Select(emoji => emoji.id).ToList());

            // Основной текст
            string directContentHTML = Parser.RenderMarkdownAsHTML(
                message.content, chatOption, message.mentions, guildID, usedEmbedsUrls);
            bool containOnlyUnicodeAndSpace;
            {
                var rEmojiWithinText = new Regex(@"<\:(.+?)\:([0-9]+)>", RegexOptions.Compiled);
                long[] longs = { };
                if (message.content != null)
                {
                    longs = Utf8ToUnicode.ToUnicodeCode(rEmojiWithinText.Replace(message.content, ""));
                }

                containOnlyUnicodeAndSpace = Utf8ToUnicode.ContainOnlyUnicodeAndSpace(longs);
            }
            string html = string.Format("<div class='content-direct {1}'>{0}</div>",
                directContentHTML, containOnlyUnicodeAndSpace ? "only-emoji" : "");

            // attachments
            if ((message.attachments != null) && message.attachments.Any() && (chatOption.attachments != 2))
            {
                string attachmentHTML = "";

                foreach (var attachment in message.attachments)
                {
                    var extension = attachment.filename.Split('.').Last().ToLowerInvariant();
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (extension)
                    {
                        case "mp4":
                        case "webm":
                            // TODO: Отображать размер видео
                            attachmentHTML += string.Format(
                                "<div class='attachment {1}'><div class='attachment-wrapper'><video><source src='{0}'></video></div></div>",
                                HttpUtility.HtmlEncode(attachment.proxy_url),
                                ((chatOption.attachments == 1) || attachment.IsSpoiler) ? "blur" : ""
                            );
                            break;
                        case "jpeg":
                        case "jpg":
                        case "bmp":
                        case "gif":
                        case "png":
                            attachmentHTML += string.Format(
                                "<div class='attachment {3}'><div class='attachment-wrapper'><img src='{0}' data-width='{1}' data-height='{2}'></div></div>",
                                HttpUtility.HtmlEncode(attachment.proxy_url),
                                attachment.width,
                                attachment.height,
                                ((chatOption.attachments == 1) || attachment.IsSpoiler) ? "blur" : ""
                            );
                            break;
                    }
                }

                html += string.Format("<div class='attachment-block'>{0}</div>", attachmentHTML);
            }

            // Preview
            if ((message.embeds != null) && message.embeds.Any() && (chatOption.link_preview != 2))
            {
                string previewHTML = "";

                foreach (var embed in message.embeds)
                {
                    var subHTML = "";
                    if (embed.provider != null)
                    {
                        subHTML += string.Format("<div class='provider'>{0}</div>",
                            HttpUtility.HtmlEncode(embed.provider.name));
                    }

                    if (embed.author != null)
                    {
                        subHTML += string.Format("<div class='author'>{0}</div>",
                            HttpUtility.HtmlEncode(embed.author.name));
                    }

                    subHTML += string.Format("<div class='title'>{0}</div>",
                        HttpUtility.HtmlEncode(embed.title));

                    if (embed.thumbnail != null)
                    {
                        subHTML += string.Format("<div class='preview'><img src='{0}' alt='{1}'></div>",
                            HttpUtility.HtmlEncode(embed.thumbnail.proxy_url),
                            HttpUtility.HtmlEncode("Превью для «" + embed.title + "»")
                        );
                    }

                    var nickColor = embed.color.ToString("X");
                    nickColor = "#" + nickColor.PadLeft(6, '0');

                    previewHTML += string.Format(
                        "<div class='embed {2}'><div class='embed-pill' style='background-color: {1};'></div>" +
                        "<div class='embed-content {3}'>{0}</div></div>",
                        subHTML,
                        nickColor,
                        (chatOption.link_preview == 1) ? "blur" : "",
                        string.IsNullOrEmpty(embed.title) ? "embed-content_no-title" : ""
                    );
                }

                html += string.Format("<div class='embed-block'>{0}</div>", previewHTML);
            }

            // Реакции
            if (
                (message.reactions != null) && message.reactions.Any() &&
                ((chatOption.message_relative_reaction != 2) || (chatOption.message_stranger_reaction != 2))
            )
            {
                // Реакции
                var reactionHTMLs = new List<string>();
                foreach (var reaction in message.reactions)
                {
                    bool isRelative = ((reaction.emoji.id == null) || thisGuildEmojis.Contains(reaction.emoji.id));
                    int emojiShow = isRelative
                        ? chatOption.message_relative_reaction
                        : chatOption.message_stranger_reaction;
                    if (emojiShow == 2) // @todo убрать магическую константу
                    {
                        continue;
                    }

                    AddMessageReactionHTML(
                        reactionHTMLs,
                        reaction,
                        emojiShow,
                        chatOption
                    );
                }

                var s = reactionHTMLs.Aggregate("", (current, s1) => current + s1);
                html += string.Format("<div class='content-reaction'>{0}</div>", s);
            }

            return html;
        }

        #region Reactions

        private void AddMessageReactionHTML(
            ICollection<string> reactionHTMLs,
            NKDiscordChatWidget.DiscordModel.EventMessageCreate.EventMessageCreate_Reaction reaction,
            int emojiShow,
            ChatDrawOption chatOption
        )
        {
            if (reaction.emoji.id != null)
            {
                // Эмодзи из Дискорда (паки эмодзей с серверов)
                reactionHTMLs.Add(string.Format(
                    "<div class='emoji {2}'><img src='{0}' alt=':{1}:'><span class='count'>{3}</span></div>",
                    HttpUtility.HtmlEncode(reaction.emoji.URL),
                    HttpUtility.HtmlEncode(reaction.emoji.name),
                    (emojiShow == 1) ? "blur" : "",
                    reaction.count
                ));
            }
            else
            {
                // Стандартные Unicode-эмодзи
                string emojiHtml;
                var emojiPack = chatOption.unicode_emoji_displaying;
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (emojiPack != EmojiPackType.StandardOS)
                {
                    emojiHtml = AddMessageReactionHTMLWithEmojiPack(reaction, emojiPack);
                }
                else
                {
                    emojiHtml = HttpUtility.HtmlEncode(reaction.emoji.name);
                }

                reactionHTMLs.Add(string.Format(
                    "<div class='emoji {1}'>{0}<span class='count'>{2}</span></div>",
                    emojiHtml,
                    (emojiShow == 1) ? "blur" : "",
                    reaction.count
                ));
            }
        }

        private string AddMessageReactionHTMLWithEmojiPack(
            NKDiscordChatWidget.DiscordModel.EventMessageCreate.EventMessageCreate_Reaction reaction,
            EmojiPackType emojiPack
        )
        {
            var longs = Utf8ToUnicode.ToUnicodeCode(reaction.emoji.name);
            var u = longs.Any(code => !UnicodeEmojiEngine.IsInIntervalEmoji(code, emojiPack));

            if (u)
            {
                // Реакция без ID, но при этом не является эмодзи, рисуем как есть
                return HttpUtility.HtmlEncode(reaction.emoji.name);
            }

            // Реакция без ID и является эмодзи, поэтому рисуем как картинку
            var localEmojiList = UnicodeEmojiEngine.RenderEmojiAsStringList(
                emojiPack, longs);
            var emojiHtml = "";
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var item in localEmojiList)
            {
                if (!item.isSuccess)
                {
                    // Этого символа нет в паке, выводим как есть. Куда деваться
                    emojiHtml += HttpUtility.HtmlEncode(item.rawText);
                    continue;
                }

                // hint: localEmojiList.Count может быть больше 1 в случае сложных эмодзи типа :one:
                var emojiSubFolderName = UnicodeEmojiEngine.GetImageSubFolder(emojiPack);
                var emojiExtension = UnicodeEmojiEngine.GetImageExtension(emojiPack);
                var url = string.Format("/images/emoji/{0}/{1}.{2}",
                    emojiSubFolderName,
                    item.emojiCode,
                    emojiExtension
                );

                emojiHtml += string.Format("<img src='{0}' alt=':{1}:'>",
                    HttpUtility.HtmlEncode(url),
                    HttpUtility.HtmlEncode(reaction.emoji.name)
                );
            }

            return emojiHtml;
        }

        #endregion
    }

    // ReSharper disable NotAccessedField.Global
    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    // ReSharper disable CollectionNeverQueried.Global
    public class AnswerMessage
    {
        public string id { get; set; }
        public double time { get; set; }
        public double time_update { get; set; }
        public string html { get; set; }

        /// <summary>
        /// Уникальный хеш, взятый от HTML-контента сообщения
        /// </summary>
        public string hash { get; set; }
    }

    public class AnswerFull
    {
        public List<AnswerMessage> messages { get; } = new List<AnswerMessage>();
        public double time_answer { get; set; }
        public HashSet<string> existedID { get; } = new HashSet<string>();
        public long time_server_start { get; set; }

        /// <summary>
        /// Заголовок для окна виджета (нужно только для дебага этого виджета)
        /// </summary>
        public string channel_title { get; set; }

        private static readonly long TimeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public AnswerFull()
        {
            this.time_server_start = TimeStart;
        }
    }
    // ReSharper restore CollectionNeverQueried.Global
    // ReSharper restore UnusedAutoPropertyAccessor.Global
    // ReSharper restore NotAccessedField.Global
    // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global
}