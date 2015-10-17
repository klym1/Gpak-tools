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
            using (Stopwatch(name, onFinish))
            {
                return func();
            }
        }

        public static void WithMeasurement(Action act, string name = null, Action<TimeSpan> onFinish = null)
        {
            using (Stopwatch(name, onFinish))
            {
                act();
            }
        }

        private static IDisposable Stopwatch(string name = null, Action<TimeSpan> onFinish = null)
        {
            var sw = new Stopwatch();
            var disposable = Disposable.Create(before: sw.Start, after: delegate
            {
                sw.Stop();
                Debug.WriteLine("{0} : {1:D}", name ?? "Default", sw.ElapsedMilliseconds);

                if (onFinish != null)
                {
                    onFinish(sw.Elapsed);
                }
            });

            return disposable;
        }
    }

    public static class Disposable
    {
        private sealed class DisposableResult : IDisposable
        {
            private readonly Action _onDispose;

            public DisposableResult(Action onDispose)
            {
                this._onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public static IDisposable Create(Action before, Action after)
        {
            before();
            return new DisposableResult(after);
        }
    }
}