using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using AngleSharp;
using AngleSharp.Html.Dom;
using NKDiscordChatWidget;
using NKDiscordChatWidget.DiscordBot.Classes;
using NKDiscordChatWidget.General;
using Xunit;

namespace MarkdownTest
{
    public class MessageMarkdownParserTest
    {
        #region Init

        private const string guildID = "568216611366895631";
        private static readonly Random _rnd = new Random();
        private static readonly List<string> randomWords;

        static MessageMarkdownParserTest()
        {
            UnicodeEmojiEngine.LoadAllEmojiPacks(Options.WWWRoot);
            randomWords = new List<string>()
            {
                "word1",
                "word2",
                "word3",
                "word4",
                "word5",
                "word6",
                "word7",
            };

            NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID] = new EventGuildCreate()
            {
                id = guildID,
                icon = "82000cc0465ffdf3d03bb09a6a79bc08",
                emojis = new List<Emoji>()
                {
                    new Emoji()
                    {
                        id = "568685036979748865",
                        name = "st2",
                        require_colons = true,
                    },
                    new Emoji()
                    {
                        id = "568685037868810269",
                        name = "st1",
                        require_colons = true,
                    },
                    new Emoji()
                    {
                        id = "663446227550994452",
                        name = "box1",
                        animated = true,
                        require_colons = true,
                    },
                    new Emoji()
                    {
                        id = "663446228616478720",
                        name = "box2",
                        animated = true,
                        require_colons = true,
                    },
                },
                channels = new List<EventGuildCreate.EventGuildCreate_Channel>(),
                roles = new List<Role>()
                {
                    new Role()
                    {
                        color = 0,
                        id = "568216611366895631",
                        name = "@everyone",
                        permissions = 104324673,
                        position = 0,
                    },
                    new Role()
                    {
                        color = 1752220,
                        id = "568217115031502868",
                        name = "NKDiscordChatWidget",
                        permissions = 1024,
                        position = 1,
                    },
                    new Role()
                    {
                        color = 15844367,
                        id = "568376310133424152",
                        name = "admins",
                        permissions = 104324705,
                        position = 4,
                    },
                    new Role()
                    {
                        color = 10181046,
                        id = "633965723764523028",
                        name = "Фиолетовый",
                        permissions = 104324673,
                        position = 2,
                    },
                    new Role()
                    {
                        color = 15158332,
                        id = "633954441485221898",
                        name = "Orange men",
                        permissions = 104324673,
                        position = 3,
                    },
                },
                members = new List<GuildMember>()
                {
                    new GuildMember()
                    {
                        nick = "北風",
                        roles = new List<string> {"568376310133424152", "633954441485221898"},
                        user = new User()
                        {
                            avatar = "8a33053d4a3ef74577fdd4b21431ed2e",
                            discriminator = "2064",
                            id = "428567095563780107",
                            username = "nokitakaze",
                        },
                    },
                    new GuildMember()
                    {
                        nick = null,
                        roles = new List<string> {"568217115031502868", "633965723764523028", "633954441485221898"},
                        user = new User()
                        {
                            avatar = null,
                            discriminator = "0355",
                            id = "568138249986375682",
                            username = "NKDiscordChatWidget",
                        },
                    },
                },
            };
        }

        private static string GetRandomWord()
        {
            return randomWords[_rnd.Next(0, randomWords.Count - 1)];
        }

        #endregion

        #region GenerateTestData

        public static IEnumerable<object[]> MainTestData()
        {
            var result = new List<object[]>();

            // bold, em, ~~, spoiler (on/off)
            // quote, no-formatting
            // emoji, mention user, mention role
            {
                var simpleTest = GenerateSimpleTests();
                result.AddRange(simpleTest.Select(singleCase =>
                    new[] {singleCase[0], "<div class='line'>" + singleCase[1] + "</div>", null, null, null}));
            }
            result.AddRange(GetThreeAsteriskTests());
            result.AddRange(GetSpaceCharacterTests());
            result.AddRange(GetInputs());
            result.AddRange(GetQuoteCheck());
            result.AddRange(GetEdgeCases());
            result.AddRange(GetLinksCases());

            return result;
        }

