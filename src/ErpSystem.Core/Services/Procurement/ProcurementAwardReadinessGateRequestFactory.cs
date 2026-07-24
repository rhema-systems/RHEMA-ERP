using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

internal static class ProcurementAwardReadinessGateRequestFactory
{
    public static EvaluateProcurementAwardReadinessRequest Create(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        string correlationId,
        IEnumerable<Guid>? expectedRecommendedSubjectIds = null,
        IEnumerable<Guid>? expectedBusinessPartnerIds = null)
    {
        var correlationHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(correlationId)))[..16];
        return new EvaluateProcurementAwardReadinessRequest
        {
            IdempotencyKey =
                $"award-gate:{(int)sourceType}:{sourceId:N}:{correlationHash}",
            ExpectedRecommendedSubjectIds = expectedRecommendedSubjectIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Order()
                .ToList() ?? [],
            ExpectedBusinessPartnerIds = expectedBusinessPartnerIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Order()
                .ToList() ?? []
        };
    }
}
