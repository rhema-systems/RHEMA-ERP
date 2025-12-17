using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities
{
    public class DataSource : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public int Type { get; set; } // DataSourceType enum as int

        [MaxLength(100)]
        public string? Host { get; set; }

        public int? Port { get; set; }

        [MaxLength(200)]
        public string? DatabaseName { get; set; }

        [MaxLength(200)]
        public string? Username { get; set; }

        [MaxLength(1000)]
        public string? EncryptedPassword { get; set; } // Store encrypted password

        [Column(TypeName = "nvarchar(max)")]
        public string? AdditionalSettings { get; set; } // JSON for extra settings

        public bool IsActive { get; set; } = true;

        public DateTime? LastConnectionTest { get; set; }
        public bool? LastConnectionSuccess { get; set; }

        [MaxLength(1000)]
        public string? LastConnectionError { get; set; }

        public DateTime? LastUsed { get; set; }
        public int UsageCount { get; set; } = 0;

        [Required]
        public Guid TenantId { get; set; }

        [Required]
        public Guid CreatedByUserId { get; set; }

        // Navigation properties
        public virtual ApplicationUser CreatedByUser { get; set; } = null!;
        public virtual List<DataSourceUsageLog> UsageLogs { get; set; } = new();

        // Computed property for DataSourceType enum
        [NotMapped]
        public DataSourceType DataSourceType
        {
            get => (DataSourceType)Type;
            set => Type = (int)value;
        }
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
