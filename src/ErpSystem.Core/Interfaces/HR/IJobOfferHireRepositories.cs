using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB OFFER
// ============================================================================

#region Job Offer

public interface IJobOfferRepository : IGenericRepository<JobOffer>
{
    /// <summary>Returns the offer matching the unique offer number.</summary>
    Task<JobOffer?> GetByOfferNumberAsync(string offerNumber);

    /// <summary>Returns a fully-loaded offer including application, position, location, and preparer/approver details.</summary>
    Task<JobOffer?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns all offers for a specific application (to handle versioned counter-offers).</summary>
    Task<IEnumerable<JobOffer>> GetByApplicationIdAsync(Guid applicationId);

    /// <summary>Returns offers filtered by status.</summary>
    Task<IEnumerable<JobOffer>> GetByStatusAsync(JobOfferStatus status);

    /// <summary>Returns offers prepared by a specific employee.</summary>
    Task<IEnumerable<JobOffer>> GetByPreparedByAsync(Guid employeeId);

    /// <summary>Returns offers whose expiry date is approaching within the given number of days.</summary>
    Task<IEnumerable<JobOffer>> GetExpiringOffersAsync(int daysAhead = 3);

    /// <summary>Returns the next offer number for auto-generation.</summary>
    Task<string> GetNextOfferNumberAsync();

    /// <summary>Returns all offers with Application and JobCandidate loaded for list/summary views.</summary>
    Task<IEnumerable<JobOffer>> GetAllForSummaryAsync();
}

#endregion

// ============================================================================
// JOB OFFER BENEFIT
// ============================================================================

#region Job Offer Benefit

public interface IJobOfferBenefitRepository : IGenericRepository<JobOfferBenefit>
{
    /// <summary>Returns all benefit lines for an offer, ordered by DisplayOrder.</summary>
    Task<IEnumerable<JobOfferBenefit>> GetByOfferIdAsync(Guid offerId);
}

#endregion

// ============================================================================
// JOB OFFER NOTE
// ============================================================================

#region Job Offer Note

public interface IJobOfferNoteRepository : IGenericRepository<JobOfferNote>
{
    /// <summary>Returns all notes for an offer, newest first.</summary>
    Task<IEnumerable<JobOfferNote>> GetByOfferIdAsync(Guid offerId);
}

#endregion

// ============================================================================
// JOB HIRE RECORD
// ============================================================================

#region Job Hire Record

public interface IJobHireRecordRepository : IGenericRepository<JobHireRecord>
{
    /// <summary>Returns the hire record matching the unique hire number.</summary>
    Task<JobHireRecord?> GetByHireNumberAsync(string hireNumber);

    /// <summary>Returns the hire record linked to a specific application.</summary>
    Task<JobHireRecord?> GetByApplicationIdAsync(Guid applicationId);

    /// <summary>Returns hire records filtered by status.</summary>
    Task<IEnumerable<JobHireRecord>> GetByStatusAsync(JobHireStatus status);

    /// <summary>Returns the hire record for an employee once onboarding is confirmed.</summary>
    Task<JobHireRecord?> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns hire records whose expected start date falls within the given number of days.</summary>
    Task<IEnumerable<JobHireRecord>> GetWithStartDateApproachingAsync(int daysAhead = 14);

    /// <summary>Returns the next hire number for auto-generation.</summary>
    Task<string> GetNextHireNumberAsync();

    /// <summary>
    /// Returns the hire record with all data needed by <c>ConfirmStartAsync</c>:
    /// Offer (with Position), Application, Application.JobCandidate.
    /// </summary>
    Task<JobHireRecord?> GetForConfirmStartAsync(Guid id);
}

#endregion
