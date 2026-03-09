using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paper.Core.Reconciler
{
    /// <summary>
    /// The computed layout result for a fiber, filled in by Paper.Layout.
    /// </summary>
    public struct LayoutBox
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        /// <summary>X relative to the root Paper surface.</summary>
        public float AbsoluteX { get; set; }
        /// <summary>Y relative to the root Paper surface.</summary>
        public float AbsoluteY { get; set; }
    }
}