using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 7: COMPLIANCE & SAFETY
// ============================================================================

#region Staff Travel Document

public interface IStaffTravelDocumentRepository : IGenericRepository<StaffTravelDocument>
{
    /// <summary>The document with its employee, issuing country and verifier, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelDocument?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all travel documents for the tenant, with traveller and issuing country loaded.</summary>
    Task<IEnumerable<StaffTravelDocument>> GetAllWithDetailsAsync();

    /// <summary>Returns all travel documents held by an employee, with issuing country loaded.</summary>
    Task<IEnumerable<StaffTravelDocument>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns an employee's documents of a given type.</summary>
    Task<IEnumerable<StaffTravelDocument>> GetByTypeAsync(Guid employeeId, TravelDocumentType documentType);

    /// <summary>Returns the primary document of a given type for an employee, if one exists.</summary>
    Task<StaffTravelDocument?> GetPrimaryDocumentAsync(Guid employeeId, TravelDocumentType documentType);

    /// <summary>Returns documents expiring within the given window.</summary>
    Task<IEnumerable<StaffTravelDocument>> GetExpiringDocumentsAsync(int daysAhead = 90);
}

#endregion

#region Staff Travel Visa Requirement

public interface IStaffTravelVisaRequirementRepository : IGenericRepository<StaffTravelVisaRequirement>
{
    /// <summary>Returns the visa requirement for a passport country travelling to a destination country.</summary>
    /// <summary>Lane 7 (E4): scoped to the tenant — the pair was matched across tenants, so the duplicate guard and the
    /// read-back could pick another tenant's row.</summary>
    Task<StaffTravelVisaRequirement?> GetRequirementAsync(Guid tenantId, Guid passportCountryId, Guid destinationCountryId);

    /// <summary>Returns all visa requirements defined for a destination country.</summary>
    Task<IEnumerable<StaffTravelVisaRequirement>> GetByDestinationAsync(Guid destinationCountryId);
}

#endregion

#region Staff Travel Visa Application

public interface IStaffTravelVisaApplicationRepository : IGenericRepository<StaffTravelVisaApplication>
{
    /// <summary>The visa application with its employee, destination and vendor, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelVisaApplication?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all visa applications for the tenant, newest-first, with traveller and destination loaded.</summary>
    Task<IEnumerable<StaffTravelVisaApplication>> GetAllWithDetailsAsync();

    /// <summary>Returns all visa applications for a travel request.</summary>
    Task<IEnumerable<StaffTravelVisaApplication>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns all visa applications for an employee, newest-first.</summary>
    Task<IEnumerable<StaffTravelVisaApplication>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns visa applications filtered by status.</summary>
    Task<IEnumerable<StaffTravelVisaApplication>> GetByStatusAsync(VisaApplicationStatus status);

    /// <summary>Returns approved visas expiring within the given window.</summary>
    Task<IEnumerable<StaffTravelVisaApplication>> GetExpiringVisasAsync(int daysAhead = 90);
}

#endregion

#region Staff Travel Risk Assessment

public interface IStaffTravelRiskAssessmentRepository : IGenericRepository<StaffTravelRiskAssessment>
{
    /// <summary>The risk assessment with its assessor and destination, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelRiskAssessment?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all risk assessments for a travel request, newest-first.</summary>
    Task<IEnumerable<StaffTravelRiskAssessment>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns risk assessments for a destination country.</summary>
    Task<IEnumerable<StaffTravelRiskAssessment>> GetByCountryAsync(Guid countryId);

    /// <summary>Returns the most recent risk assessment for a request.</summary>
    Task<StaffTravelRiskAssessment?> GetCurrentForRequestAsync(Guid requestId);

    /// <summary>Returns risk assessments that still require employee acknowledgement.</summary>
    Task<IEnumerable<StaffTravelRiskAssessment>> GetRequiringAcknowledgementAsync();
}

#endregion

#region Staff Travel Alert

public interface IStaffTravelAlertRepository : IGenericRepository<StaffTravelAlert>
{
    /// <summary>The alert with its country, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelAlert?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all active alerts.</summary>
    Task<IEnumerable<StaffTravelAlert>> GetActiveAlertsAsync();

    /// <summary>Returns alerts for a country.</summary>
    Task<IEnumerable<StaffTravelAlert>> GetByCountryAsync(Guid countryId);

    /// <summary>Returns active alerts for a country that are within their effective window right now.</summary>
    Task<IEnumerable<StaffTravelAlert>> GetCurrentAlertsForCountryAsync(Guid countryId);

    /// <summary>Returns an alert with its notifications loaded.</summary>
    Task<StaffTravelAlert?> GetWithNotificationsAsync(Guid id);
}

#endregion

#region Staff Travel Alert Notification

public interface IStaffTravelAlertNotificationRepository : IGenericRepository<StaffTravelAlertNotification>
{
    /// <summary>Returns all notifications raised for an alert.</summary>
    Task<IEnumerable<StaffTravelAlertNotification>> GetByAlertIdAsync(Guid alertId);

    /// <summary>Returns all notifications relating to a travel request.</summary>
    Task<IEnumerable<StaffTravelAlertNotification>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns all notifications addressed to an employee.</summary>
    Task<IEnumerable<StaffTravelAlertNotification>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>One notification with its alert, its trip and its employee loaded.</summary>
    Task<StaffTravelAlertNotification?> GetByIdWithDetailsAsync(Guid id);

    /// <summary>Returns notifications for an employee that have not yet been acknowledged.</summary>
    Task<IEnumerable<StaffTravelAlertNotification>> GetUnacknowledgedAsync(Guid employeeId);
}

#endregion

#region Staff Travel Insurance Policy

public interface IStaffTravelInsurancePolicyRepository : IGenericRepository<StaffTravelInsurancePolicy>
{
    /// <summary>The insurance policy with its vendor, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelInsurancePolicy?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all insurance policies for a travel request, with vendor loaded.</summary>
    Task<IEnumerable<StaffTravelInsurancePolicy>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns insurance policies underwritten by the given vendor.</summary>
    Task<IEnumerable<StaffTravelInsurancePolicy>> GetByVendorIdAsync(Guid vendorId);

    /// <summary>Returns policies whose coverage window includes the given date.</summary>
    Task<IEnumerable<StaffTravelInsurancePolicy>> GetActiveByDateAsync(DateOnly onDate);
}

#endregion

#region Staff Travel Health Requirement

public interface IStaffTravelHealthRequirementRepository : IGenericRepository<StaffTravelHealthRequirement>
{
    /// <summary>The health requirement with its country, tenant-scoped — for reloading a write (F-12) and by-id reads (F-13).</summary>
    Task<StaffTravelHealthRequirement?> GetWithDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns all health requirements defined for a country.</summary>
    Task<IEnumerable<StaffTravelHealthRequirement>> GetByCountryAsync(Guid countryId);

    /// <summary>Returns the mandatory, active health requirements for a country.</summary>
    Task<IEnumerable<StaffTravelHealthRequirement>> GetMandatoryByCountryAsync(Guid countryId);

    /// <summary>Returns all active health requirements.</summary>
    Task<IEnumerable<StaffTravelHealthRequirement>> GetActiveAsync();
}

#endregion
