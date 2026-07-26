using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Interfaces.Documents;

namespace ErpSystem.Api.Services.Documents;

public sealed class DocumentOutputService : IDocumentOutputService
{
    private readonly IReadOnlyDictionary<string, IDocumentBuilder> _builders;

    public DocumentOutputService(IEnumerable<IDocumentBuilder> builders)
    {
        _builders = builders.ToDictionary(builder => builder.DocumentType, StringComparer.OrdinalIgnoreCase);
    }

    public Task<RenderedDocumentDto> RenderAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            throw new InvalidOperationException("A document type is required.");
        }

        var format = string.IsNullOrWhiteSpace(request.Format) ? "pdf" : request.Format.Trim().ToLowerInvariant();
        if (!_builders.TryGetValue(request.DocumentType, out var builder))
        {
            throw new NotSupportedException($"Document type '{request.DocumentType}' is not registered.");
        }

        if (!builder.SupportsFormat(format))
        {
            throw new NotSupportedException($"Document type '{request.DocumentType}' does not support '{format}'.");
        }

        if (builder.RequiresEntityId && request.EntityId == Guid.Empty)
        {
            throw new InvalidOperationException("A document entity id is required.");
        }

        request.Format = format;
        request.CopyType = string.IsNullOrWhiteSpace(request.CopyType) ? "Original" : request.CopyType.Trim();

        return builder.RenderAsync(request, cancellationToken);
    }
}