        private static IEnumerable<object[]> GenerateSimpleTests()
        {
            var result = new List<object[]>();
            for (int i = 0; i < (1 << 4); i++)
            {
                var possibilities = new List<int>();
                for (int n = 1; n <= (1 << 3); n *= 2)
                {
                    if ((i & n) == n)
                    {
                        possibilities.Add(n);
                    }
                }

                var fullVariations = MutateListFactorial(possibilities);
                foreach (var variation in fullVariations)
                {
                    {
                        // Ищем идущие подряд * & **. Пропускаем
                        bool u = false;
                        for (int j = 1; j < variation.Count; j++)
                        {
                            if ((variation[j] == 2) && (variation[j - 1] == 1))
                            {
                                u = true;
                                break;
                            }
                        }

                        if (u)
                        {
                            continue;
                        }
                    }

                    var word = GetRandomWord();
                    var currentMarkdown = word;
                    var currentHTML = word;
                    foreach (var num in variation)
                    {
                        switch (num)
                        {
                            case 1:
                                currentMarkdown = "**" + currentMarkdown + "**";
                                currentHTML = "<strong>" + currentHTML + "</strong>";
                                break;
                            case 2:
                                currentMarkdown = "*" + currentMarkdown + "*";
                                currentHTML = "<em>" + currentHTML + "</em>";
                                break;
                            case 4:
                                currentMarkdown = "~~" + currentMarkdown + "~~";
                                currentHTML = "<del>" + currentHTML + "</del>";
                                break;
                            case 8:
                                currentMarkdown = "||" + currentMarkdown + "||";
                                currentHTML = string.Format(
                                    "<span class='spoiler '><span class='spoiler-content'>{0}</span></span>",
                                    currentHTML);
                                break;
                            default:
                                throw new Exception();
                        }
                    }

                    result.Add(new object[] {currentMarkdown, currentHTML});
                }
            }

            return result.ToArray();
        }

        private static List<List<int>> MutateListFactorial(IReadOnlyList<int> rawList)
        {
            if (rawList.Count == 1)
            {
                return new List<List<int>>() {new List<int>() {rawList.First()}};
            }

            var result = new List<List<int>>();
            for (int i = 0; i < rawList.Count; i++)
            {
                var prefix = rawList[i];
                var leftList = rawList.ToList();
                leftList.RemoveAt(i);

                var otherLists = MutateListFactorial(leftList);

                foreach (var otherList in otherLists)
                {
                    var resultSubList = new List<int>() {prefix};
                    resultSubList.AddRange(otherList);
                    result.Add(resultSubList);
                }
            }

            return result;
        }

        /// <summary>
        /// Тестирование влияния пробелов на форматирование
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<object[]> GetSpaceCharacterTests()
        {
            var result = new List<object[]>();

            var htmlTemplate = new List<string>()
            {
                "<strong>{0}</strong>",
                "<em>{0}</em>",
                "<del>{0}</del>",
                "<span class='spoiler '><span class='spoiler-content'>{0}</span></span>",
                "<u>{0}</u>",
                "<em>{0}</em>",
            };
            var markdownTemplate = new List<string>()
            {
                "**{0}**",
                "*{0}*",
                "~~{0}~~",
                "||{0}||",
                "__{0}__",
                "_{0}_",
            };

            for (int i = 0; i < htmlTemplate.Count; i++)
            {
                if (i == 1)
                {
                    // Пробелы внутри это сразу на хер

                    continue;
                }

                for (int j = 0; j < htmlTemplate.Count; j++)
                {
                    if (
                        (i == j) ||
                        ((i == 0) && (j == 1)) ||
                        ((i == 4) && (j == 5)) ||
                        ((i == 1) && (j == 5)) ||
                        ((i == 5) && (j == 1)) ||
                        ((i == 0) && (j == 5)) ||
                        ((i == 5) && (j == 0))
                    )
                    {
                        continue;
                    }

                    for (int n = 1; n < 4; n++)
                    {
                        var prefixSpace = (n & 1) == 1 ? " " : "";
                        var postfixSpace = (n & 2) == 2 ? " " : "";

                        var word = GetRandomWord();
                        var fullText = string.Format("{0}{1}{2}", prefixSpace, word, postfixSpace);

                        var inputMarkdown = string.Format(markdownTemplate[i], fullText);
                        inputMarkdown = string.Format(markdownTemplate[j], inputMarkdown);

                        var outputHTML = string.Format(htmlTemplate[i], fullText);
                        outputHTML = string.Format(htmlTemplate[j], outputHTML);

                        result.Add(new object[]
                        {
                            inputMarkdown,
                            string.Format("<div class='line'>{0}</div>", outputHTML),
                            null,
                            null,
                            null
                        });
                    }
                }
            }

            return result;
        }

