﻿using System;

namespace OnlyM.Core.Models;

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

    public bool IsFullScreen() =>
        LeftMarginPercentage == 0 &&
        TopMarginPercentage == 0 &&
        RightMarginPercentage == 0 &&
        BottomMarginPercentage == 0;

    public object Clone() =>
        new ScreenPosition(
            LeftMarginPercentage, TopMarginPercentage, RightMarginPercentage, BottomMarginPercentage);

    public bool SamePosition(ScreenPosition other) =>
        LeftMarginPercentage == other.LeftMarginPercentage &&
        TopMarginPercentage == other.TopMarginPercentage &&
        RightMarginPercentage == other.RightMarginPercentage &&
        BottomMarginPercentage == other.BottomMarginPercentage;

    public void Sanitize()
    {
        if (LeftMarginPercentage < 0)
        {
            LeftMarginPercentage = 0;
        }

        if (RightMarginPercentage < 0)
        {
            RightMarginPercentage = 0;
        }

        if (TopMarginPercentage < 0)
        {
            TopMarginPercentage = 0;
        }

        if (BottomMarginPercentage < 0)
        {
            BottomMarginPercentage = 0;
        }

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
