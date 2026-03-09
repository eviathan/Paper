namespace Paper.CSX.Syntax
{
    public readonly record struct TextSpan(int Start, int Length)
    {
        public int End => Start + Length;
    }

    public abstract record CSXNode(TextSpan Span);

    public sealed record CSXElement(
        string Name,
        IReadOnlyList<CSXAttribute> Attributes,
        IReadOnlyList<CSXChild> Children,
        TextSpan Span) : CSXNode(Span);

    public sealed record CSXAttribute(string Name, CSXAttributeValue Value, TextSpan Span);

    public abstract record CSXAttributeValue(TextSpan Span);
    public sealed record CSXStringValue(string Value, TextSpan Span) : CSXAttributeValue(Span);
    public sealed record CSXExpressionValue(string Code, TextSpan Span) : CSXAttributeValue(Span);
    public sealed record CSXBareValue(string Value, TextSpan Span) : CSXAttributeValue(Span);

    public abstract record CSXChild(TextSpan Span);
    public sealed record CSXText(string Text, TextSpan Span) : CSXChild(Span);
    public sealed record CSXExpression(string Code, TextSpan Span) : CSXChild(Span);
    public sealed record CSXChildElement(CSXElement Element) : CSXChild(Element.Span);
}

