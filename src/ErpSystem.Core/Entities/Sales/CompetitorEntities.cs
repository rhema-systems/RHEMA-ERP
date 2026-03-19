using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Sales
{
    /// <summary>
    /// Competitor profile — company-level intelligence
    /// </summary>
    public class Competitor : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Website { get; set; }

        [MaxLength(200)]
        public string? Industry { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? Strengths { get; set; }

        [MaxLength(2000)]
        public string? Weaknesses { get; set; }

        [MaxLength(2000)]
        public string? KeyProducts { get; set; }

        [MaxLength(500)]
        public string? PricingStrategy { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedMarketShare { get; set; }

        public CompetitorThreatLevel ThreatLevel { get; set; } = CompetitorThreatLevel.Medium;
        public bool IsActive { get; set; } = true;

        public ICollection<CompetitorDeal> Deals { get; set; } = new List<CompetitorDeal>();
    }

    /// <summary>
    /// Competitor presence on a specific opportunity/deal
    /// </summary>
    public class CompetitorDeal : BaseEntity
    {
        public Guid CompetitorId { get; set; }
        public Competitor Competitor { get; set; } = null!;

        public Guid? OpportunityId { get; set; }

        [MaxLength(200)]
        public string? OpportunityName { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }

        public CompetitorThreatLevel ThreatLevel { get; set; } = CompetitorThreatLevel.Medium;
        public CompetitorDealOutcome Outcome { get; set; } = CompetitorDealOutcome.InProgress;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DealValue { get; set; }

        [MaxLength(1000)]
        public string? CompetitorProposal { get; set; }

        [MaxLength(1000)]
        public string? OurDifferentiator { get; set; }

        [MaxLength(1000)]
        public string? LessonsLearned { get; set; }

        public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedDate { get; set; }
    }
}
