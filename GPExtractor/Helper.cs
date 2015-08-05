using System;
using System.Diagnostics;

namespace GPExtractor
{
    public static class Helper
    {
        public static void DumpArray(byte [] bytes, int offset = 0, int count = 0)
        {
            var str = String.Empty;

            for (var k = 0; k < (count == 0 ? bytes.Length-offset : count); k++)
            {
                str += String.Format("{0:X2} ", bytes[offset + k]);
            }
            
            Debug.WriteLine(str);
        }
    }
}