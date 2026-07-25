using ErpSystem.Core.DTOs.Documents;

namespace ErpSystem.Core.Interfaces.Documents;

public interface IDocumentOutputService
{
    Task<RenderedDocumentDto> RenderAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken = default);
}

public interface IDocumentBuilder
{
    string DocumentType { get; }
    bool RequiresEntityId { get; }
    bool SupportsFormat(string format);
    Task<RenderedDocumentDto> RenderAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken = default);
}
