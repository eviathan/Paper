using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// Intrinsic element type name constants.
    /// </summary>
    public static class ElementTypes
    {
        public const string Box = "box";
        public const string Text = "text";
        public const string Image = "image";
        public const string Input = "input";
        public const string Button = "button";
        public const string Scroll = "scroll";
        /// <summary>A fragment — renders its children with no wrapper element.</summary>
        public const string Fragment = "fragment";
        /// <summary>A viewport that renders an external OpenGL texture (e.g. engine game view).</summary>
        public const string Viewport = "viewport";
        public const string Checkbox = "checkbox";
        public const string Textarea = "textarea";
        public const string Table = "table";
        public const string TableRow = "table-row";
        public const string TableCell = "table-cell";
        public const string RadioGroup = "radio-group";
        public const string RadioOption = "radio-option";
        /// <summary>
        /// A portal — reconciles children normally but renders them into a separate
        /// overlay pass on top of the main tree (after z-index deferred items).
        /// Use for modals, tooltips, dropdowns, and toasts.
        /// </summary>
        public const string Portal = "portal";
    }
}