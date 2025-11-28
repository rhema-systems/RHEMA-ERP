using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Data
{
    /// <summary>
    /// Specialized DbContext for reporting and complex queries where global filters 
    /// (TenantId, SoftDelete) should be disabled to avoid EF Core translation issues.
    /// </summary>
    public class ReportingDbContext : ApplicationDbContext
    {

        public ReportingDbContext(DbContextOptions<ReportingDbContext> options, IServiceProvider serviceProvider) 
            : base(options, serviceProvider)
        {
        }

        protected override void ApplyGlobalFilters(ModelBuilder builder)
        {
            // Override with empty body to disable all global filters
            // This allows complex queries to run without EF Core translation errors
            // caused by the global filter expressions.
        }
    }
}
