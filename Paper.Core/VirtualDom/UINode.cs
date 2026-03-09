using Paper.Core.Styles;

namespace Paper.Core.VirtualDom
{
    /// <summary>
    /// An immutable description of a UI element — Paper's equivalent of a React element.
    /// Created by <see cref="UI"/> factory methods; consumed by the reconciler.
    /// </summary>
    public sealed class UINode
    {
        /// <summary>
        /// Element type. One of:
        /// <list type="bullet">
        ///   <item>A <see cref="string"/> constant from <see cref="Elements"/> for intrinsic nodes ("box", "text", etc.)</item>
        ///   <item>A <see cref="System.Type"/> for class components (<see cref="Components.Component"/>)</item>
        ///   <item>A <see cref="Func{Props,UINode}"/> delegate for function components</item>
        /// </list>
        /// </summary>
        public object Type { get; }

        /// <summary>Props passed to this element.</summary>
        public Props Props { get; }

        /// <summary>
        /// Optional stable key used by the reconciler to match nodes across renders.
        /// Set this on list items to preserve component state during reorders.
        /// </summary>
        public string? Key { get; }

        public UINode(object type, Props props, string? key = null)
        {
            Type = type;
            Props = props;
            Key = key;
        }

        /// <summary>Children from props (shorthand).</summary>
        public IReadOnlyList<UINode> Children => Props.Children;

        /// <summary>Style from props (shorthand).</summary>
        public StyleSheet? Style => Props.Style;

        public override string ToString()
        {
            var typeName = Type switch
            {
                string s => s,
                Type t => t.Name,
                _ => Type.ToString() ?? "?",
            };
            return $"<{typeName} key={Key ?? "null"} children={Children.Count}>";
        }
    }
}
