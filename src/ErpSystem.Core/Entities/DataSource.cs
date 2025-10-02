using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities
{
    public class DataSource : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public DataSourceType Type { get; set; }

        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Host { get; set; }

        public int? Port { get; set; }

        [StringLength(100)]
        public string? DatabaseName { get; set; }

        [StringLength(100)]
        public string? Username { get; set; }

        [StringLength(500)]
        public string? EncryptedPassword { get; set; }

        public Dictionary<string, object>? AdditionalSettings { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastConnectionTest { get; set; }

        public bool? LastConnectionSuccess { get; set; }

        public string? LastConnectionError { get; set; }

        [Required]
        public Guid TenantId { get; set; }

        [Required]
        public Guid CreatedByUserId { get; set; }

        public DateTime? LastUsed { get; set; }

        public int UsageCount { get; set; }

        // Navigation properties
        public virtual ApplicationUser CreatedByUser { get; set; } = null!;
    }

    public enum DataSourceType
    {
        SqlServer = 1,
        MySql = 2,
        PostgreSQL = 3,
        Oracle = 4,
        SQLite = 5,
        MongoDB = 6,
        RestApi = 7,
        GraphQL = 8,
        OData = 9,
        Excel = 10,
        Csv = 11,
        Json = 12,
        Xml = 13,
        Redis = 14,
        ElasticSearch = 15,
        Azure = 16,
        AWS = 17,
        GoogleCloud = 18
    }
}