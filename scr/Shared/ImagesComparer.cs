using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class ImagesComparer
    {
        public void Compare(Bitmap one, Bitmap two)
        {
            CompareInternal(one, two);
        }

        private void CompareInternal(Bitmap one, Bitmap two)
        {
            if (one.Width != two.Width || one.Height != two.Height)
            {
                //Sizes do not match
                return;
            }

            var lockedOne = new LockBitmap(one);
            var lockedTwo = new LockBitmap(two);

            var numberOfDifferentPixels = 0;

            for (int i = 0; i < one.Width; i++)
                for (int j = 0; j < one.Height; j++)
            {
                if (lockedOne.GetPixel(i, j) != lockedTwo.GetPixel(i, j))
                {
                    numberOfDifferentPixels++;
                }
            }

            Debug.WriteLine("Different pixels: " + numberOfDifferentPixels);
        }
    }
}
