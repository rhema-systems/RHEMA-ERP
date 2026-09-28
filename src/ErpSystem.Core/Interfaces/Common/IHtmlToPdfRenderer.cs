namespace ErpSystem.Core.Interfaces.Common;

/// <summary>
/// Turns a rendered HTML document — a letter HR produces — into a PDF, for an email attachment the
/// recipient can keep, print and forward (round 4, lane N-b: the offer letter on the Offer Issued email).
/// Implemented in the API project on Syncfusion's document engine, which the document-management
/// renditions already use.
/// </summary>
/// <remarks>
/// <para>Returns <c>null</c> — never throws — when a document cannot be converted. Every caller can go on
/// without the PDF (the email still goes, and says nothing about an attachment; the letter is still on
/// the portal), and a letter that cannot be attached must never stop the offer it belongs to.</para>
///
/// <para>⚠ Without <c>Syncfusion:LicenseKey</c> in configuration the engine stamps a trial notice on
/// every page. Development and UAT run without one; a production server needs the key.</para>
/// </remarks>
public interface IHtmlToPdfRenderer
{
    Task<byte[]?> RenderAsync(string html, string title, CancellationToken cancellationToken = default);
}
