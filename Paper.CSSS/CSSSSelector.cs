using Paper.Core.Reconciler;
using Paper.Core.Styles;

namespace Paper.CSSS
{
    internal enum CSSSCombinator { Descendant, Child, AdjacentSibling, GeneralSibling }

    internal sealed class CSSSSegment
    {
        public string? Element   { get; init; }  // element type e.g. "Box"; null = any; "*" = universal
        public string? Class     { get; init; }  // class name without dot
        public string? Id        { get; init; }  // id without hash

        public bool Matches(Fiber fiber)
        {
            if (Element != null && Element != "*")
            {
                if (fiber.Type is not string t || !string.Equals(t, Element, StringComparison.Ordinal))
                    return false;
            }
            if (Class != null)
            {
                var cls = fiber.Props.ClassName ?? "";
                bool found = false;
                foreach (var token in cls.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (token == Class) { found = true; break; }
                if (!found) return false;
            }
            if (Id != null && fiber.Props.Id != Id)
                return false;
            return true;
        }

        /// <summary>Parse a single selector token like "Box", ".card", "#header", "Box.card", "*".</summary>
        public static CSSSSegment Parse(string token)
        {
            string t = token.Trim();
            if (string.IsNullOrEmpty(t) || t == "*") return new CSSSSegment { Element = "*" };
            if (t.StartsWith('#')) return new CSSSSegment { Id = t[1..] };
            if (t.StartsWith('.')) return new CSSSSegment { Class = t[1..] };
            // element.class compound: e.g. "Box.card"
            int dotIdx = t.IndexOf('.');
            if (dotIdx > 0) return new CSSSSegment { Element = t[..dotIdx], Class = t[(dotIdx + 1)..] };
            return new CSSSSegment { Element = t };
        }
    }

    // ── Pseudo-classes ────────────────────────────────────────────────────────

    internal abstract class CSSSPseudo
    {
        public abstract bool Matches(Fiber fiber, InteractionState state);

        public static CSSSPseudo? TryParse(string pseudo)
        {
            return pseudo switch
            {
                ":hover"       => new SimplePseudo(s => s.Hover),
                ":active"      => new SimplePseudo(s => s.Active),
                ":focus"       => new SimplePseudo(s => s.Focus),
                ":first-child" => new PositionPseudo(static f => ChildIndex(f) == 1),
                ":last-child"  => new PositionPseudo(static f => IsLastChild(f)),
                ":only-child"  => new PositionPseudo(static f => ChildIndex(f) == 1 && IsLastChild(f)),
                _ when pseudo.StartsWith(":nth-child(") => NthChildPseudo.Parse(pseudo),
                _ when pseudo.StartsWith(":not(")      => NotPseudo.Parse(pseudo),
                _ => null,
            };
        }

        // ── Sibling position helpers ──────────────────────────────────────────

        internal static int ChildIndex(Fiber f)
        {
            if (f.Parent == null) return 1;
            int idx = 1;
            var cur = f.Parent.Child;
            while (cur != null && cur != f) { idx++; cur = cur.Sibling; }
            return idx;
        }

        internal static bool IsLastChild(Fiber f) => f.Sibling == null;
    }

    internal sealed class SimplePseudo(Func<InteractionState, bool> pred) : CSSSPseudo
    {
        public override bool Matches(Fiber fiber, InteractionState state) => pred(state);
    }

    internal sealed class PositionPseudo(Func<Fiber, bool> pred) : CSSSPseudo
    {
        public override bool Matches(Fiber fiber, InteractionState state) => pred(fiber);
    }

    internal sealed class NthChildPseudo : CSSSPseudo
    {
        private readonly int _a; // step
        private readonly int _b; // offset — matches when (index - b) % a == 0

        private NthChildPseudo(int a, int b) { _a = a; _b = b; }

        public override bool Matches(Fiber fiber, InteractionState state)
        {
            int idx = ChildIndex(fiber);
            if (_a == 0) return idx == _b;
            int rem = idx - _b;
            return rem >= 0 && rem % _a == 0;
        }

