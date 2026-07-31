using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Owns the missing AP-006/TDC-0507 exception lifecycle while composing the
/// existing vendor-invoice matcher, shared workflow/evidence and audit stack.
/// This service never allocates or posts supplier payments.
/// </summary>
public interface IVendorInvoiceMatchExceptionService
{
    Task<VendorInvoiceMatchExceptionOverviewDto> GetOverviewAsync(
        Guid vendorInvoiceId,
        CancellationToken cancellationToken = default);

    Task<VendorInvoiceMatchExceptionDto> RequestAsync(
        Guid vendorInvoiceId,
        CreateVendorInvoiceMatchExceptionDto request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<VendorInvoiceMatchExceptionDto> DecideAsync(
        Guid exceptionId,
        DecideVendorInvoiceMatchExceptionDto request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<VendorInvoiceMatchExceptionDto> CancelAsync(
        Guid exceptionId,
        CancelVendorInvoiceMatchExceptionDto request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<VendorInvoiceMatchExceptionDto> CompleteCorrectiveActionAsync(
        Guid exceptionId,
        CompleteVendorInvoiceMatchCorrectiveActionDto request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
