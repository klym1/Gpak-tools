using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GPExtractor
{
    public static class Helper
    {
        private static IEnumerable<int> IterateBitsLeftToRight(byte value)
        {
            for (int i = 0; i < 8; i++)
            {
                yield return ((value >> i) & 0x1);
            }
        }

        public static IEnumerable<int> IterateBits(byte value)
        {
            return IterateBitsLeftToRight(value).Reverse();
        }

        public static void DumpArray(byte [] bytes, int offset = 0, int count = 0)
        {
            var str = String.Empty;

            for (var k = 0; k < (count == 0 ? bytes.Length-offset : count); k++)
            {
                str += String.Format("{0:X2} ", bytes[offset + k]);
            }
            
            Debug.WriteLine(str);
        }

        public static T WithMeasurement<T>(Func<T> func, string name = null, Action<TimeSpan> onFinish = null)
        {
            return StopwatchEncloser(func, name, onFinish);
        }

        public static void WithMeasurement(Action act, string name = null, Action<TimeSpan> onFinish = null)
        {
            StopwatchEncloser(act, name, onFinish);
        }

        private static void StopwatchEncloser(Action act, string name = null, Action<TimeSpan> onFinish = null)
        {
            var sw = Stopwatch.StartNew();

            act();

            sw.Stop();
            if (onFinish != null)
            {
                onFinish(sw.Elapsed);
            }
            Debug.WriteLine("{0} : {1:D}", name ?? "Default", sw.ElapsedMilliseconds); 
        }

        private static T StopwatchEncloser<T>(Func<T> func, string name = null, Action<TimeSpan> onFinish = null)
        {
            var sw = Stopwatch.StartNew();

            var result = func();

            sw.Stop();
            if (onFinish != null)
            {
                onFinish(sw.Elapsed);
            }
            Debug.WriteLine("{0} : {1:D}", name ?? "Default", sw.ElapsedMilliseconds);

            return result;
        }
    }
}