        public static CSSSPseudo? Parse(string pseudo)
        {
            // ":nth-child(2n+1)", ":nth-child(odd)", ":nth-child(even)", ":nth-child(3)"
            int start = pseudo.IndexOf('(') + 1;
            int end   = pseudo.LastIndexOf(')');
            if (start <= 0 || end <= start) return null;
            var arg = pseudo[start..end].Trim();
            return arg switch
            {
                "odd"  => new NthChildPseudo(2, 1),
                "even" => new NthChildPseudo(2, 2),
                _ when int.TryParse(arg, out int n) => new NthChildPseudo(0, n),
                _ => ParseAnPlusB(arg),
            };
        }

        private static CSSSPseudo? ParseAnPlusB(string expr)
        {
            // e.g. "2n+1", "3n", "n+2", "-n+3"
            expr = expr.Replace(" ", "");
            int nIdx = expr.IndexOf('n');
            if (nIdx < 0) return null;
            string aPart = expr[..nIdx];
            int a = aPart is "" or "+" ? 1 : aPart == "-" ? -1 : int.TryParse(aPart, out int ap) ? ap : 1;
            int b = 0;
            if (nIdx + 1 < expr.Length && int.TryParse(expr[(nIdx + 1)..], out int bp)) b = bp;
            return new NthChildPseudo(a, b);
        }
    }

    internal sealed class NotPseudo : CSSSPseudo
    {
        private readonly CSSSSelector _inner;
        private NotPseudo(CSSSSelector inner) { _inner = inner; }

        public override bool Matches(Fiber fiber, InteractionState state) =>
            !_inner.Matches(fiber, state);

        public static CSSSPseudo? Parse(string pseudo)
        {
            int start = pseudo.IndexOf('(') + 1;
            int end   = pseudo.LastIndexOf(')');
            if (start <= 0 || end <= start) return null;
            var inner = CSSSSelector.Parse(pseudo[start..end].Trim());
            return new NotPseudo(inner);
        }
    }

    // ── Selector ──────────────────────────────────────────────────────────────

    internal sealed class CSSSSelector
    {
        public CSSSSegment[]    Segments    { get; }
        public CSSSCombinator[] Combinators { get; }
        public CSSSPseudo[]     Pseudos     { get; }  // all pseudo-classes on the rightmost segment

        public CSSSSelector(CSSSSegment[] segments, CSSSCombinator[] combinators, CSSSPseudo[] pseudos)
        {
            Segments    = segments;
            Combinators = combinators;
            Pseudos     = pseudos;
        }

        public bool Matches(Fiber fiber, InteractionState state)
        {
            if (Segments.Length == 0) return false;

            // All pseudo-classes must match
            foreach (var p in Pseudos)
                if (!p.Matches(fiber, state)) return false;

            // Rightmost segment must match the current fiber
            if (!Segments[^1].Matches(fiber)) return false;

            // Walk the ancestor/sibling chain for remaining segments (right to left)
            Fiber? cursor = fiber;
            for (int i = Segments.Length - 2; i >= 0; i--)
            {
                var comb = Combinators[i];
                var seg  = Segments[i];

                switch (comb)
                {
                    case CSSSCombinator.Child:
                        cursor = cursor?.Parent;
                        if (cursor == null || !seg.Matches(cursor)) return false;
                        break;

                    case CSSSCombinator.Descendant:
                        cursor = cursor?.Parent;
                        bool found = false;
                        while (cursor != null)
                        {
                            if (seg.Matches(cursor)) { found = true; break; }
                            cursor = cursor.Parent;
                        }
                        if (!found) return false;
                        break;

                    case CSSSCombinator.AdjacentSibling:
                        // Match the immediately preceding sibling
                        cursor = PrecedingSibling(cursor);
                        if (cursor == null || !seg.Matches(cursor)) return false;
                        break;

                    case CSSSCombinator.GeneralSibling:
                        // Match any preceding sibling
                        cursor = PrecedingSibling(cursor);
                        bool foundSib = false;
                        while (cursor != null)
                        {
                            if (seg.Matches(cursor)) { foundSib = true; break; }
                            cursor = PrecedingSibling(cursor);
                        }
                        if (!foundSib) return false;
                        break;
                }
            }
            return true;
        }

