namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>Tenant policy for procurement actor separation; never substitutes for workflow or permission checks.</summary>
public interface IProcurementSodPolicy
{
    Task<bool> IsEnabledAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> IsRequiredForSourceAsync(Guid tenantId, string? sourceType, Guid? sourceId,
        CancellationToken cancellationToken = default);
}
