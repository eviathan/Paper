using Silk.NET.GLFW;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        private unsafe void InitGlfwCursors()
        {
            try
            {
                _glfwState.GLFW = Glfw.GetApi();
                if (_glfwState.GLFW == null) return;

                _glfwState.CursorArrow = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.Arrow);
                _glfwState.CursorHand = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.Hand);
                _glfwState.CursorIBeam = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.IBeam);
                _glfwState.CursorCrosshair = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.Crosshair);
                _glfwState.CursorEwResize = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.HResize);
                _glfwState.CursorNsResize = (nint)_glfwState.GLFW.CreateStandardCursor(CursorShape.VResize);
            }
            catch { }
        }

        private unsafe void DestroyGlfwCursors()
        {
            if (_glfwState.GLFW == null) return;
            try
            {
                if (_glfwState.CursorArrow != 0)
                    _glfwState.GLFW.DestroyCursor((Cursor*)_glfwState.CursorArrow);
                if (_glfwState.CursorHand != 0)
                    _glfwState.GLFW.DestroyCursor((Cursor*)_glfwState.CursorHand);
                if (_glfwState.CursorIBeam != 0)
                    _glfwState.GLFW.DestroyCursor((Cursor*)_glfwState.CursorIBeam);
                if (_glfwState.CursorCrosshair != 0)
                    _glfwState.GLFW.DestroyCursor((Cursor*)_glfwState.CursorCrosshair);
                if (_glfwState.CursorEwResize != 0)
                    _glfwState.GLFW.DestroyCursor((Cursor*)_glfwState.CursorEwResize);
                if (_glfwState.CursorNsResize != 0)
                    _glfwState.GLFW.DestroyCursor((Cursor*)_glfwState.CursorNsResize);

                _glfwState.CursorArrow = _glfwState.CursorHand = _glfwState.CursorIBeam = _glfwState.CursorCrosshair = _glfwState.CursorEwResize = _glfwState.CursorNsResize = 0;
                _glfwState.GLFW.Dispose();
                _glfwState.GLFW = null;
            }
            catch { }
        }

        private unsafe void ApplyGlfwCursor(Core.Styles.Cursor cursor)
        {
            if (_glfwState.GLFW == null) return;
            try
            {
                nint windowHandle = _window!.Handle;
                if (windowHandle == 0) return;
                nint cursorHandle = cursor switch
                {
                    Core.Styles.Cursor.Pointer => _glfwState.CursorHand,
                    Core.Styles.Cursor.Text => _glfwState.CursorIBeam,
                    Core.Styles.Cursor.Crosshair => _glfwState.CursorCrosshair,
                    Core.Styles.Cursor.EwResize or Core.Styles.Cursor.ColResize => _glfwState.CursorEwResize,
                    Core.Styles.Cursor.RowResize => _glfwState.CursorNsResize,
                    Core.Styles.Cursor.None => 0,
                    _ => _glfwState.CursorArrow,
                };
                _glfwState.GLFW.SetCursor((WindowHandle*)windowHandle, (Cursor*)cursorHandle);
            }
            catch { }
        }

        /// <summary>Applies minimum window size constraints via GLFW. No-op if not set or backend is not GLFW.</summary>
        private unsafe void ApplyMinimumWindowSizeLimits()
        {
            if (!MinimumWindowWidth.HasValue && !MinimumWindowHeight.HasValue) return;
            int minWidth = MinimumWindowWidth ?? 1;
            int minHeight = MinimumWindowHeight ?? 1;
            try
            {
                var glfw = Glfw.GetApi();
                if (glfw == null) return;
                nint windowHandle = _window!.Handle;
                if (windowHandle == 0) return;
                glfw.SetWindowSizeLimits((WindowHandle*)windowHandle, minWidth, minHeight, Glfw.DontCare, Glfw.DontCare);
            }
            catch { }
        }
    }
}
