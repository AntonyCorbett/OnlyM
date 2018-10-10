namespace OnlyM.Core.Models
{
    using System;

    public class ScreenPosition : ICloneable
    {
        public ScreenPosition()
        {
        }

        public ScreenPosition(int left, int top, int right, int bottom)
        {
            LeftMarginPercentage = left;
            TopMarginPercentage = top;
            RightMarginPercentage = right;
            BottomMarginPercentage = bottom;
        }

        public int LeftMarginPercentage { get; set; }

        public int TopMarginPercentage { get; set; }

        public int RightMarginPercentage { get; set; }

        public int BottomMarginPercentage { get; set; }

        public bool IsFullScreen()
        {
            return LeftMarginPercentage == 0 &&
                   TopMarginPercentage == 0 &&
                   RightMarginPercentage == 0 &&
                   BottomMarginPercentage == 0;
        }

        public object Clone()
        {
            return new ScreenPosition(
                LeftMarginPercentage, TopMarginPercentage, RightMarginPercentage, BottomMarginPercentage);
        }

        public bool SamePosition(ScreenPosition other)
        {
            return LeftMarginPercentage == other.LeftMarginPercentage &&
                   TopMarginPercentage == other.TopMarginPercentage &&
                   RightMarginPercentage == other.RightMarginPercentage &&
                   BottomMarginPercentage == other.BottomMarginPercentage;
        }

        public void Sanitize()
        {
            if (LeftMarginPercentage + RightMarginPercentage > 90)
            {
                LeftMarginPercentage = 0;
                RightMarginPercentage = 0;
            }

            if (TopMarginPercentage + BottomMarginPercentage > 90)
            {
                TopMarginPercentage = 0;
                BottomMarginPercentage = 0;
            }
        }
    }
}
