using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paper.Rendering.Silk.NET.Models
{
    public class ScrollState
    {
        public Dictionary<string, (float scrollX, float scrollY)> ScrollOffsets = new();
        public string? ScrollbarDragPath;
        public float ScrollbarDragAnchorY;
        public float ScrollbarDragAnchorScroll;
        public Dictionary<string, double> ScrollbarLastActive = new ();
    }
}