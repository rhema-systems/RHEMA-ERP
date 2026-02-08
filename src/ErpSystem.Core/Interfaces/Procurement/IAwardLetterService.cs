namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Service interface for generating award letter PDFs
/// </summary>
public interface IAwardLetterService
{
    /// <summary>
    /// Generates a PDF award letter for the specified award
    /// </summary>
    /// <param name="awardId">The ID of the tender award</param>
    /// <returns>A tuple containing the PDF bytes and the filename</returns>
    Task<(byte[] PdfBytes, string FileName)> GenerateAwardLetterAsync(Guid awardId);
}
