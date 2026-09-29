using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ErpSystem.Core.Interfaces.Common;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;

namespace ErpSystem.Api.Services.DocumentManagement;

/// <summary>
/// HR's rendered letters as PDFs (round 4, lane N-b) — see <see cref="IHtmlToPdfRenderer"/>. The HTML is
/// imported into a Syncfusion Word document and rendered to PDF, the same engine
/// <see cref="CentralDocumentRenditionService"/> converts Word files with.
/// </summary>
/// <remarks>
/// <para>⚠ <b>The importer reads XHTML, and HR's letters are HTML.</b> A letter is a fragment, uses
/// <c>&amp;nbsp;</c> and <c>&amp;mdash;</c> (XML knows neither), and — once HR rewords it on the template
/// screen — may carry a <c>&lt;br&gt;</c> or an unquoted attribute. So the letter is normalised first:
/// wrapped in a document, named entities made numeric, bare ampersands escaped, void elements closed,
/// attribute values quoted, <c>rem</c> lengths turned into <c>px</c> (the importer drew rem-styled
/// tables as empty boxes) — and then checked with an XML parser, so that a letter the importer would
/// refuse is reported with the parser's own reason rather than as a generic conversion failure.</para>
/// </remarks>
public sealed class HtmlToPdfRenderer : IHtmlToPdfRenderer
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
    };

    private readonly ILogger<HtmlToPdfRenderer> _logger;

    public HtmlToPdfRenderer(ILogger<HtmlToPdfRenderer> logger) => _logger = logger;

    public Task<byte[]?> RenderAsync(string html, string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html)) return Task.FromResult<byte[]?>(null);

        string xhtml;
        try
        {
            xhtml = ToXhtml(html, title);
            XDocument.Parse(xhtml);
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(
                "{Title} could not be made a PDF: its HTML is not well-formed even after normalising ({Reason}).",
                title, ex.Message);
            return Task.FromResult<byte[]?>(null);
        }

        try
        {
            using var source = new MemoryStream(Encoding.UTF8.GetBytes(xhtml));
            using var document = new WordDocument(source, FormatType.Html, XHTMLValidationType.None);
            using var renderer = new DocIORenderer();
            using var pdf = renderer.ConvertToPDF(document);
            using var output = new MemoryStream();
            pdf.Save(output);
            return Task.FromResult<byte[]?>(output.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Title} could not be made a PDF.", title);
            return Task.FromResult<byte[]?>(null);
        }
    }

    /// <summary>A letter's HTML as a well-formed XHTML document the Word importer accepts.</summary>
    internal static string ToXhtml(string html, string title)
    {
        var content = html;

        // A whole document? What is inside its <body> is the letter.
        var body = Regex.Match(content, @"<body\b[^>]*>(?<inner>[\s\S]*?)</body\s*>", RegexOptions.IgnoreCase);
        if (body.Success) content = body.Groups["inner"].Value;

        content = Regex.Replace(content, @"<!DOCTYPE[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<script\b[\s\S]*?</script\s*>", string.Empty, RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<!--[\s\S]*?-->", string.Empty);

        // Named entities XML does not know (&nbsp; &mdash; …) become numeric; the five it does stay.
        content = Regex.Replace(content, @"&([a-zA-Z][a-zA-Z0-9]*);", m =>
        {
            var name = m.Groups[1].Value;
            if (name is "amp" or "lt" or "gt" or "quot" or "apos") return m.Value;
            var decoded = WebUtility.HtmlDecode(m.Value);
            if (decoded == m.Value) return "&amp;" + name + ";"; // not an entity at all — print it as written
            var numeric = new StringBuilder();
            for (var i = 0; i < decoded.Length; i++)
            {
                numeric.Append("&#").Append(char.ConvertToUtf32(decoded, i)).Append(';');
                if (char.IsHighSurrogate(decoded[i])) i++;
            }
            return numeric.ToString();
        });

        // A bare ampersand — a query string, "Smith & Sons" — is escaped.
        content = Regex.Replace(content, @"&(?!#\d+;|#x[0-9a-fA-F]+;|[a-zA-Z][a-zA-Z0-9]*;)", "&amp;");

        // Opening tags: void elements closed, attribute values quoted.
        content = Regex.Replace(content, @"<(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>(?:\s[^<>]*?)?)\s*(?<self>/?)>", m =>
        {
            var name = m.Groups["name"].Value;
            var attrs = QuoteAttributes(m.Groups["attrs"].Value);
            var closed = VoidElements.Contains(name) || m.Groups["self"].Value == "/";
            return closed ? $"<{name}{attrs} />" : $"<{name}{attrs}>";
        });

        return "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>" + WebUtility.HtmlEncode(title)
             + "</title></head><body>" + content + "</body></html>";
    }

    /// <summary>
    /// ⚠ <c>rem</c> becomes <c>px</c> (at the browser default of 16px). The importer does not know the unit,
    /// and a table styled in it — every table HR's letters draw — rendered as an EMPTY box: the rows'
    /// height survived, the columns collapsed to nothing, and the text went with them. Measured on the
    /// offer letter: its terms and salary tables were blank in the PDF, and whole with this conversion.
    /// </summary>
    internal static string RemToPx(string style) =>
        Regex.Replace(style, @"(\d*\.?\d+)rem\b", m =>
            (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 16)
                .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px");

    /// <summary>
    /// Rewrites a tag's attributes so XML accepts them: every value quoted (keeping the quote it had),
    /// a bare attribute given its own name as its value. Read character by character, never by regex,
    /// so a space or an equals sign INSIDE a quoted value — a style, a font list — is left alone.
    /// </summary>
    private static string QuoteAttributes(string attrs)
    {
        if (string.IsNullOrWhiteSpace(attrs)) return string.Empty;
        var result = new StringBuilder();
        var i = 0;
        while (i < attrs.Length)
        {
            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;
            if (i >= attrs.Length) break;

            var nameStart = i;
            while (i < attrs.Length && !char.IsWhiteSpace(attrs[i]) && attrs[i] != '=') i++;
            var name = attrs[nameStart..i];
            if (name.Length == 0) { i++; continue; }

            var j = i;
            while (j < attrs.Length && char.IsWhiteSpace(attrs[j])) j++;
            if (j >= attrs.Length || attrs[j] != '=')
            {
                result.Append(' ').Append(name).Append("=\"").Append(name).Append('"');
                i = j;
                continue;
            }

            j++; // past '='
            while (j < attrs.Length && char.IsWhiteSpace(attrs[j])) j++;
            char quote;
            string value;
            if (j < attrs.Length && attrs[j] is '"' or '\'')
            {
                quote = attrs[j];
                var end = attrs.IndexOf(quote, j + 1);
                if (end < 0) end = attrs.Length; // unterminated: the rest is the value
                value = attrs[(j + 1)..end];
                i = Math.Min(end + 1, attrs.Length);
            }
            else
            {
                var valueStart = j;
                while (j < attrs.Length && !char.IsWhiteSpace(attrs[j])) j++;
                value = attrs[valueStart..j];
                quote = value.Contains('"') ? '\'' : '"';
                i = j;
            }
            if (string.Equals(name, "style", StringComparison.OrdinalIgnoreCase)) value = RemToPx(value);
            result.Append(' ').Append(name).Append('=').Append(quote).Append(value).Append(quote);
        }
        return result.ToString();
    }
}
