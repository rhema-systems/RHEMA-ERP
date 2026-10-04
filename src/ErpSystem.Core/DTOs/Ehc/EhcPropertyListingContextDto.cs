namespace ErpSystem.Core.DTOs.Ehc;

/// <summary>A server-verified snapshot of the property and requester at submission time.</summary>
public sealed record EhcPropertyListingContextDto(
    string Source,
    Guid ListingId,
    string ListingReference,
    string ListingName,
    string ListingType,
    string Currency,
    string? Location,
    decimal? Price,
    Guid ParentAssetId,
    Guid? DemarcationId,
    Guid? BusinessPartnerId,
    string BusinessPartnerName,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? ContactReference = null,
    string? AlternativePhoneNumber = null,
    string? PreferredContactMethod = null,
    Guid? PublicContactId = null)
{
    public string? AssetType { get; init; }
}