        private static IEnumerable<object[]> GetThreeAsteriskTests()
        {
            var result = GetThreeAsteriskTestsRaw()
                .Select(pair => new[]
                    {pair[0], string.Format("<div class='line'>{0}</div>", pair[1]), null, null, null})
                .Cast<object[]>()
                .ToList();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var local in new[]
            {
                new[]
                {
                    "~~ > ~~",
                    "<div class='line'><del> &gt; </del></div>"
                },
                new[]
                {
                    "|| > ||",
                    "<div class='line'><span class='spoiler '><span class='spoiler-content'> &gt; </span></span></div>"
                },
            })
            {
                result.Add(new object[] {local[0], local[1], null, null, null});
            }

            return result;
        }

        private static IEnumerable<string[]> GetThreeAsteriskTestsRaw()
        {
            var word1 = GetRandomWord();
            var word2 = GetRandomWord();
            var word3 = GetRandomWord();

            // ReSharper disable once LoopCanBeConvertedToQuery
            return new[]
            {
                new[]
                {
                    string.Format("**{0}** {1} **{2}**", word1, word2, word3), string.Format(
                        "<strong>{0}</strong> {1} <strong>{2}</strong>",
                        word1, word2, word3)
                },
                new[]
                {
                    string.Format("***{0}*** {1} ***{2}***", word1, word2, word3),
                    string.Format(
                        "<strong><em>{0}</em></strong> {1} <strong><em>{2}</em></strong>",
                        word1, word2, word3)
                },

                // Чекаем работает ли порядок появления форматирования
                // ** `nyan**` pasu **
                new[]
                {
                    string.Format("** `{0}**` {1} **", word1, word2),
                    string.Format("<strong> `{0}</strong>` {1} **", word1, word2)
                },
                // `** nyan` ** pasu **
                new[]
                {
                    string.Format("`** {0}` ** {1} **", word1, word2),
                    string.Format(
                        "<span class='without-mark'>** {0}</span> <strong> {1} </strong>",
                        word1, word2)
                },

                //
                new[] {"im*italic*within", "im<em>italic</em>within"},

                new[] {"! **bold** !", "! <strong>bold</strong> !"},
                new[] {"! ** bold ** !", "! <strong> bold </strong> !"},

                // Discord идёт на встречу пользователям, поэтому парсер не такой как по спеке
                new[] {"! *italic* !", "! <em>italic</em> !"},
                new[] {"! * not italic * !", "! * not italic * !"},
                new[] {"!* not italic *!", "!* not italic *!"},
                new[] {"* not italic*", "* not italic*"},
                new[] {"*not italic *", "*not italic *"},
                new[] {"! _italic_ !", "! <em>italic</em> !"},
                new[] {"! _ italic _ !", "! <em> italic </em> !"},

                // Underscore tests
                new[] {"__underscore__", "<u>underscore</u>"},
                new[] {"__ underscore __", "<u> underscore </u>"},
                new[] {"___underscore italic___", "<em><u>underscore italic</u></em>"},
                new[] {"___ underscore italic ___", "<em><u> underscore italic </u></em>"},
            };
        }

        private static IEnumerable<object[]> GetInputs()
        {
            var result = new List<object[]>();
            var inputs = new List<string[]>()
            {
                new[]
                {
                    "<:st1:568685037868810269> <a:box1:663446227550994452> 😏",
                    "<span class='emoji '><img src='https://cdn.discordapp.com/emojis/568685037868810269.png' alt=':st1:'></span> <span class='emoji '><img src='https://cdn.discordapp.com/emojis/663446227550994452.gif' alt=':box1:'></span> <span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f60f.svg' alt=':1f60f:'></span>",
                },
                new[]
                {
                    "😏 😼",
                    "<span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f60f.svg' alt=':1f60f:'></span> <span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f63c.svg' alt=':1f63c:'></span>",
                },
                new[]
                {
                    "<@!428567095563780107>",
                    "<span class='user mention' style='color: #F1C40F;'>@北風</span>",
                },
                new[]
                {
                    "<@&633965723764523028>",
                    "<span class='role mention' style='color: #9B59B6;'>@Фиолетовый</span>",
                },
                new[]
                {
                    "<@!400000000000000000>",
                    "<Пользователь Unknown #400000000000000000>",
                },
                new[]
                {
                    "<@&600000000000000000>",
                    "<Role Unknown #600000000000000000>",
                },
            };

            var mentions = new List<EventMessageCreate.EventMessageCreate_Mention>();
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var member in NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].members)
            {
                mentions.Add(new EventMessageCreate.EventMessageCreate_Mention()
                {
                    member = member,
                    username = member.user.username,
                    id = member.user.id,
                });
            }

            var chatOption = new ChatDrawOption()
            {
                message_mentions_style = 1,
            };

            foreach (var input in inputs)
            {
                foreach (var u1 in new[] {false, true})
                {
                    var prefix = u1 ? GetRandomWord() + " " : "";

                    for (var i1 = 0; i1 < 3; i1++)
                    {
                        string postfix;
                        switch (i1)
                        {
                            case 0:
                                postfix = "";
                                break;
                            case 1:
                                postfix = " ";
                                break;
                            case 2:
                                postfix = " " + GetRandomWord();
                                break;
                            default:
                                throw new Exception();
                        }

                        var markdown = prefix + input[0] + postfix;
                        var html = prefix + input[1] + postfix;

                        result.Add(new object[]
                        {
                            markdown,
                            "<div class='line'>" + html + "</div>",
                            chatOption,
                            mentions,
                            null
                        });
                    }
                }
            }

            foreach (var t1 in inputs)
            {
                result.AddRange(inputs.Select(t2 => new object[]
                {
                    t1[0] + " " + t2[0],
                    "<div class='line'>" + t1[1] + " " + t2[1] + "</div>",
                    chatOption,
                    mentions,
                    null
                }));
            }

            return result;
        }

        private static IEnumerable<object[]> GetQuoteCheck()
        {
            var result = new List<object[]>();
            var inputs = new List<string[]>()
            {
                new[]
                {
                    "<:st1:568685037868810269> <a:box1:663446227550994452> 😏",
                    "<span class='emoji '><img src='https://cdn.discordapp.com/emojis/568685037868810269.png' alt=':st1:'></span> <span class='emoji '><img src='https://cdn.discordapp.com/emojis/663446227550994452.gif' alt=':box1:'></span> <span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f60f.svg' alt=':1f60f:'></span>",
                },
                new[]
                {
                    "😏 😼",
                    "<span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f60f.svg' alt=':1f60f:'></span> <span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f63c.svg' alt=':1f63c:'></span>",
                },
                new[]
                {
                    "<@!428567095563780107>",
                    "<span class='user mention' style='color: #F1C40F;'>@北風</span>",
                },
                new[]
                {
                    "<@&633965723764523028>",
                    "<span class='role mention' style='color: #9B59B6;'>@Фиолетовый</span>",
                },
            };
            inputs.AddRange(GetThreeAsteriskTestsRaw());

            var mentions = new List<EventMessageCreate.EventMessageCreate_Mention>();
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var member in NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].members)
            {
                mentions.Add(new EventMessageCreate.EventMessageCreate_Mention()
                {
                    member = member,
                    username = member.user.username,
                    id = member.user.id,
                });
            }

            var chatOption = new ChatDrawOption() {message_mentions_style = 1};

            for (int i = 0; i < Math.Pow(4, 4); i++)
            {
                var ars = new List<int>();
                {
                    int i1 = i;
                    for (int j = 0; j < 4; j++)
                    {
                        int n = i1 % 4;
                        ars.Add(n);
                        i1 >>= 2;
                    }
                }

                if (ars[0] == 0)
                {
                    continue;
                }

                var inputMarkdown = "";
                var outputHTML = "";

                bool isInQuote = false;
                var outputHTMLQuote = "";
                foreach (var ar in ars)
                {
                    string localInputMarkdown, localOutputHTML;
                    if (ar % 2 == 0)
                    {
                        localInputMarkdown = "";
                        localOutputHTML = "";
                    }
                    else
                    {
                        var input = inputs[_rnd.Next(0, inputs.Count - 1)];
                        localInputMarkdown = input[0];
                        localOutputHTML = input[1];
                    }

                    if ((ar == 2) || (ar == 3))
                    {
                        inputMarkdown += "\n> " + localInputMarkdown;
                        outputHTMLQuote += "<div class='line'>" + localOutputHTML + "</div>";
                        isInQuote = true;
                    }
                    else
                    {
                        inputMarkdown += "\n" + localInputMarkdown;
                        if (isInQuote)
                        {
                            outputHTML += string.Format(
                                "<div class='quote-block'><div class='quote-border'></div><div class='quote-content'>{0}</div></div>",
                                outputHTMLQuote
                            );
                            isInQuote = false;
                            outputHTMLQuote = "";
                        }

                        outputHTML += "<div class='line'>" + localOutputHTML + "</div>";
                    }
                }

                if (isInQuote)
                {
                    outputHTML += string.Format(
                        "<div class='quote-block'><div class='quote-border'></div><div class='quote-content'>{0}</div></div>",
                        outputHTMLQuote
                    );
                }

                result.Add(new object[]
                {
                    inputMarkdown.Substring(1),
                    outputHTML,
                    chatOption,
                    mentions,
                    null
                });
            }

            return result;
        }

        private static IEnumerable<object[]> GetEdgeCases()
        {
            var result = new List<object[]>();

            // Тестируем Эмодзи: chatOption.emoji_relative & chatOption.emoji_stranger
            const string ourServerEmoji = "<a:box1:663446227550994452> <:st1:568685037868810269>";
            const string otherServerEmoji = "<a:unk1:600000000000000000> <:unk2:500000000000000000>";
            const string ourServerEmojiHTML =
                "<span class='emoji {0}'><img src='https://cdn.discordapp.com/emojis/663446227550994452.gif' alt=':box1:'></span> " +
                "<span class='emoji {0}'><img src='https://cdn.discordapp.com/emojis/568685037868810269.png' alt=':st1:'></span>";
            const string otherServerEmojiHTML =
                "<span class='emoji {0}'><img src='https://cdn.discordapp.com/emojis/600000000000000000.gif' alt=':unk1:'></span> " +
                "<span class='emoji {0}'><img src='https://cdn.discordapp.com/emojis/500000000000000000.png' alt=':unk2:'></span>";
            const string ourServerEmojiPlain = ":box1: :st1:";
            const string otherServerEmojiPlain = ":unk1: :unk2:";

            var mentions = new List<EventMessageCreate.EventMessageCreate_Mention>();
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var member in NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].members)
            {
                mentions.Add(new EventMessageCreate.EventMessageCreate_Mention()
                {
                    member = member,
                    username = member.user.username,
                    id = member.user.id,
                });
            }

            for (int i1 = 1; i1 < 4; i1++)
            {
                var markdown = ((i1 % 2 == 1) ? ourServerEmoji : "") + ((i1 >> 1 == 1) ? otherServerEmoji : "");

                for (int i2 = 0; i2 < (1 << 4); i2++)
                {
                    var chatOption = new ChatDrawOption
                    {
                        emoji_relative = i2 % 4,
                        emoji_stranger = (i2 >> 2) % 4,
                    };
                    if ((chatOption.emoji_relative == 3) || (chatOption.emoji_stranger == 3))
                    {
                        continue;
                    }

                    var expectedHTML = "";
                    if (i1 % 2 == 1)
                    {
                        switch (chatOption.emoji_relative)
                        {
                            case 0:
                                expectedHTML += string.Format(ourServerEmojiHTML, "");
                                break;
                            case 1:
                                expectedHTML += string.Format(ourServerEmojiHTML, "blur");
                                break;
                            case 2:
                                expectedHTML += ourServerEmojiPlain;
                                break;
                        }
                    }

                    if (i1 >> 1 == 1)
                    {
                        switch (chatOption.emoji_stranger)
                        {
                            case 0:
                                expectedHTML += string.Format(otherServerEmojiHTML, "");
                                break;
                            case 1:
                                expectedHTML += string.Format(otherServerEmojiHTML, "blur");
                                break;
                            case 2:
                                expectedHTML += otherServerEmojiPlain;
                                break;
                        }
                    }

                    result.Add(new object[]
                    {
                        markdown,
                        "<div class='line'>" + expectedHTML + "</div>",
                        chatOption,
                        null,
                        null
                    });
                }
            }

            {
                var inputs = new dynamic[]
                {
                    new
                    {
                        codes = new[] {0x1F346},
                        expected =
                            "<span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f346.svg' alt=':1f346:'></span>"
                    },
                    new
                    {
                        codes = new[] {0x1F47A},
                        expected =
                            "<span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f47a.svg' alt=':1f47a:'></span>"
                    },
                    new
                    {
                        codes = new[] {0x1F346, 0x1F47A},
                        expected =
                            "<span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f346.svg' alt=':1f346:'></span>" +
                            "<span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f47a.svg' alt=':1f47a:'></span>"
                    },
                    new
                    {
                        codes = new[] {0x1F1F7, 0x1F1FA},
                        expected =
                            "<span class='emoji unicode-emoji '><img src='/images/emoji/twemoji/1f1f7-1f1fa.svg' alt=':1f1f7-1f1fa:'></span>"
                    },
                    new
                    {
                        codes = new[] {0x31, 0xfe0f, 0x20e3},
                        expected = (string) null,
                    },
                };

                foreach (var input in inputs)
                {
                    int[] codes = input.codes;
                    string expectedTwemoji = input.expected;

                    foreach (var emojiPack in new[] {EmojiPackType.Twemoji, EmojiPackType.StandardOS})
                    {
                        var chatOption = new ChatDrawOption {unicode_emoji_displaying = emojiPack};
                        var emoji = codes.Aggregate("",
                            (current, code) => current + Utf8ToUnicode.UnicodeCodeToString(code));

                        var expectedHTML = "";
                        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                        switch (emojiPack)
                        {
                            case EmojiPackType.Twemoji:
                                expectedHTML = expectedTwemoji ?? emoji;
                                break;
                            case EmojiPackType.StandardOS:
                                expectedHTML = emoji;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        result.Add(new object[]
                        {
                            emoji,
                            "<div class='line'>" + expectedHTML + "</div>",
                            chatOption,
                            null,
                            null
                        });
                    }
                }
            }

            // text_spoiler
            {
                var word = GetRandomWord();
                string markdown = string.Format("||{0}||", word);
                for (int i = 0; i < 2; i++)
                {
                    var chatOption = new ChatDrawOption {text_spoiler = i};
                    var html = string.Format(
                        "<div class='line'><span class='spoiler {0}'><span class='spoiler-content'>{1}</span></span></div>",
                        (i == 1) ? "spoiler-show" : "",
                        word
                    );

                    result.Add(new object[]
                    {
                        markdown,
                        html,
                        chatOption,
                        null,
                        null
                    });
                }
            }

            // message_mentions_style
            {
                var inputs = new[]
                {
                    new[]
                    {
                        "428567095563780107",
                        "北風",
                        "F1C40F",
                    },
                    new[]
                    {
                        "568138249986375682",
                        "NKDiscordChatWidget",
                        "E74C3C",
                    },
                };

                foreach (var input in inputs)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        string markdown = string.Format("<@!{0}>", input[0]);
                        var chatOption = new ChatDrawOption {message_mentions_style = i};
                        var html = string.Format(
                            "<div class='line'><span class='user mention'{0}>@{1}</span></div>",
                            (i == 1) ? string.Format(" style='color: #{0};'", input[2]) : "",
                            input[1]
                        );

                        result.Add(new object[]
                        {
                            markdown,
                            html,
                            chatOption,
                            mentions,
                            null
                        });
                        result.Add(new object[]
                        {
                            markdown,
                            string.Format("<div class='line'><Пользователь Unknown #{0}></div>", input[0]),
                            chatOption,
                            null,
                            null
                        });
                    }
                }
            }

            return result;
        }

        private static IEnumerable<object[]> GetLinksCases()
        {
            var result = new List<object[]>();

            var chatOptionNotShort = new ChatDrawOption
            {
                short_anchor = 0,
            };
            var chatOptionShort = new ChatDrawOption
            {
                short_anchor = 1,
            };

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var input in new[]
            {
                new[] {"http://example.com/", "http://example.com/", "http://example.com/"},
                new[]
                {
                    "https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    "https://ru.wikipedia.org/wiki/Википедия:Введение",
                    "https://ru.wikipedia.org/wiki/Википед...",
                },
            })
            {
                var markdown = input[0];
                var html = string.Format("<a href='{0}' target='_blank'>{1}</a>",
                    HttpUtility.HtmlEncode(input[0]),
                    HttpUtility.HtmlEncode(input[1])
                );
                var htmlShortAnchor = string.Format("<a href='{0}' target='_blank'>{1}</a>",
                    HttpUtility.HtmlEncode(input[0]),
                    HttpUtility.HtmlEncode(input[2])
                );

                result.Add(new object[]
                {
                    markdown,
                    string.Format("<div class='line'>{0}</div>", html),
                    chatOptionNotShort,
                    null,
                    null
                });
                result.Add(new object[]
                {
                    markdown,
                    string.Format("<div class='line'>{0}</div>", htmlShortAnchor),
                    chatOptionShort,
                    null,
                    null
                });
            }

            //
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var input in new[]
            {
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                    new List<string>() {"https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg"},
                    "",
                    chatOptionShort
                },
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                    new List<string>() {"https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg"},
                    "",
                    chatOptionNotShort
                },
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                    new List<string>(),
                    "<a href='https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg' target='_blank'>https://cs7.pikabu.ru/post_img/big/20...</a>",
                    chatOptionShort
                },
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                    new List<string>(),
                    "<a href='https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg' target='_blank'>https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg</a>",
                    chatOptionNotShort
                },

                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>() {"https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg"},
                    " <a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википед...</a>",
                    chatOptionShort
                },
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>() {"https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg"},
                    " <a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википедия:Введение</a>",
                    chatOptionNotShort
                },
                new dynamic[]
                {
                    "https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>() {"https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg"},
                    "<a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википед...</a>",
                    chatOptionShort
                },
                new dynamic[]
                {
                    "https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>() {"https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg"},
                    "<a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википедия:Введение</a>",
                    chatOptionNotShort
                },
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>()
                    {
                        "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                        "https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif"
                    },
                    " <a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википед...</a>",
                    chatOptionShort
                },
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>()
                    {
                        "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                        "https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif"
                    },
                    " <a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википедия:Введение</a>",
                    chatOptionNotShort
                },
                new dynamic[]
                {
                    "https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>()
                    {
                        "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                        "https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif"
                    },
                    "<a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википед...</a>",
                    chatOptionShort
                },
                new dynamic[]
                {
                    "https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5",
                    new List<string>()
                    {
                        "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                        "https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif"
                    },
                    "<a href='https://ru.wikipedia.org/wiki/%D0%92%D0%B8%D0%BA%D0%B8%D0%BF%D0%B5%D0%B4%D0%B8%D1%8F:%D0%92%D0%B2%D0%B5%D0%B4%D0%B5%D0%BD%D0%B8%D0%B5' target='_blank'>https://ru.wikipedia.org/wiki/Википедия:Введение</a>",
                    chatOptionNotShort
                },
                //
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif",
                    new List<string>()
                    {
                        "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                        "https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif"
                    },
                    " ",
                    chatOptionShort
                },
                new dynamic[]
                {
                    "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif",
                    new List<string>()
                    {
                        "https://cs7.pikabu.ru/post_img/big/2018/10/31/8/1540989921187952325.jpg",
                        "https://media.discordapp.net/attachments/421392740970921996/599311410807439390/terminator-thumbs-up.gif"
                    },
                    " ",
                    chatOptionNotShort
                },
            })
            {
                string markdown = input[0];
                List<string> list = input[1];
                string expectedHTML = input[2];
                ChatDrawOption chatOption = input[3];

                result.Add(new object[]
                {
                    markdown,
                    string.Format("<div class='line'>{0}</div>", expectedHTML),
                    chatOption,
                    null,
                    list
                });
            }

            return result;
        }

        #endregion

        #region Test

        [Theory]
        [MemberData(nameof(MainTestData))]
        public void MainTest(
            string rawMarkdown,
            string expectedHtml,
            ChatDrawOption chatOption,
            List<EventMessageCreate.EventMessageCreate_Mention> mentions,
            IEnumerable<string> usedEmbedUrl
        )
        {
            if (chatOption == null)
            {
                chatOption = new ChatDrawOption();
            }

            var usedEmbedUrlHash = (usedEmbedUrl != null) ? usedEmbedUrl.ToHashSet() : new HashSet<string>();

            var renderedText = MessageMarkdownParser.RenderMarkdownAsHTML(
                rawMarkdown,
                chatOption,
                mentions,
                guildID,
                usedEmbedUrlHash
            );

            IHtmlDocument document;
            {
                var configuration = Configuration.Default;
                var context = BrowsingContext.New(configuration);
                document = (IHtmlDocument) context.OpenAsync(res => res
                    .Content(renderedText)
                    .Address("http://localhost:5050/chat.cgi")).Result;
            }

            var realExpectedHtml = "";
            {
                var r1 = new Regex("<em><strong>(.*?)</strong></em>",
                    RegexOptions.Compiled);
                expectedHtml = r1.Replace(expectedHtml,
                    (m) => m.Groups[1].Value.IndexOf("<", StringComparison.Ordinal) == -1
                        ? string.Format("<strong><em>{0}</em></strong>", m.Groups[1].Value)
                        : m.Groups[0].Value);
                var r2 = new Regex("<u><em>(.*?)</em></u>",
                    RegexOptions.Compiled);
                expectedHtml = r2.Replace(expectedHtml,
                    (m) => m.Groups[1].Value.IndexOf("<", StringComparison.Ordinal) == -1
                        ? string.Format("<em><u>{0}</u></em>", m.Groups[1].Value)
                        : m.Groups[0].Value);

                var configuration = Configuration.Default;
                var context = BrowsingContext.New(configuration);
                var document1 = (IHtmlDocument) context.OpenAsync(res => res
                    .Content(expectedHtml)
                    .Address("http://localhost:5050/chat.cgi")).Result;
                realExpectedHtml = document1.Body.InnerHtml;
            }
            var resultHTML = document.Body.InnerHtml;
            Assert.Equal(realExpectedHtml, resultHTML);
        }

        #endregion
    }
}