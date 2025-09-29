using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

[Table("EmailTemplates")]
public class EmailTemplate : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TableName { get; set; }

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string HtmlBody { get; set; } = string.Empty;

    public string? PlainTextBody { get; set; }

    /// <summary>
    /// JSON array of selected database fields for placeholders
    /// Example: ["Users.FirstName", "Users.Email", "Orders.OrderNumber"]
    /// </summary>
    public string? SelectedFields { get; set; }

    /// <summary>
    /// JSON object containing template variables and their descriptions
    /// Example: {"{{User.FirstName}}": "User's first name", "{{Order.Total}}": "Order total amount"}
    /// </summary>
    public string? TemplateVariables { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Template category for organization (e.g., "Notifications", "Reports", "Confirmations")
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }
}