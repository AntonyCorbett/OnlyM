﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnlyM.Models
{
    public class NewMediaSizeEventArgs : EventArgs
    {
        public int Width { get; set; }
        public int Height { get; set; }

    }
}
