using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NKDiscordChatWidget.General
{
    public static class Utf8ToUnicode
    {
        public static long[] ToUnicodeCode(string utf8)
        {
            return ToUnicodeCode(Encoding.UTF8.GetBytes(utf8));
        }

        public static long[] ToUnicodeCode(byte[] utf8)
        {
            var longs = new List<long>();

            for (int i = 0; i < utf8.Length; i++)
            {
                byte b = utf8[i];
                // 1 byte
                if ((b & 0b1000_0000) == 0b0000_0000)
                {
                    longs.Add(b);
                    continue;
                }

                // error
                if ((b & 0b1100_0000) == 0b1000_0000)
                {
                    // @hint Такого байта тут быть не может
                    longs.Add(0);
                    continue;
                }

                // 2 bytes
                if ((b & 0b1110_0000) == 0b1100_0000)
                {
                    // @todo не хватает байт
                    long L = (b & 0b0001_1111) << 6;
                    long L1 = (utf8[i + 1] & 0b0011_1111);

                    longs.Add(L | L1);
                    i += 1;
                    continue;
                }

                // 3 bytes
                if ((b & 0b1111_0000) == 0b1110_0000)
                {
                    // @todo не хватает байт
                    long L = (b & 0b0000_1111) << 12;
                    long L1 = (utf8[i + 1] & 0b0011_1111) << 6;
                    long L2 = (utf8[i + 2] & 0b0011_1111);

                    longs.Add(L | L1 | L2);
                    i += 2;

                    continue;
                }

                // 4 bytes
                if ((b & 0b1111_1000) == 0b1111_0000)
                {
                    // @todo не хватает байт
                    long L = (b & 0b0000_0111) << 18;
                    long L1 = (utf8[i + 1] & 0b0011_1111) << 12;
                    long L2 = (utf8[i + 2] & 0b0011_1111) << 6;
                    long L3 = (utf8[i + 3] & 0b0011_1111);

                    longs.Add(L | L1 | L2 | L3);
                    i += 3;
                    continue;
                }

                // 5 bytes
                if ((b & 0b1111_1100) == 0b1111_1000)
                {
                    // @todo не хватает байт
                    long L = (b & 0b0000_0011) << 24;
                    long L1 = (utf8[i + 1] & 0b0011_1111) << 18;
                    long L2 = (utf8[i + 2] & 0b0011_1111) << 12;
                    long L3 = (utf8[i + 3] & 0b0011_1111) << 6;
                    long L4 = (utf8[i + 4] & 0b0011_1111);

                    longs.Add(L | L1 | L2 | L3 | L4);
                    i += 4;
                    continue;
                }

                // 6 bytes
                if ((b & 0b1111_1110) == 0b1111_1100)
                {
                    // @todo не хватает байт
                    long L = (b & 0b0000_0001) << 30;
                    long L1 = (utf8[i + 1] & 0b0011_1111) << 24;
                    long L2 = (utf8[i + 2] & 0b0011_1111) << 18;
                    long L3 = (utf8[i + 3] & 0b0011_1111) << 12;
                    long L4 = (utf8[i + 4] & 0b0011_1111) << 6;
                    long L5 = (utf8[i + 5] & 0b0011_1111);

                    longs.Add(L | L1 | L2 | L3 | L4 | L5);
                    i += 5;
                }
            }

            return longs.ToArray();
        }

        public static bool ContainOnlyUnicodeAndSpace(IEnumerable<long> bytes)
        {
            foreach (var b in bytes)
            {
                if ((b == 32) || (b == 0xFE0F) || (b == 0x200D))
                {
                    continue;
                }

                // 0x1F300
                if ((b >= 0x1F000) && (b <= 0x1FA9F))
                {
                    continue;
                }

                if (b == 0x2630A)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Превращаем Unicode-символ (по коду символа) в UTF-16 строку
        /// </summary>
        /// <param name="code"></param>
        /// <url>https://ru.wikipedia.org/wiki/UTF-16</url>
        /// <returns></returns>
        public static byte[] UnicodeCodeToUTF16Bytes(long code)
        {
            if (code <= 0xFFFF)
            {
                return new[]
                {
                    (byte) (code & 0xFF),
                    (byte) ((code >> 8) & 0xFF),
                };
            }

            var a = code - 0x1_0000;
            var a1 = (a & 0x3FF) + 0xDC00;
            var a2 = ((a >> 10) & 0x3FF) + 0xD800;

            return new[]
            {
                (byte) (a2 & 0xFF),
                (byte) ((a2 >> 8) & 0xFF),
                (byte) (a1 & 0xFF),
                (byte) ((a1 >> 8) & 0xFF),
            };
        }

        public static string UnicodeCodeToString(long code)
        {
            byte[] unicodeBytes = UnicodeCodeToUTF16Bytes(code);

            var utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, unicodeBytes);

            return Encoding.UTF8.GetString(utf8Bytes);
        }
    }
}