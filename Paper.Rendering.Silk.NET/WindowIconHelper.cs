using Silk.NET.Core;
using Silk.NET.Windowing;
using StbImageSharp;
using System.Runtime.InteropServices;

namespace Paper.Rendering.Silk.NET
{
    internal static class WindowIconHelper
    {
        internal static void Apply(IWindow? window, string? iconPath)
        {
            if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath)) return;
            try
            {
                if (OperatingSystem.IsMacOS())
                    ApplyMacOS(iconPath);
                else if (window != null)
                    ApplyGlfw(window, iconPath);
            }
            catch { }
        }

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr msg0(IntPtr self, IntPtr sel);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr msg1(IntPtr self, IntPtr sel, IntPtr arg);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void msg1v(IntPtr self, IntPtr sel, IntPtr arg);

        [DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr objc_getClass(string name);

        [DllImport("/usr/lib/libobjc.dylib")]
        private static extern IntPtr sel_registerName(string name);

        private static void ApplyMacOS(string path)
        {
            IntPtr pathPtr = Marshal.StringToHGlobalAnsi(path);
            try
            {
                var allocSel      = sel_registerName("alloc");
                var nsStringClass = objc_getClass("NSString");
                var initUtf8Sel   = sel_registerName("initWithUTF8String:");
                var nsString      = msg1(msg0(nsStringClass, allocSel), initUtf8Sel, pathPtr);

                var nsImageClass  = objc_getClass("NSImage");
                var initFileSel   = sel_registerName("initWithContentsOfFile:");
                var nsImage       = msg1(msg0(nsImageClass, allocSel), initFileSel, nsString);
                if (nsImage == IntPtr.Zero) return;

                var nsAppClass    = objc_getClass("NSApplication");
                var sharedAppSel  = sel_registerName("sharedApplication");
                var setIconSel    = sel_registerName("setApplicationIconImage:");
                var app           = msg0(nsAppClass, sharedAppSel);
                msg1v(app, setIconSel, nsImage);
            }
            finally { Marshal.FreeHGlobal(pathPtr); }
        }

        private static void ApplyGlfw(IWindow window, string path)
        {
            using var stream = File.OpenRead(path);
            var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            var raw = new RawImage(img.Width, img.Height, new Memory<byte>(img.Data));
            window.SetWindowIcon(ref raw);
        }
    }
}
