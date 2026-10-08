using System.Text.Json.Serialization;

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
    public Guid? IdentificationTypeId { get; init; }
    public string? IdentificationTypeName { get; init; }
    public string? MaskedIdentificationNumber { get; init; }

    /// <summary>
    /// Transient input copied into the ticket's protected column. It is deliberately excluded
    /// from the immutable display snapshot so the full value is not duplicated in JSON.
    /// </summary>
    [JsonIgnore]
    public string? IdentificationNumber { get; init; }
}
