using System.Globalization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Paper.CSX.LanguageServer
{
    /// <summary>
    /// Loads a .NET XML documentation file and serves member docs to Roslyn's
    /// <see cref="ISymbol.GetDocumentationCommentXml"/> pipeline.
    /// </summary>
    internal sealed class XmlDocProvider : DocumentationProvider
    {
        private readonly string _xmlPath;
        private Dictionary<string, string>? _index;

        public XmlDocProvider(string xmlPath) => _xmlPath = xmlPath;

        protected override string? GetDocumentationForSymbol(
            string documentationMemberID,
            CultureInfo preferredCulture,
            CancellationToken cancellationToken = default)
        {
            _index ??= BuildIndex();
            return _index.TryGetValue(documentationMemberID, out var xml) ? xml : null;
        }

        private Dictionary<string, string> BuildIndex()
        {
            try
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                var doc    = XDocument.Load(_xmlPath);
                foreach (var member in doc.Descendants("member"))
                {
                    var name = member.Attribute("name")?.Value;
                    if (name == null) continue;
                    // Roslyn expects only the inner content, not the <member> wrapper
                    result[name] = string.Concat(member.Nodes().Select(n => n.ToString()));
                }
                return result;
            }
            catch { return new Dictionary<string, string>(); }
        }

        public override bool Equals(object? obj) =>
            obj is XmlDocProvider p && p._xmlPath == _xmlPath;

        public override int GetHashCode() => _xmlPath.GetHashCode(StringComparison.Ordinal);
    }
}
