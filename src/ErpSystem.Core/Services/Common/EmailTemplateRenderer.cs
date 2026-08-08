using System.Text;

namespace ErpSystem.Core.Services.Common;

/// <summary>
/// Lightweight, dependency-free merge engine for email templates.
///
/// <para>Supported syntax:</para>
/// <list type="bullet">
///   <item><c>{{Token}}</c> — substitutes the token value, HTML-encoded (safe for candidate-supplied
///     values such as names).</item>
///   <item><c>{{{Token}}}</c> — substitutes the raw value without encoding, for pre-built HTML
///     fragments (e.g. an itemised salary table). Use only for values the application itself built.</item>
///   <item><c>{{#if Token}} … {{else}} … {{/if}}</c> — renders the first block when the token is
///     "truthy" (present and not empty / "false" / "0"), otherwise the (optional) else block.
///     Conditionals may be nested.</item>
/// </list>
///
/// <para>Unknown tokens render as an empty string. Whitespace inside the braces is ignored.</para>
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders a single template string against the supplied tokens.
    /// When <paramref name="htmlEncode"/> is true (the default, for HTML bodies) <c>{{Token}}</c>
    /// values are HTML-encoded; pass false for plain-text contexts such as the subject line.
    /// <c>{{{Token}}}</c> is always emitted raw.
    /// </summary>
    string Render(string template, IReadOnlyDictionary<string, string?> tokens, bool htmlEncode = true);

    /// <summary>
    /// Returns the distinct token names referenced by a template (both <c>{{x}}</c> and
    /// <c>{{#if x}}</c> forms). Useful for validation / authoring aids.
    /// </summary>
    IReadOnlyList<string> ExtractTokenNames(string template);
}

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, string?> tokens, bool htmlEncode = true)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        tokens ??= new Dictionary<string, string?>();

        var nodes = Parse(Tokenize(template));
        var sb = new StringBuilder(template.Length + 64);
        RenderNodes(nodes, tokens, sb, htmlEncode);
        return sb.ToString();
    }

    public IReadOnlyList<string> ExtractTokenNames(string template)
    {
        var names = new List<string>();
        if (string.IsNullOrEmpty(template)) return names;

        foreach (var tok in Tokenize(template))
        {
            switch (tok.Kind)
            {
                case TokKind.Var:
                case TokKind.RawVar:
                    Add(names, tok.Value);
                    break;
                case TokKind.If:
                    Add(names, tok.Value);
                    break;
            }
        }
        return names;
    }

    private static void Add(List<string> names, string name)
    {
        if (!string.IsNullOrWhiteSpace(name) &&
            !names.Contains(name, StringComparer.OrdinalIgnoreCase))
            names.Add(name);
    }

    // ── Truthiness ────────────────────────────────────────────────────────────
    private static bool IsTruthy(IReadOnlyDictionary<string, string?> tokens, string name)
    {
        if (!tokens.TryGetValue(name, out var value) || value is null) return false;
        var v = value.Trim();
        if (v.Length == 0) return false;
        if (v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if (v == "0") return false;
        return true;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────
    private static void RenderNodes(
        IReadOnlyList<Node> nodes, IReadOnlyDictionary<string, string?> tokens, StringBuilder sb, bool htmlEncode)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LiteralNode lit:
                    sb.Append(lit.Text);
                    break;
                case VarNode var:
                    tokens.TryGetValue(var.Name, out var raw);
                    sb.Append(var.Raw || !htmlEncode ? (raw ?? string.Empty) : HtmlEncode(raw ?? string.Empty));
                    break;
                case IfNode iff:
                    RenderNodes(IsTruthy(tokens, iff.Name) ? iff.WhenTrue : iff.WhenFalse, tokens, sb, htmlEncode);
                    break;
            }
        }
    }

    // Minimal HTML text encoder (avoids a System.Web dependency in Core).
    private static string HtmlEncode(string s)
    {
        if (s.Length == 0) return s;
        StringBuilder? sb = null;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            string? rep = c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => null
            };
            if (rep is null)
            {
                sb?.Append(c);
            }
            else
            {
                sb ??= new StringBuilder(s.Length + 16).Append(s, 0, i);
                sb.Append(rep);
            }
        }
        return sb?.ToString() ?? s;
    }

    // ── Tokenizer ─────────────────────────────────────────────────────────────
    private enum TokKind { Literal, Var, RawVar, If, Else, EndIf }

    private readonly record struct Tok(TokKind Kind, string Value);

    private static List<Tok> Tokenize(string template)
    {
        var toks = new List<Tok>();
        int i = 0, n = template.Length, litStart = 0;

        void FlushLiteral(int end)
        {
            if (end > litStart)
                toks.Add(new Tok(TokKind.Literal, template.Substring(litStart, end - litStart)));
        }

        while (i < n)
        {
            if (template[i] == '{' && i + 1 < n && template[i + 1] == '{')
            {
                bool raw = i + 2 < n && template[i + 2] == '{';
                int open = raw ? 3 : 2;
                string close = raw ? "}}}" : "}}";
                int contentStart = i + open;
                int end = template.IndexOf(close, contentStart, StringComparison.Ordinal);
                if (end < 0)
                {
                    // Unterminated marker — treat the rest as literal.
                    break;
                }

                FlushLiteral(i);
                var expr = template.Substring(contentStart, end - contentStart).Trim();

                if (raw)
                {
                    toks.Add(new Tok(TokKind.RawVar, expr));
                }
                else if (expr.StartsWith("#if ", StringComparison.OrdinalIgnoreCase) || expr.Equals("#if", StringComparison.OrdinalIgnoreCase))
                {
                    var name = expr.Length > 3 ? expr.Substring(3).Trim() : string.Empty;
                    toks.Add(new Tok(TokKind.If, name));
                }
                else if (expr.Equals("else", StringComparison.OrdinalIgnoreCase))
                {
                    toks.Add(new Tok(TokKind.Else, string.Empty));
                }
                else if (expr.Equals("/if", StringComparison.OrdinalIgnoreCase))
                {
                    toks.Add(new Tok(TokKind.EndIf, string.Empty));
                }
                else
                {
                    toks.Add(new Tok(TokKind.Var, expr));
                }

                i = end + close.Length;
                litStart = i;
            }
            else
            {
                i++;
            }
        }

        FlushLiteral(n);
        return toks;
    }

    // ── Parser (builds a node tree so {{#if}} nesting is honoured) ──────────────
    private abstract class Node { }
    private sealed class LiteralNode : Node { public string Text = string.Empty; }
    private sealed class VarNode : Node { public string Name = string.Empty; public bool Raw; }
    private sealed class IfNode : Node
    {
        public string Name = string.Empty;
        public List<Node> WhenTrue = new();
        public List<Node> WhenFalse = new();
    }

    private static List<Node> Parse(List<Tok> toks)
    {
        int pos = 0;
        return ParseBlock(toks, ref pos, stopAtElseOrEnd: false);
    }

    private static List<Node> ParseBlock(List<Tok> toks, ref int pos, bool stopAtElseOrEnd)
    {
        var nodes = new List<Node>();
        while (pos < toks.Count)
        {
            var tok = toks[pos];
            switch (tok.Kind)
            {
                case TokKind.Literal:
                    nodes.Add(new LiteralNode { Text = tok.Value });
                    pos++;
                    break;
                case TokKind.Var:
                    nodes.Add(new VarNode { Name = tok.Value, Raw = false });
                    pos++;
                    break;
                case TokKind.RawVar:
                    nodes.Add(new VarNode { Name = tok.Value, Raw = true });
                    pos++;
                    break;
                case TokKind.If:
                    pos++; // consume #if
                    var ifNode = new IfNode { Name = tok.Value };
                    ifNode.WhenTrue = ParseBlock(toks, ref pos, stopAtElseOrEnd: true);
                    if (pos < toks.Count && toks[pos].Kind == TokKind.Else)
                    {
                        pos++; // consume else
                        ifNode.WhenFalse = ParseBlock(toks, ref pos, stopAtElseOrEnd: true);
                    }
                    if (pos < toks.Count && toks[pos].Kind == TokKind.EndIf)
                        pos++; // consume /if
                    nodes.Add(ifNode);
                    break;
                case TokKind.Else:
                case TokKind.EndIf:
                    if (stopAtElseOrEnd) return nodes;
                    // Stray else/endif with no opening if — ignore it.
                    pos++;
                    break;
            }
        }
        return nodes;
    }
}
