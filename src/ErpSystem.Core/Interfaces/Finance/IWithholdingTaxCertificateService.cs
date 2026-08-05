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

    Task<WhtCertificateDto> ReissueApCertificateAsync(
        Guid vendorPaymentId,
        ReissueWhtCertificateDto dto,
        CancellationToken cancellationToken = default);

    Task<WhtCertificateDto> CancelApCertificateAsync(
        Guid vendorPaymentId,
        CancelWhtCertificateDto dto,
        CancellationToken cancellationToken = default);

    Task<string> GetApCertificateHtmlAsync(
        Guid vendorPaymentId,
        Guid? certificateId = null,
        CancellationToken cancellationToken = default);

    Task<WhtCalculationResultDto> CalculateApWithholdingAsync(
        WhtCalculationRequestDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhtRemittanceLiabilityDto>> GetUnremittedLiabilitiesAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string? currencyCode,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WhtRemittanceDto>> GetRemittancesAsync(
        WhtRemittanceQueryDto query,
        CancellationToken cancellationToken = default);

    Task<WhtRemittanceDto?> GetRemittanceAsync(
        Guid remittanceId,
        CancellationToken cancellationToken = default);

    Task<WhtRemittanceDto> CreateRemittanceAsync(
        CreateWhtRemittanceDto dto,
        CancellationToken cancellationToken = default);

    Task<WhtRemittanceDto> SubmitRemittanceAsync(
        Guid remittanceId,
        SubmitWhtRemittanceDto dto,
        CancellationToken cancellationToken = default);

    Task<WhtRemittanceDto> MarkRemittancePaidAsync(
        Guid remittanceId,
        PayWhtRemittanceDto dto,
        CancellationToken cancellationToken = default);

    Task<WhtRemittanceDto> CancelRemittanceAsync(
        Guid remittanceId,
        CancelWhtRemittanceDto dto,
        CancellationToken cancellationToken = default);

    Task<WhtRegisterExportDto> ExportRegisterAsync(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);
}
