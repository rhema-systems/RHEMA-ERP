using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace ErpSystem.Api.Services.DocumentManagement;

internal static class DocumentTemplateWordBodyExtractor
{
    private static readonly Regex MergeFieldPattern = new(
        @"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}",
        RegexOptions.Compiled);

    public static string ExtractBody(Stream source)
    {
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        using var document = WordprocessingDocument.Open(source, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var paragraphs = body
            .Descendants<Paragraph>()
            .Select(ExtractParagraphText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
    }

    public static IReadOnlyList<string> ExtractMergeFields(string body) =>
        MergeFieldPattern.Matches(body ?? string.Empty)
            .Select(match => match.Groups[1].Value)
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string ExtractParagraphText(Paragraph paragraph)
    {
        var parts = new List<string>();

        foreach (var element in paragraph.Descendants())
        {
            switch (element)
            {
                case Text text:
                    parts.Add(text.Text);
                    break;
                case TabChar:
                    parts.Add("\t");
                    break;
                case Break:
                    parts.Add(Environment.NewLine);
                    break;
            }
        }

        return string.Concat(parts).TrimEnd();
    }
}
