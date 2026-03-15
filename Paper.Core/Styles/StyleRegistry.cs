namespace Paper.Core.Styles
{
    public sealed class StyleRegistry
    {
        private readonly Dictionary<string, StyleSheet> _classStyles = new(StringComparer.Ordinal);
        private readonly object _csssLock = new();
        private List<object> _csssSheets = []; // typed as object to avoid a hard reference to Paper.CSSS

        /// <summary>
        /// Increments whenever the registry changes (class added, CSSS sheet added/replaced/removed).
        /// Consumers can compare against a stored version to detect staleness.
        /// </summary>
        public int Version { get; private set; }

        public void SetClass(string className, StyleSheet style)
        {
            _classStyles[className] = style;
            Version++;
        }

        public bool TryGetClass(string className, out StyleSheet style)
        {
            if (_classStyles.TryGetValue(className, out var s))
            {
                style = s;
                return true;
            }
            style = StyleSheet.Empty;
            return false;
        }

        /// <summary>Register a CSSS sheet (typed as object to avoid coupling Paper.Core → Paper.CSSS).</summary>
        public void AddCSSSSheet(object sheet)
        {
            lock (_csssLock)
            {
                if (!_csssSheets.Contains(sheet))
                {
                    _csssSheets = [.. _csssSheets, sheet];
                    Version++;
                }
            }
        }

        /// <summary>Replace a CSSS sheet by source path (hot-reload safe — called from background thread).</summary>
        public void ReplaceCSSSSheet(string sourcePath, object newSheet)
        {
            lock (_csssLock)
            {
                var list = new List<object>(_csssSheets);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is ICSSSSheet s && s.SourcePath == sourcePath)
                    {
                        list[i] = newSheet;
                        _csssSheets = list;
                        Version++;
                        return;
                    }
                }
                list.Add(newSheet);
                _csssSheets = list;
                Version++;
            }
        }

        /// <summary>Remove a CSSS sheet by source path.</summary>
        public void RemoveCSSSSheet(string sourcePath)
        {
            lock (_csssLock)
            {
                _csssSheets = _csssSheets.Where(s => s is not ICSSSSheet cs || cs.SourcePath != sourcePath).ToList();
                Version++;
            }
        }

        /// <summary>Snapshot of CSSS sheets — safe to iterate from any thread.</summary>
        public IReadOnlyList<object> CSSSSheets
        {
            get { lock (_csssLock) return _csssSheets; }
        }
    }

    /// <summary>Minimal interface implemented by CSSSSheet so StyleRegistry can manage sheets without depending on Paper.CSSS.</summary>
    public interface ICSSSSheet
    {
        string SourcePath { get; }
    }
}

