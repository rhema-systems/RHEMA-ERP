using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

#region Business Partner User Management DTOs

/// <summary>
/// DTO for business partner user summary
/// </summary>
public class BusinessPartnerUserDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public DateTime GrantedAt { get; set; }
    public Guid? GrantedById { get; set; }
    public string? GrantedByName { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating a new business partner user
/// </summary>
public class CreateBusinessPartnerUserDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required]
    [StringLength(50)]
    public string Role { get; set; } = "User"; // Admin, User, Viewer

    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating business partner user
/// </summary>
public class UpdateBusinessPartnerUserDto
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required]
    [StringLength(50)]
    public string Role { get; set; } = "User";

    public bool IsActive { get; set; }

    public string? Notes { get; set; }
}

#endregion

#region Tender Assignment DTOs

/// <summary>
/// DTO for tender assignment
/// </summary>
public class TenderAssignmentDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string AssignmentType { get; set; } = "Self";
    public DateTime AssignedAt { get; set; }
    public Guid AssignedById { get; set; }
    public string AssignedByName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating tender assignment
/// </summary>
public class CreateTenderAssignmentDto
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [StringLength(50)]
    public string AssignmentType { get; set; } = "Self"; // AllUsers, Self, SelectedUsers

    /// <summary>
    /// Required when AssignmentType is "SelectedUsers"
    /// </summary>
    public List<Guid>? AssignedUserIds { get; set; }

    public string? Notes { get; set; }
}

#endregion

