using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPExtractor
{
    public static class PrecalculatedValues
    {
        public static short[] SecondPartBlockLengths = new short[0x100];
        public static int[][] SecondPartBlockBits = new int[0x100][];

        public static void CalculateSecondPartBlockLength()
        {
            var sw = Stopwatch.StartNew();

            for (int i = 0; i <= 0xFF; i++)
            {
                SecondPartBlockLengths[i] = (short)Helper.IterateBits((byte)i).Select(it => it == 1 ? 2 : 1).Sum();
            }

            sw.Stop();

            Debug.WriteLine("CalculateSecondPartBlockLength finished in {0}", sw.Elapsed.ToString());
        }

        public static void CalculateSecondPartBlockBits()
        {
            var sw = Stopwatch.StartNew();

            for (int i = 0; i <= 0xFF; i++)
            {
                SecondPartBlockBits[i] = Helper.IterateBits((byte)i).ToArray();
            }

            sw.Stop();

            Debug.WriteLine("CalculateSecondPartBlockBits finished in {0}", sw.Elapsed.ToString());
        }
    }
}