        /// <summary>Returns the sibling immediately before <paramref name="f"/> in its parent's child list.</summary>
        private static Fiber? PrecedingSibling(Fiber? f)
        {
            if (f?.Parent == null) return null;
            Fiber? prev = null;
            var cur = f.Parent.Child;
            while (cur != null && cur != f) { prev = cur; cur = cur.Sibling; }
            return prev;
        }

        /// <summary>Parse a CSS selector string into a typed CSSSSelector.</summary>
        public static CSSSSelector Parse(string selector)
        {
            string sel = selector.Trim();

            // Extract all pseudo-classes from the rightmost segment.
            // We scan from the end for ":pseudo" and ":pseudo(...)" tokens.
            var pseudos = new List<CSSSPseudo>();
            sel = ExtractPseudos(sel, pseudos);

            // Tokenise by combinators: ' ' (descendant), ' > ' (child), ' + ' (adjacent), ' ~ ' (general)
            // Normalise spacing around > + ~ then split
            var normalised = sel
                .Replace(" > ", " \x01 ")   // child
                .Replace(" + ", " \x02 ")   // adjacent sibling
                .Replace(" ~ ", " \x03 ")   // general sibling
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var segments    = new List<CSSSSegment>();
            var combinators = new List<CSSSCombinator>();
            var pendingComb = CSSSCombinator.Descendant;
            bool hasPending = false;

            foreach (var part in normalised)
            {
                if (part == "\x01") { pendingComb = CSSSCombinator.Child;           hasPending = true; continue; }
                if (part == "\x02") { pendingComb = CSSSCombinator.AdjacentSibling; hasPending = true; continue; }
                if (part == "\x03") { pendingComb = CSSSCombinator.GeneralSibling;  hasPending = true; continue; }

                if (segments.Count > 0)
                    combinators.Add(hasPending ? pendingComb : CSSSCombinator.Descendant);
                hasPending = false;
                pendingComb = CSSSCombinator.Descendant;
                segments.Add(CSSSSegment.Parse(part));
            }

            return new CSSSSelector(segments.ToArray(), combinators.ToArray(), pseudos.ToArray());
        }

        /// <summary>
        /// Strips trailing pseudo-class tokens from <paramref name="sel"/>, adds parsed pseudos
        /// to <paramref name="pseudos"/>, and returns the remaining selector text.
        /// </summary>
        private static string ExtractPseudos(string sel, List<CSSSPseudo> pseudos)
        {
            // Walk backwards peeling pseudo tokens off the end.
            // A pseudo is either ":name" or ":name(...)" (balanced parens).
            while (true)
            {
                sel = sel.TrimEnd();
                int colon = FindLastPseudoColon(sel);
                if (colon < 0) break;

                string pseudoStr = sel[colon..];
                var p = CSSSPseudo.TryParse(pseudoStr);
                if (p == null) break;

                pseudos.Insert(0, p);
                sel = sel[..colon].TrimEnd();
                if (string.IsNullOrEmpty(sel)) break;
            }
            return sel;
        }

        /// <summary>
        /// Finds the start index of the last pseudo-class token (":name" or ":name(...)") in <paramref name="sel"/>.
        /// Returns -1 if none found.
        /// </summary>
        private static int FindLastPseudoColon(string sel)
        {
            // If the selector ends with ')', find the matching '(' then find the ':' before the function name.
            if (sel.EndsWith(')'))
            {
                int depth = 0, i = sel.Length - 1;
                while (i >= 0) { if (sel[i] == ')') depth++; else if (sel[i] == '(') { depth--; if (depth == 0) break; } i--; }
                // Now find the ':' before position i (the function name is between ':' and '(')
                int colon = sel.LastIndexOf(':', i - 1);
                return colon >= 0 ? colon : -1;
            }
            // Otherwise just find the last ':'
            int c = sel.LastIndexOf(':');
            // Make sure what follows is a valid pseudo name (letters, hyphens only)
            if (c < 0) return -1;
            string suffix = sel[(c + 1)..];
            return suffix.Length > 0 && suffix.All(ch => char.IsLetter(ch) || ch == '-') ? c : -1;
        }
    }
}
