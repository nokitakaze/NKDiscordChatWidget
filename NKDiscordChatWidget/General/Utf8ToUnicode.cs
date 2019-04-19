using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NKDiscordChatWidget.General
{
    public static class Utf8ToUnicode
    {
        public static long[] ToUnicode(string utf8)
        {
            return ToUnicode(Encoding.UTF8.GetBytes(utf8));
        }

        public static long[] ToUnicode(byte[] utf8)
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
                    long L = (b & 0b0000_0011) << 30;
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
            return (from b in bytes
                    where (b != 32) && (b != 0xFE0F) && (b != 0x200D)
                    where (b < 0x1F300) || (b > 0x1F5FF)
                    where b != 0x2630A
                    select b)
                .All(b => (b >= 0x1F600) && (b <= 0x1F64F));
        }
    }
}