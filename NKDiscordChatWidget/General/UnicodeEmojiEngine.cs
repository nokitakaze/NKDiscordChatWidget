using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NKDiscordChatWidget.General
{
    public static class UnicodeEmojiEngine
    {
        public static readonly Dictionary<EmojiPackType, string[]> emojiList =
            new Dictionary<EmojiPackType, string[]>();

        public static readonly Dictionary<EmojiPackType, HashSet<long>> emojiCodesList =
            new Dictionary<EmojiPackType, HashSet<long>>();

        public static string GetImageExtension(EmojiPackType pack)
        {
            switch (pack)
            {
                case EmojiPackType.Twemoji:
                    return "svg";
                case EmojiPackType.JoyPixels:
                    return "png";
                case EmojiPackType.StandardOS:
                    return "";
                default:
                    return "";
            }
        }

        public static string GetImageSubFolder(EmojiPackType pack)
        {
            switch (pack)
            {
                case EmojiPackType.Twemoji:
                    return "twemoji";
                case EmojiPackType.JoyPixels:
                    return "joypixels";
                case EmojiPackType.StandardOS:
                    return "";
                default:
                    return "";
            }
        }

        public static void LoadAllEmojiPacks(string WWWRoot)
        {
            // Так делать нельзя, но если очень хочется, то можно
            foreach (var pack in new[] {EmojiPackType.JoyPixels, EmojiPackType.Twemoji})
            {
                var subFolder = GetImageSubFolder(pack);
                var extension = GetImageExtension(pack);
                emojiList[pack] =
                    GetEmojiPacks(string.Format("{0}/images/emoji/{1}", WWWRoot, subFolder), extension);
            }

            foreach (var id in Enum.GetValues(typeof(EmojiPackType)))
            {
                if (!emojiList.ContainsKey((EmojiPackType) id))
                {
                    emojiList[(EmojiPackType) id] = new string[] { };
                    emojiCodesList[(EmojiPackType) id] = new HashSet<long>();
                    continue;
                }

                var rawList = new HashSet<long>();
                foreach (var codeString in emojiList[(EmojiPackType) id])
                {
                    var a = codeString.Split("-");
                    foreach (var singleStringCode in a)
                    {
                        var code = int.Parse(singleStringCode, NumberStyles.HexNumber);
                        if (!IsInStandardUnicodeEmoji(code))
                        {
                            rawList.Add(code);
                        }
                    }
                }

                emojiCodesList[(EmojiPackType) id] = rawList;
            }
        }

        private static string[] GetEmojiPacks(string folder, string expectedExtension)
        {
            var list = new List<string>();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var fileName in Directory.EnumerateFiles(folder))
            {
                var a = fileName.Replace("\\", "/").Split("/");
                var filenameItself = a.Last();
                a = filenameItself.Split(".");

                var extension = a.Last();
                if (extension != expectedExtension)
                {
                    continue;
                }

                var nameBefore = a.First();
                list.Add(nameBefore);
            }

            return list.ToArray();
        }

        public static string GetStringForCodes(long code)
        {
            return GetStringForCodes(new[] {code});
        }

        public static string GetStringForCodes(IEnumerable<long> codes)
        {
            if (!codes.Any())
            {
                return "";
            }

            var s = codes.Aggregate("", (current, code) => current + ("-" + code.ToString("X").ToLower()));

            return s.Substring(1);
        }

        public static bool IsInIntervalEmoji(long code, EmojiPackType pack)
        {
            return IsInStandardUnicodeEmoji(code) || emojiCodesList[pack].Contains(code);
        }

        public static bool IsInStandardUnicodeEmoji(long code)
        {
            return (
                ((code >= 0x1F000) && (code <= 0x1FA9F)) ||
                (code == 0x263A)
            );
        }

        // ReSharper disable once ReturnTypeCanBeEnumerable.Global
        public static EmojiRenderResult[] RenderEmojiAsStringList(EmojiPackType pack, IList<long> activeEmoji)
        {
            var result = new List<EmojiRenderResult>();
            var dictionary = emojiList[pack];
            for (int i = 0; i < activeEmoji.Count; i++)
            {
                bool u = false;
                int max = Math.Min(i + 3, activeEmoji.Count - 1);
                int j = max;
                for (; j >= i; j--)
                {
                    var a = new List<long>();
                    for (int n = i; n <= j; n++)
                    {
                        a.Add(activeEmoji[n]);
                    }

                    var emojiString = GetStringForCodes(a);
                    if (dictionary.Contains(emojiString))
                    {
                        u = true;
                        result.Add(new EmojiRenderResult() {emojiCode = emojiString});
                        break;
                    }
                }

                if (u)
                {
                    // Такой сиквенс, который начинается на activeEmoji[i] есть в словаре 
                    i = j;
                }
                else
                {
                    // Такого сиквенса не существует
                    result.Add(new EmojiRenderResult()
                    {
                        isSuccess = false,
                        rawText = Utf8ToUnicode.UnicodeCodeToString(activeEmoji[i])
                    });
                }
            }

            return result.ToArray();
        }

        public class EmojiRenderResult
        {
            public bool isSuccess = true;
            public string rawText;
            public string emojiCode;
        }
    }

    public enum EmojiPackType
    {
        JoyPixels = 0,
        Twemoji = 1,
        StandardOS = 2,
    }
}