using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
                        position = 0,
                    },
                    new Role()
                    {
                        color = 15844367,
                        id = "568376310133424152",
                        name = "admins",
                        permissions = 104324705,
                        position = 3,
                    },
                    new Role()
                    {
                        color = 10181046,
                        id = "633965723764523028",
                        name = "Фиолетовый",
                        permissions = 104324673,
                        position = 2,
                    },
                },
                members = new List<GuildMember>()
                {
                    new GuildMember()
                    {
                        nick = "北風",
                        roles = new List<string> {"568376310133424152",},
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
                        roles = new List<string> {"568217115031502868", "633965723764523028",},
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
            // TODO заполнить EventGuildCreate
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
            var emptyMentions = new List<EventMessageCreate.EventMessageCreate_Mention>();

            // bold, em, ~~, spoiler (on/off)
            // quote, no-formatting
            // emoji, mention user, mention role
            /* TODO: Вернуть
            {
                var simpleTest = GenerateSimpleTests();
                result.AddRange(simpleTest.Select(singleCase =>
                    new[] {singleCase[0], "<div class='line'>" + singleCase[1] + "</div>", null, null, null}));
            }
            */
            result.AddRange(GetThreeAsteriskTests());
            // result.AddRange(GetSpaceCharacterTests()); TODO: Вернуть
            {
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

                var mentions = new List<EventMessageCreate.EventMessageCreate_Mention>();
                foreach (var member in NKDiscordChatWidget.DiscordBot.Bot.guilds[guildID].members)
                {
                    mentions.Add(new EventMessageCreate.EventMessageCreate_Mention()
                    {
                        member = member,
                        username = member.nick,
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
                            string postfix = "";
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
            }

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
            };
            var markdownTemplate = new List<string>()
            {
                "**{0}**",
                "*{0}*",
                "~~{0}~~",
                "||{0}||",
            };

            for (int i = 0; i < htmlTemplate.Count; i++)
            {
                for (int j = 0; j < htmlTemplate.Count; j++)
                {
                    if ((i == j) || ((j == 1) && (i == 0)))
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
            var result = new List<object[]>();
            var word1 = GetRandomWord();
            var word2 = GetRandomWord();
            var word3 = GetRandomWord();

            result.Add(new object[]
            {
                string.Format("**{0}** {1} **{2}**", word1, word2, word3),
                string.Format("<div class='line'><strong>{0}</strong> {1} <strong>{2}</strong></div>",
                    word1, word2, word3),
                null,
                null,
                null
            });
            result.Add(new object[]
            {
                string.Format("***{0}*** {1} ***{2}***", word1, word2, word3),
                string.Format(
                    "<div class='line'><strong><em>{0}</em></strong> {1} <strong><em>{2}</em></strong></div>",
                    word1, word2, word3),
                null,
                null,
                null
            });

            // Чекаем работает ли порядок появления форматирования

            // ** `nyan**` pasu **
            result.Add(new object[]
            {
                string.Format("** `{0}**` {1} **", word1, word2),
                string.Format(
                    "<div class='line'><strong> `{0}</strong>` {1} **</div>",
                    word1, word2),
                null,
                null,
                null
            });
            // `** nyan` ** pasu **
            result.Add(new object[]
            {
                string.Format("`** {0}` ** {1} **", word1, word2),
                string.Format(
                    "<div class='line'><span class='without-mark'>** {0}</span> <strong> {1} </strong></div>",
                    word1, word2),
                null,
                null,
                null
            });

            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var pair in new[]
                {
                    new object[] {"im*italic*within", "im<em>italic</em>within"},

                    new object[] {"! **bold** !", "! <strong>bold</strong> !"},
                    new object[] {"! ** bold ** !", "! <strong> bold </strong> !"},

                    // Discord идёт на встречу пользователям, поэтому парсер не такой как по спеке
                    new object[] {"! *italic* !", "! <em>italic</em> !"},
                    new object[] {"! * not italic * !", "! * not italic * !"},
                    new object[] {"!* not italic *!", "!* not italic *!"},
                    new object[] {"* not italic*", "* not italic*"},
                    new object[] {"*not italic *", "*not italic *"},
                    new object[] {"! _italic_ !", "! <em>italic</em> !"},
                    new object[] {"! _ italic _ !", "! <em> italic </em> !"},
                    
                    // Underscore tests
                    new object[] {"__underscore__", "<u>underscore</u>"},
                    new object[] {"__ underscore __", "<u> underscore </u>"},
                    new object[] {"___underscore italic___", "<em><u>underscore italic</u></em>"},
                    new object[] {"___ underscore italic ___", "<em><u> underscore italic </u></em>"},
                })
                {
                    result.Add(new[]
                    {
                        pair[0],
                        string.Format("<div class='line'>{0}</div>", pair[1]),
                        null,
                        null,
                        null
                    });
                }
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
            List<CheckRule> rules
        )
        {
            if (chatOption == null)
            {
                chatOption = new ChatDrawOption();
            }

            if (mentions == null)
            {
                mentions = new List<EventMessageCreate.EventMessageCreate_Mention>();
            }

            if (rules == null)
            {
                rules = new List<CheckRule>();
            }

            var renderedText = MessageMarkdownParser.RenderMarkdownAsHTML(
                rawMarkdown,
                chatOption,
                mentions,
                guildID
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
                var r = new Regex("<em><strong>(.*?)</strong></em>",
                    RegexOptions.Compiled);
                expectedHtml = r.Replace(expectedHtml,
                    (m) => m.Groups[1].Value.IndexOf("<", StringComparison.Ordinal) == -1
                        ? string.Format("<strong><em>{0}</em></strong>", m.Groups[1].Value)
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
            // var images = document.QuerySelectorAll<IHtmlImageElement>("img");

            var usedPaths = new HashSet<string>();
            foreach (var rule in rules)
            {
                usedPaths.Add(rule.path);

                // TODO
            }
        }

        public class CheckRule
        {
            public string path;
            public string expectedValue = "";
            public string expectedNodeType = null;
            public IEnumerable<string> expectedClassList = null;
        }

        #endregion
    }
}