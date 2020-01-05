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
        private const string guildID = "1";
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
                emojis = new List<Emoji>(),
                channels = new List<EventGuildCreate.EventGuildCreate_Channel>(),
                roles = new List<Role>(),
                members = new List<GuildMember>(),
            };
            // TODO заполнить EventGuildCreate
        }

        private static string GetRandomWord()
        {
            return randomWords[_rnd.Next(0, randomWords.Count - 1)];
        }

        public static IEnumerable<object[]> MainTestData()
        {
            var result = new List<object[]>();
            var emptyMentions = new List<EventMessageCreate.EventMessageCreate_Mention>();

            // bold, em, ~~, spoiler (on/off)
            // quote, no-formatting
            // emoji, mention user, mention role
            {
                var simpleTest = GenerateSimpleTests();
                result.AddRange(simpleTest.Select(singleCase =>
                    new[] {singleCase[0], "<div class='line'>" + singleCase[1] + "</div>", null, null, null}));
            }
            {
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
            }
            {
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
                                emptyMentions,
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
    }
}