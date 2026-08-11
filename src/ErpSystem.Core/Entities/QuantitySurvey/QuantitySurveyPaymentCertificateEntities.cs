using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Entities.QuantitySurvey;

[Table("QuantitySurveyPaymentCertificateRevisions")]
public sealed class QuantitySurveyPaymentCertificateRevision : TenantEntity
{
    public Guid PaymentCertificateId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string AfterJson { get; set; } = string.Empty;

    public ProjectPaymentCertificate PaymentCertificate { get; set; } = null!;
}
