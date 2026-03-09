namespace Paper.CSX.LanguageServer.Enums
{
    internal enum CompletionContext
    {
        Unknown,
        JsxTagName,       // after < but before first attr or >
        JsxPropName,      // after tag name, between attrs
        JsxStyleProp,     // after style={{ — CSS property names
        JsxStyleValue,    // after style={{ propName: — CSS values for that prop
        JsxClassName,     // inside className="..."
        JsxEventValue,    // inside onXxx={...}
        JsxPropValue,     // inside ={...}
        CSharp,           // general C# code
        ImportPath,       // inside @import "..." string — file path completions
    }
}