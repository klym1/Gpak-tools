using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WindowsFormsTestClient
{
    [DebuggerDisplay("{Values}")]
    public class CounterSection
    {
        public int Counter { get { return SecondPartBlocks.Count; } }

        public CounterSection(List<byte> secondPartBlocks)
        {
            SecondPartBlocks = secondPartBlocks;
        }

        public string Values
        {
            get { return String.Join(" ",SecondPartBlocks.Select(it => String.Format((string) "{0:X2}", (object) it)).ToArray()); }
            
        }

        public List<byte> SecondPartBlocks;
    }
}