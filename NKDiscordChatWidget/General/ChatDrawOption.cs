using System;
using System.Collections.Generic;
using NKDiscordChatWidget.Util;

namespace NKDiscordChatWidget.General
{
    // @todo Сделать атрибут "read only", чтобы нельзя было менять из URL'ов

    public class ChatDrawOption
    {
        // ReSharper disable UnassignedField.Global
        // ReSharper disable ConvertToConstant.Global
        // ReSharper disable FieldCanBeMadeReadOnly.Global
        public bool merge_same_user_messages; // @todo Временно игнорируется

        /// <summary>
        /// Аттачи в чате
        /// </summary>
        public int attachments;

        /// <summary>
        /// Превью для ссылок
        /// </summary>
        public int link_preview;

        /// <summary>
        /// Реакции с этого сервера для сообщений
        /// </summary>
        public int message_relative_reaction;

        /// <summary>
        /// Реакции с чужого сервера для сообщений
        /// </summary>
        public int message_stranger_reaction;

        /// <summary>
        /// Emoji с чужого сервера внутри текста
        /// </summary>
        public int emoji_stranger;

        /// <summary>
        /// Emoji с этого сервера внутри текста
        /// </summary>
        public int emoji_relative;

        /// <summary>
        /// Показывать текстовые спойлеры
        /// </summary>
        public int text_spoiler;

        /// <summary>
        /// Цвет ников в упоминаниях в сообщениях
        /// </summary>
        public int message_mentions_style = 1;

        /// <summary>
        /// Временное смещение в минутах относительно Гринвича. I.e. Москва = + 180
        /// </summary>
        public int timezone;

        /// <summary>
        /// Нужно ли сокращать анкор до 40 символов
        /// </summary>
        public int short_anchor = 1;

        /// <summary>
        /// Удалять ссылки на контент, который был вставлен (embed)
        /// </summary>
        public int hide_used_embed_links = 1;

        /// <summary>
        /// Какой пак эмодзи отображать
        /// </summary>
        public EmojiPackType unicode_emoji_displaying = EmojiPackType.Twemoji;
        // ReSharper restore FieldCanBeMadeReadOnly.Global
        // ReSharper restore ConvertToConstant.Global
        // ReSharper restore UnassignedField.Global

        public void SetOptionsFromDictionary(Dictionary<string, object> newChatDrawOption)
        {
            // @todo Сделать атрибут "read only", чтобы нельзя было менять из URL'ов
            foreach (var field in this.GetType().GetFields())
            {
                if (!newChatDrawOption.ContainsKey(field.Name))
                {
                    // Такого поля нет в настройках
                    continue;
                }

                var value = newChatDrawOption[field.Name];

                switch (field.FieldType.FullName)
                {
                    case "System.Boolean":
                    {
                        switch (value)
                        {
                            case int i:
                                field.SetValue(this, (i > 0));
                                break;
                            case long l:
                                field.SetValue(this, (l > 0));
                                break;
                            case string s:
                                if (int.TryParse(s, out var parsedI))
                                {
                                    field.SetValue(this, (parsedI > 0));
                                }
                                else
                                {
                                    Console.Error.WriteLine(
                                        "ChangeDrawOption object has field {0} with type {1}, but raw value is ({3}) ({2})",
                                        field.Name,
                                        field.FieldType.FullName,
                                        value.GetType().FullName,
                                        value
                                    );
                                }

                                break;
                            default:
                                Console.Error.WriteLine(
                                    "ChangeDrawOption object has field {0} with type {1}, but raw value is {2}",
                                    field.Name,
                                    field.FieldType.FullName,
                                    value.GetType().FullName
                                );
                                break;
                        }

                        break;
                    }
                    case "NKDiscordChatWidget.General.EmojiPackType": // @todo сделать правильно, через Enum
                    {
                        int valueInt = 0;
                        switch (value)
                        {
                            case int i:
                                valueInt = i;
                                break;
                            case long l:
                                valueInt = (int)l;
                                break;
                            case bool u:
                                valueInt = u ? 1 : 0;
                                break;
                            case string s:
                                field.SetValue(this, int.TryParse(s, out valueInt) ? valueInt : 0);

                                break;
                            default:
                                Console.Error.WriteLine(
                                    "ChangeDrawOption object has field {0} with type {1}, but raw value is {2}",
                                    field.Name,
                                    field.FieldType.FullName,
                                    value.GetType().FullName
                                );
                                break;
                        }

                        field.SetValue(this, (EmojiPackType)valueInt);

                        break;
                    }
                    case "System.Int32":
                    {
                        switch (value)
                        {
                            case int i:
                                field.SetValue(this, i);
                                break;
                            case long l:
                                field.SetValue(this, (int)l);
                                break;
                            case string s: // hint: Самый частый вариант мутаций
                                field.SetValue(this, int.TryParse(s, out var valueInt) ? valueInt : 0);
                                break;
                            case bool u:
                                field.SetValue(this, u ? 1 : 0);
                                break;
                            default:
                                Console.Error.WriteLine(
                                    "ChangeDrawOption object has field {0} with type {1}, but raw value is {2}",
                                    field.Name,
                                    field.FieldType.FullName,
                                    value.GetType().FullName
                                );
                                break;
                        }

                        break;
                    }
                    case "System.Int64": // hint: На данный момент таких полей в настройках нет
                    {
                        switch (value)
                        {
                            case int i:
                                field.SetValue(this, i);
                                break;
                            case long l:
                                field.SetValue(this, l);
                                break;
                            case string s:
                                field.SetValue(this, long.TryParse(s, out var valueInt) ? valueInt : 0);
                                break;
                            case bool u:
                                field.SetValue(this, u ? 1 : 0);
                                break;
                            default:
                                Console.Error.WriteLine(
                                    "ChangeDrawOption object has field {0} with type {1}, but raw value is {2}",
                                    field.Name,
                                    field.FieldType.FullName,
                                    value.GetType().FullName
                                );
                                break;
                        }

                        break;
                    }
                    // @todo string (сейчас строк в настройках не используется, поэтому и не обрабатывается)
                    default:
                        Console.Error.WriteLine(
                            "ChangeDrawOption object has field {0} with unexpected type {1}",
                            field.Name, field.FieldType.FullName);
                        break;
                }
            }
        }
    }
}