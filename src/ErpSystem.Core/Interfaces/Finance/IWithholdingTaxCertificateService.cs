using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IWithholdingTaxCertificateService
{
    Task<PagedResult<WhtCertificateDto>> GetApCertificatesAsync(
        WhtCertificateQueryDto query,
        CancellationToken cancellationToken = default);

    Task<WhtCertificateDto?> GetApCertificateAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default);

    Task<WhtCertificateDto> GenerateApCertificateAsync(
        Guid vendorPaymentId,
        GenerateWhtCertificateDto dto,
        CancellationToken cancellationToken = default);

    Task<string> GetApCertificateHtmlAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default);
}
