using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Silk.NET.GLFW;

namespace Paper.Rendering.Silk.NET.Models
{
    public class GLFWState
    {
        public Glfw? GLFW;
        public nint CursorArrow;
        public nint CursorHand;
        public nint CursorIBeam;
        public nint CursorCrosshair;
        public nint CursorEwResize;
    }
}