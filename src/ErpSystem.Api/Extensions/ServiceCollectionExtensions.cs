using System.Text;
using System.Threading.RateLimiting;
using ErpSystem.Api.Authorization;
using ErpSystem.Api.HealthChecks;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Extensions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using ErpSystem.Web.Configuration;
using ErpSystem.Web.HealthChecks;
using ErpSystem.Web.Middleware;
using ErpSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using static ErpSystem.Core.Services.StorageServiceExtensions;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data.Repositories.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.Procurement;

namespace ErpSystem.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures database provider based on appsettings.json configuration.
        /// Uses the new DatabaseConfiguration.AddConfigurableDatabase method.
        /// Change Database:Provider in appsettings.json to switch databases.
        /// </summary>
        public static IServiceCollection AddErpSystemDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            // Use the new configurable database provider
            return services.AddConfigurableDatabase(configuration);

            // OLD METHOD (commented out - kept for reference):
            /*
            var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("ErpSystem.Data");
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }));
            */
        }

        /// <summary>
        /// Registers the database for the guarded migration/seed CLI only: one bounded command attempt,
        /// no provider execution-strategy retries, and no change to the normal web runtime profile.
        /// </summary>
        internal static IServiceCollection AddErpSystemCliDatabase(
            this IServiceCollection services,
            IConfiguration configuration,
            int commandTimeoutSeconds)
        {
            return services.AddConfigurableDatabase(
                configuration,
                DatabaseExecutionProfile.MigrationCli(commandTimeoutSeconds));
        }

        public static IServiceCollection AddErpSystemIdentity(this IServiceCollection services)
        {
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Password settings
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredUniqueChars = 1;

                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                options.User.RequireUniqueEmail = true;

                // Sign in settings
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            return services;
        }

        public static IServiceCollection AddErpSystemJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("JwtSettings");
            var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not found"));

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false; // Set to true in production
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                x.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var requestPath = context.HttpContext.Request.Path;

                        if (string.IsNullOrWhiteSpace(context.Token)
                            && !string.IsNullOrWhiteSpace(accessToken)
                            && requestPath.StartsWithSegments("/api/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
            // The named PortalBearer handler (distinct signing key + audience for the bespoke
            // candidate/consultant portals) was retired 2026-08-31 with its last tenant, the
            // consultant-client portal. Every external principal now holds a main-scheme token
            // whose role (Candidate, ConsultantClient, ExternalUser) is fenced by the dedicated
            // access middlewares and named in the InternalOnly blocklist.

            // Register JWT service
            services.AddScoped<IJwtTokenService, JwtTokenService>();

            // Bind the external-portal URL options. Without this, IOptions<CandidatePortalOptions>.Value
            // .PortalUrl is empty and the candidate/consultant portal auth services build relative
            // verification / password-reset links (e.g. "/careers/verify-email?...") that recipients
            // cannot follow. ValidateOnStart makes a missing/empty PortalUrl fail fast at boot rather than
            // shipping broken emails.
            services.AddOptions<ErpSystem.Core.Models.CandidatePortalOptions>()
                .Bind(configuration.GetSection(ErpSystem.Core.Models.CandidatePortalOptions.SectionName))
                .Validate(o => ErpSystem.Core.Models.CandidatePortalOptions.IsValidPortalUrl(o.PortalUrl),
                    "CandidatePortal:PortalUrl must be configured with an absolute portal base URL.")
                .ValidateOnStart();

            return services;
        }

        public static IServiceCollection AddErpSystemRepositories(this IServiceCollection services)
        {
            // Generic repository
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // Specific repositories
            services.AddScoped<ITenantRepository, TenantRepository>();

            // Enquiry, Helpdesk & Complaints (EHC) repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Ehc.IEhcTicketRepository, ErpSystem.Data.Repositories.Ehc.EhcTicketRepository>();

            // Report repositories
            services.AddScoped<IReportRepository, ReportRepository>();
            services.AddScoped<IReportScheduleRepository, ReportScheduleRepository>();
            services.AddScoped<IReportTemplateRepository, ReportTemplateRepository>();
            services.AddScoped<IReportExecutionRepository, ReportExecutionRepository>();
            services.AddScoped<IUserReportFavoriteRepository, UserReportFavoriteRepository>();
            services.AddScoped<IReportExportRepository, ReportExportRepository>();
            services.AddScoped<IReportRoleAssignmentRepository, ReportRoleAssignmentRepository>();
            services.AddScoped<IDocumentNumberingService, DocumentNumberingService>();

            // Data source repositories
            services.AddScoped<IDataSourceRepository, DataSourceRepository>();
            
            #region HR Repositories
            
            // HR repositories using consolidated HR namespace interfaces
            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<ISectionRepository, SectionRepository>();
            services.AddScoped<IEmployeeSkillRepository, EmployeeSkillRepository>();

            // Organization & Location structure repositories
            services.AddScoped<IOrganizationStructureRepository, OrganizationStructureRepository>();
            services.AddScoped<IOrganizationLevelRepository, OrganizationLevelRepository>();
            services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
            services.AddScoped<IOrganizationUnitHistoryRepository, OrganizationUnitHistoryRepository>();
            services.AddScoped<ILocationStructureRepository, LocationStructureRepository>();
            services.AddScoped<ILocationLevelRepository, LocationLevelRepository>();
            services.AddScoped<ILocationRepository, LocationRepository>();
            services.AddScoped<ILocationContactRepository, LocationContactRepository>();

            // Additional HR repositories
            services.AddScoped<IEmployeePositionRepository, EmployeePositionRepository>();

            services.AddScoped<ILeaveRepository, LeaveRepository>();

            // TODO: Additional HR repositories to be implemented as needed:
            // - SkillRepository: needs implementation
            // - CountryRepository: needs implementation
            // - ShiftRepository: needs implementation
            // - AttendanceRecordRepository: needs implementation
            // - WorkStationRepository: needs implementation
            //
            // Current status: Basic HR repositories (Employee, Department, Section) are working
            // Interface conflicts have been resolved by consolidating duplicate interfaces into HR namespace
            //
            // Additional repositories can be implemented following the same pattern:
            // services.AddScoped<ErpSystem.Core.Interfaces.HR.IEmployeePositionRepository, ErpSystem.Data.Repositories.EmployeePositionRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.HR.ISkillRepository, ErpSystem.Data.Repositories.SkillRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.HR.IEmployeeSkillRepository, ErpSystem.Data.Repositories.EmployeeSkillRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.HR.ICountryRepository, ErpSystem.Data.Repositories.CountryRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.HR.IShiftRepository, ErpSystem.Data.Repositories.ShiftRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.HR.IAttendanceRecordRepository, ErpSystem.Data.Repositories.AttendanceRecordRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.HR.IWorkStationRepository, ErpSystem.Data.Repositories.WorkStationRepository>();
            
            #endregion HR Repositories

            // Procurement repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderItemRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderItemRepository>();
            services.AddScoped<ErpSystem.Core.Services.Procurement.PurchaseOrderLandedCostPlanService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderLandedCostPlanRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderLandedCostPlanRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderLandedCostPlanItemRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderLandedCostPlanItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierRepository, ErpSystem.Data.Repositories.Procurement.SupplierRepository>();
            // ⚠ These two were never registered, and SuppliersController takes all three in its
            // constructor — so EVERY endpoint under /api/Suppliers answered 400 INVALID_OPERATION
            // ("Unable to resolve service for type 'ISupplierContactRepository' while attempting to
            // activate 'SuppliersController'") before a line of controller code ran. The whole
            // controller was dead. Both interfaces and both implementations already existed; only
            // the registration was missing. Found 2026-09-14 because HR's pre-employment check
            // providers cannot be registered without a supplier. Recorded in
            // docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md.
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierContactRepository, ErpSystem.Data.Repositories.Procurement.SupplierContactRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierItemCatalogRepository, ErpSystem.Data.Repositories.Procurement.SupplierItemCatalogRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseRequisitionRepository, ErpSystem.Data.Repositories.Procurement.PurchaseRequisitionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseRequisitionItemRepository, ErpSystem.Data.Repositories.Procurement.PurchaseRequisitionItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderReceiptRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderReceiptRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderReceiptItemRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderReceiptItemRepository>();

            // Business Partner / Supplier & Contractor Management repositories - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerCategoryRepository, ErpSystem.Data.Repositories.Procurement.PartnerCategoryRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractorSpecializationRepository, ErpSystem.Data.Repositories.Procurement.ContractorSpecializationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ILicenseTypeRepository, ErpSystem.Data.Repositories.Procurement.LicenseTypeRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerLicenseRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerLicenseRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerContactRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerContactRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerDocumentRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerDocumentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerFinancialRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerFinancialRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerRegistrationRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerRegistrationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerRegistrationDocumentRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerRegistrationDocumentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerRegistrationStatusHistoryRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerRegistrationStatusHistoryRepository>();

            // Performance Tracking repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierPerformanceMetricRepository, ErpSystem.Data.Repositories.Procurement.SupplierPerformanceMetricRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IQualityIncidentRepository, ErpSystem.Data.Repositories.Procurement.QualityIncidentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPerformanceReviewRepository, ErpSystem.Data.Repositories.Procurement.PerformanceReviewRepository>();

            // Blacklist Appeal repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBlacklistAppealRepository, ErpSystem.Data.Repositories.Procurement.BlacklistAppealRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBlacklistHistoryRepository, ErpSystem.Data.Repositories.Procurement.BlacklistHistoryRepository>();

            // Tender Management repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderRepository, ErpSystem.Data.Repositories.Procurement.TenderRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderItemRepository, ErpSystem.Data.Repositories.Procurement.TenderItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderDocumentRepository, ErpSystem.Data.Repositories.Procurement.TenderDocumentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderInvitationRepository, ErpSystem.Data.Repositories.Procurement.TenderInvitationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderBidRepository, ErpSystem.Data.Repositories.Procurement.TenderBidRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderBidItemRepository, ErpSystem.Data.Repositories.Procurement.TenderBidItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderBidDocumentRepository, ErpSystem.Data.Repositories.Procurement.TenderBidDocumentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderFeeRepository, ErpSystem.Data.Repositories.Procurement.TenderFeeRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderPaymentRepository, ErpSystem.Data.Repositories.Procurement.TenderPaymentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderEvaluatorRepository, ErpSystem.Data.Repositories.Procurement.TenderEvaluatorRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderEvaluationRepository, ErpSystem.Data.Repositories.Procurement.TenderEvaluationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderInterviewRepository, ErpSystem.Data.Repositories.Procurement.TenderInterviewRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderClarificationRepository, ErpSystem.Data.Repositories.Procurement.TenderClarificationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderRevisionRepository, ErpSystem.Data.Repositories.Procurement.TenderRevisionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderTemplateRepository, ErpSystem.Data.Repositories.Procurement.TenderTemplateRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderViewLogRepository, ErpSystem.Data.Repositories.Procurement.TenderViewLogRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAwardRepository, ErpSystem.Data.Repositories.Procurement.TenderAwardRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEvaluationCriterionRepository, ErpSystem.Data.Repositories.Procurement.EvaluationCriterionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEvaluationTemplateRepository, ErpSystem.Data.Repositories.Procurement.EvaluationTemplateRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEvaluationTemplateCriterionRepository, ErpSystem.Data.Repositories.Procurement.EvaluationTemplateCriterionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerUserRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerUserRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAssignmentRepository, ErpSystem.Data.Repositories.Procurement.TenderAssignmentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderDocumentTypeRepository, ErpSystem.Data.Repositories.Procurement.TenderDocumentTypeRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderLotRepository, ErpSystem.Data.Repositories.Procurement.TenderLotRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderBidLotRepository, ErpSystem.Data.Repositories.Procurement.TenderBidLotRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPerformanceBondRequestRepository, ErpSystem.Data.Repositories.Procurement.PerformanceBondRequestRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderNegotiationRepository, ErpSystem.Data.Repositories.Procurement.TenderNegotiationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSettingsRepository, ErpSystem.Data.Repositories.Procurement.ProcurementSettingsRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceSettingsRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceSettingsRepository>();

            // RFQ (Request for Quotation) repositories - separate from tenders
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRequestForQuotationRepository, ErpSystem.Data.Repositories.Procurement.RequestForQuotationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRequestForQuotationItemRepository, ErpSystem.Data.Repositories.Procurement.RequestForQuotationItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRequestForQuotationInvitationRepository, ErpSystem.Data.Repositories.Procurement.RequestForQuotationInvitationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRequestForQuotationQuoteRepository, ErpSystem.Data.Repositories.Procurement.RequestForQuotationQuoteRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRequestForQuotationQuoteItemRepository, ErpSystem.Data.Repositories.Procurement.RequestForQuotationQuoteItemRepository>();

            // Procurement Planning repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPlanRepository, ErpSystem.Data.Repositories.Procurement.ProcurementPlanRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPlanItemRepository, ErpSystem.Data.Repositories.Procurement.ProcurementPlanItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPlanItemSupplierRepository, ErpSystem.Data.Repositories.Procurement.ProcurementPlanItemSupplierRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementBudgetRepository, ErpSystem.Data.Repositories.Procurement.ProcurementBudgetRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementBudgetAllocationRepository, ErpSystem.Data.Repositories.Procurement.ProcurementBudgetAllocationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementBudgetRevisionRepository, ErpSystem.Data.Repositories.Procurement.ProcurementBudgetRevisionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementScheduleRepository, ErpSystem.Data.Repositories.Procurement.ProcurementScheduleRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IMarketAnalysisRepository, ErpSystem.Data.Repositories.Procurement.MarketAnalysisRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPriceHistoryRepository, ErpSystem.Data.Repositories.Procurement.PriceHistoryRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierConsolidationRepository, ErpSystem.Data.Repositories.Procurement.SupplierConsolidationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEmergencyProcurementPlanRepository, ErpSystem.Data.Repositories.Procurement.EmergencyProcurementPlanRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEmergencyProcurementItemRepository, ErpSystem.Data.Repositories.Procurement.EmergencyProcurementItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEmergencySupplierRepository, ErpSystem.Data.Repositories.Procurement.EmergencySupplierRepository>();

            // Finance - Common repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IPaymentTermRepository, ErpSystem.Data.Repositories.Finance.PaymentTermRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICurrencyRepository, ErpSystem.Data.Repositories.Finance.CurrencyRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ITaxCalculationEngine, ErpSystem.Api.Services.Finance.Taxation.TaxCalculationEngine>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IInvoiceService, ErpSystem.Api.Services.Finance.AR.InvoiceService>();
            // AR controllers and Finance-owned Estate adapters depend on the interface. Register
            // the extended service explicitly so receipt trace/reversal cannot fail only at request activation.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IPaymentService, ErpSystem.Api.Services.Finance.AR.PaymentService>();
            // Cash/bank now owns controlled source-document reversal and trace endpoints. Register
            // the concrete implementation explicitly so request activation does not depend on the
            // legacy assembly-scanning conventions used by some older Finance services.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICashTransactionService, ErpSystem.Api.Services.Finance.Cash.CashTransactionService>();

            // Award Verification repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IAwardVerificationChecklistTemplateRepository, ErpSystem.Data.Repositories.Procurement.AwardVerificationChecklistTemplateRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IAwardVerificationChecklistItemRepository, ErpSystem.Data.Repositories.Procurement.AwardVerificationChecklistItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAwardVerificationRepository, ErpSystem.Data.Repositories.Procurement.TenderAwardVerificationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAwardVerificationBidderRepository, ErpSystem.Data.Repositories.Procurement.TenderAwardVerificationBidderRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAwardVerificationItemResultRepository, ErpSystem.Data.Repositories.Procurement.TenderAwardVerificationItemResultRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAwardVerificationItemDocumentRepository, ErpSystem.Data.Repositories.Procurement.TenderAwardVerificationItemDocumentRepository>();

            // Contract Management repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractRepository, ErpSystem.Data.Repositories.Procurement.ContractRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractMilestoneRepository, ErpSystem.Data.Repositories.Procurement.ContractMilestoneRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractAmendmentRepository, ErpSystem.Data.Repositories.Procurement.ContractAmendmentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractDocumentRepository, ErpSystem.Data.Repositories.Procurement.ContractDocumentRepository>();

            // Maintenance repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAssetRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceAssetRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAssetCategoryRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceAssetCategoryRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderTypeRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderTypeRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceTypeRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceTypeRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IPriorityLevelRepository, ErpSystem.Data.Repositories.Maintenance.PriorityLevelRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderTaskRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderTaskRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderPartRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderPartRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderLaborRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderLaborRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderDocumentRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderDocumentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderCommentRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderCommentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceScheduleRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceScheduleRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IInspectionTemplateRepository, ErpSystem.Data.Repositories.Maintenance.InspectionTemplateRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetInspectionRepository, ErpSystem.Data.Repositories.Maintenance.AssetInspectionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IInspectionDocumentRepository, ErpSystem.Data.Repositories.Maintenance.InspectionDocumentRepository>();
            // New maintenance module repositories - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicalSkillRepository, ErpSystem.Data.Repositories.Maintenance.TechnicalSkillRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianSkillAssignmentRepository, ErpSystem.Data.Repositories.Maintenance.TechnicianSkillAssignmentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ISafetyProtocolRepository, ErpSystem.Data.Repositories.Maintenance.SafetyProtocolRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ISafetyComplianceRepository, ErpSystem.Data.Repositories.Maintenance.SafetyComplianceRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IProtocolAdherenceRepository, ErpSystem.Data.Repositories.Maintenance.ProtocolAdherenceRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IProtocolViolationRepository, ErpSystem.Data.Repositories.Maintenance.ProtocolViolationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IProtocolTrainingRepository, ErpSystem.Data.Repositories.Maintenance.ProtocolTrainingRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianRepository, ErpSystem.Data.Repositories.Maintenance.TechnicianRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IJobCardRepository, ErpSystem.Data.Repositories.Maintenance.JobCardRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceToolRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceToolRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IToolCheckoutRepository, ErpSystem.Data.Repositories.Maintenance.ToolCheckoutRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderToolRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderToolRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceStaffScheduleRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceStaffScheduleRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceExpenseRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceExpenseRepository>();

            // Project management repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectRepository, ErpSystem.Data.Repositories.Projects.ProjectRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectTypeRepository, ErpSystem.Data.Repositories.Projects.ProjectTypeRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectPriorityRepository, ErpSystem.Data.Repositories.Projects.ProjectPriorityRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectTemplateRepository, ErpSystem.Data.Repositories.Projects.ProjectTemplateRepository>();
services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectPortfolioRepository, ErpSystem.Data.Repositories.Projects.ProjectPortfolioRepository>();
services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectProgramRepository, ErpSystem.Data.Repositories.Projects.ProjectProgramRepository>();
services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectManagementSettingsRepository, ErpSystem.Data.Repositories.Projects.ProjectManagementSettingsRepository>();
services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectCatalogRepository, ErpSystem.Data.Repositories.Projects.ProjectCatalogRepository>();

            // Task template repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetTaskTemplateRepository, ErpSystem.Data.Repositories.Maintenance.AssetTaskTemplateRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetTypeTaskTemplateRepository, ErpSystem.Data.Repositories.Maintenance.AssetTypeTaskTemplateRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceTaskTemplateRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceTaskTemplateRepository>();

            // TODO: Additional maintenance repositories to be implemented as needed:
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianTeamRepository, ErpSystem.Data.Repositories.Maintenance.TechnicianTeamRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianTeamMemberRepository, ErpSystem.Data.Repositories.Maintenance.TechnicianTeamMemberRepository>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetDowntimeRepository, ErpSystem.Data.Repositories.Maintenance.AssetDowntimeRepository>();

            // Technician Scheduling repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianScheduleRepository, ErpSystem.Data.Repositories.Maintenance.TechnicianScheduleRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianAvailabilityRepository, ErpSystem.Data.Repositories.Maintenance.TechnicianAvailabilityRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianShiftRepository, ErpSystem.Data.Repositories.Maintenance.TechnicianShiftRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IEmployeeRepository, ErpSystem.Data.Repositories.Maintenance.EmployeeRepository>();

            // Workflow repositories - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowDefinitionRepository, ErpSystem.Data.Repositories.WorkflowDefinitionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowStepRepository, ErpSystem.Data.Repositories.WorkflowStepRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowTransitionRepository, ErpSystem.Data.Repositories.WorkflowTransitionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowInstanceRepository, ErpSystem.Data.Repositories.WorkflowInstanceRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowStepInstanceRepository, ErpSystem.Data.Repositories.WorkflowStepInstanceRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowApprovalRepository, ErpSystem.Data.Repositories.WorkflowApprovalRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowApprovalPolicySetRepository, ErpSystem.Data.Repositories.WorkflowApprovalPolicySetRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowActivityLogRepository, ErpSystem.Data.Repositories.WorkflowActivityLogRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Repositories.IWorkflowEntityTypeRepository, ErpSystem.Data.Repositories.WorkflowEntityTypeRepository>();

            // Notification repository - for generic notifications
            services.AddScoped<ErpSystem.Core.Interfaces.INotificationRepository, ErpSystem.Data.Repositories.NotificationRepository>();

            // Unit of Work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }

        public static IServiceCollection AddErpSystemServices(this IServiceCollection services)
        {
            // Core services
            services.AddScoped<ErpSystem.Core.Interfaces.IFileUploadService, ErpSystem.Api.Services.SimpleFileUploadService>();

            // SMS (Twilio + mNotify; GhanaGateway remains the persisted compatibility name)
            // used by notifications and OTP flows.
            services.AddOptions<ErpSystem.Api.Services.Sms.SmsOptions>()
                .BindConfiguration("Sms");
            services.AddScoped<ErpSystem.Api.Services.Sms.TwilioSmsSender>();
            services.AddHttpClient<ErpSystem.Api.Services.Sms.GhanaGatewaySmsSender>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ErpSystem.Api.Services.Sms.SmsOptions>>().Value;
                var seconds = Math.Clamp(opts.GhanaGateway.TimeoutSeconds, 1, 60);
                client.Timeout = TimeSpan.FromSeconds(seconds);
            }).RemoveAllLoggers();
            // mNotify requires the API key in the query string. This dedicated client
            // must not use the standard HttpClientFactory request logger, which records
            // request URIs and could therefore disclose the credential.
            services.AddHttpClient("mnotify").RemoveAllLoggers();
            services.AddScoped<ErpSystem.Api.Services.Sms.CompositeSmsSender>();
            services.AddScoped<ErpSystem.Api.Services.Sms.ISmsSender>(sp => sp.GetRequiredService<ErpSystem.Api.Services.Sms.CompositeSmsSender>());
            services.AddScoped<ErpSystem.Api.Services.Sms.ITenantSmsSender, ErpSystem.Api.Services.Sms.TenantSmsSender>();

            // Microsoft 365 inbound email (Graph) - used for email-to-ticket
            services.AddOptions<ErpSystem.Web.Configuration.Microsoft365InboundEmailOptions>()
                .BindConfiguration(ErpSystem.Web.Configuration.Microsoft365InboundEmailOptions.SectionName);
            services.AddHttpClient("MicrosoftGraph", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Unified notification service - comprehensive notification handling for all ERP modules
            services.AddScoped<ErpSystem.Core.Interfaces.INotificationService, ErpSystem.Api.Services.UnifiedNotificationService>();
            services.AddScoped<ErpSystem.Api.Services.Otp.IOtpService, ErpSystem.Api.Services.Otp.OtpService>();
            services.AddScoped<ErpSystem.Core.Interfaces.INotificationTopicPublisher, ErpSystem.Core.Services.Notifications.NotificationTopicPublisher>();
            services.AddScoped<ErpSystem.Core.Interfaces.Events.IAppEventBus, ErpSystem.Core.Services.Events.AppEventBus>();
            services.AddScoped<ErpSystem.Core.Interfaces.Events.IAppEventHandler<ErpSystem.Core.Interfaces.Events.EntityActivityEvent>, ErpSystem.Core.Services.Notifications.EntityActivityNotificationTopicHandler>();
            services.AddScoped<ErpSystem.Core.Interfaces.IWorkflowService, ErpSystem.Api.Services.SimpleWorkflowService>();
            services.AddScoped<ErpSystem.Core.Interfaces.IWorkflowIntegrationService, ErpSystem.Core.Services.Workflow.WorkflowIntegrationService>();
            services.AddScoped<ErpSystem.Core.Services.Workflow.IWorkflowApprovalPolicyResolver, ErpSystem.Core.Services.Workflow.WorkflowApprovalPolicyResolver>();
            services.AddScoped<ErpSystem.Core.Services.Workflow.IWorkflowRuntimeGovernanceService, ErpSystem.Data.Services.WorkflowRuntimeGovernanceService>();
            services.AddScoped<ErpSystem.Core.Services.Workflow.IWorkflowSignatureSubmissionStore, ErpSystem.Data.Services.WorkflowSignatureSubmissionStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowEntityTypeCatalogService, ErpSystem.Core.Services.Workflow.WorkflowEntityTypeCatalogService>();
            services.AddHostedService<ErpSystem.Api.Services.Workflow.WorkflowSlaEscalationBackgroundService>();
            services.AddHostedService<ErpSystem.Api.Services.Workflow.WorkflowIntegrationQueueBackgroundService>();
            services.AddScoped<ErpSystem.Core.Interfaces.IWorkflowStatusAdapterRegistry, ErpSystem.Core.Services.Workflow.WorkflowStatusAdapterRegistry>();
            services.AddWorkflowStatusAdaptersFromAssemblies(typeof(ErpSystem.Core.Services.Workflow.WorkflowStatusAdapterRegistry).Assembly);
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<IUserTenantService, UserTenantService>();
            services.AddScoped<ILdapAuthenticationService, LdapAuthenticationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Identity.IHrIdentityAccessService,
                ErpSystem.Api.Services.Identity.HrIdentityAccessService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Identity.IHrIdentityReconciliationService,
                ErpSystem.Api.Services.Identity.HrIdentityReconciliationService>();
            services.AddHostedService<ErpSystem.Api.Services.Identity.HrIdentityReconciliationBackgroundService>();
            services.AddScoped<ISearchService, SearchService>();

            // Identity services
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ErpSystem.Data.Services.IPermissionService, ErpSystem.Data.Services.PermissionService>();
            services.AddScoped<ErpSystem.Data.Services.IRolePermissionService, ErpSystem.Data.Services.RolePermissionService>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

            // Settings services
            services.AddScoped<ISettingsService, SettingsService>();
            // Estate/DMS integration services: registered centrally so existing modules can call them through explicit handoffs.
            services.AddScoped<ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentManagementService, ErpSystem.Core.Services.DocumentManagement.CentralDocumentManagementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService, ErpSystem.Api.Services.DocumentManagement.CentralDocumentRepositoryFileService>();
            services.AddScoped<ErpSystem.Api.Services.DocumentManagement.ICentralDocumentRenditionService, ErpSystem.Api.Services.DocumentManagement.CentralDocumentRenditionService>();
            services.AddScoped<ErpSystem.Api.Services.DocumentManagement.ICentralDocumentPdfSigningService, ErpSystem.Api.Services.DocumentManagement.CentralDocumentPdfSigningService>();
            services.AddOptions<ErpSystem.Api.Services.DocumentManagement.DocumentSigningOptions>()
                .BindConfiguration(ErpSystem.Api.Services.DocumentManagement.DocumentSigningOptions.SectionName);
            services.AddScoped<ErpSystem.Core.Interfaces.Legal.ILegalProcedureCatalogService, ErpSystem.Core.Services.Legal.LegalProcedureCatalogService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Estate.IFacilitiesProcedureCatalogService, ErpSystem.Core.Services.Estate.FacilitiesProcedureCatalogService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Estate.IPropertyManagementProcedureCatalogService, ErpSystem.Core.Services.Estate.PropertyManagementProcedureCatalogService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Estate.IEstateProcedureCatalogService, ErpSystem.Core.Services.Estate.EstateProcedureCatalogService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Estate.IEstateManagedAssetService, ErpSystem.Core.Services.Estate.EstateManagedAssetService>();
            services.AddScoped<ErpSystem.Api.Services.Estate.IEstateSalesListingApplicationHandoffService, ErpSystem.Api.Services.Estate.EstateSalesListingApplicationHandoffService>();
            services.AddScoped<ErpSystem.Api.Services.Estate.IGroundRentAdministrationService, ErpSystem.Api.Services.Estate.GroundRentAdministrationService>();
            services.AddScoped<ErpSystem.Api.Services.Estate.EstateRecurringBillingService>();
            services.AddScoped<ErpSystem.Api.Services.Estate.FacilitiesLeaseReminderService>();
            services.AddHostedService<ErpSystem.Api.Services.Estate.EstateRecurringBillingBackgroundService>();
            services
                .AddOptions<ErpSystem.Api.Services.Estate.EstateGisNetworkSecurityOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                    configuration
                        .GetSection(ErpSystem.Api.Services.Estate.EstateGisNetworkSecurityOptions.SectionName)
                        .Bind(options));
            services.AddSingleton<ErpSystem.Api.Services.Estate.EstateGisNetworkPolicy>();
            services
                .AddHttpClient<ErpSystem.Api.Services.Estate.IEstateGisIntegrationService, ErpSystem.Api.Services.Estate.EstateGisIntegrationService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("RHEMA-ERP-Estate-GIS/1.0");
                })
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                {
                    var networkPolicy = serviceProvider.GetRequiredService<ErpSystem.Api.Services.Estate.EstateGisNetworkPolicy>();
                    return new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        UseProxy = false,
                        ConnectTimeout = TimeSpan.FromSeconds(10),
                        ConnectCallback = networkPolicy.ConnectHttpAsync
                    };
                });
            services.AddScoped<ErpSystem.Core.Interfaces.Planning.IPlanningProcedureCatalogService, ErpSystem.Core.Services.Planning.PlanningProcedureCatalogService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procedures.IProcedureCaseService, ErpSystem.Api.Services.ProcedureCaseService>();

            // Email template services
            services.AddScoped<IEmailTemplateService, EmailTemplateService>();
            services.AddScoped<IDatabaseMetadataService, DatabaseMetadataService>();
            services.AddScoped<ISampleDataService, SampleDataService>();

            // Logging services
            services.AddScoped<IAuditLogService, AuditLogService>();
            services.AddScoped<ISecurityLogService, SecurityLogService>();

            // Communication services - use ProductionEmailService to actually send emails via SMTP
            // In development, emails will still be logged to console if SMTP settings are not configured
            services.AddScoped<ErpSystem.Web.Services.IEmailService>(serviceProvider =>
            {
                return serviceProvider.GetRequiredService<ProductionEmailService>();
            });

            // Register both email service implementations
            services.AddScoped<SimpleEmailService>();
            services.AddScoped<ProductionEmailService>();
            services.AddScoped<ITenantEmailSender, TenantEmailSender>();

            services.AddScoped<ErpSystem.Core.Interfaces.Common.IEmailService, CoreEmailServiceAdapter>();

            // Security services
            services.AddScoped<ICryptoService, CryptoService>();
            services.AddScoped<IConcurrentLoginService, ConcurrentLoginService>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            services.AddScoped<IJwtBlacklistService, JwtBlacklistService>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            services.AddScoped<IUserSessionService, ErpSystem.Data.Services.UserSessionService>();
            services.AddScoped<ITwoFactorAuthService, TwoFactorAuthService>();
            services.AddScoped<IDeviceSessionService, DeviceSessionService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddScoped<IInternalUserTemporaryPasswordResetService, InternalUserTemporaryPasswordResetService>();

            // Configure HttpClient for geolocation services
            services.AddHttpClient("geolocation", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "ERP-System/1.0");
            });

            // CAPTCHA verification (reCAPTCHA / hCaptcha)
            services.AddHttpClient("captcha", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("User-Agent", "ERP-System/1.0");
            });
            services.AddScoped<ErpSystem.Api.Services.ICaptchaVerificationService, ErpSystem.Api.Services.CaptchaVerificationService>();

            // User↔employee matching rules (login resolver, auto-link, HR unlinked queue)
            services.AddScoped<ErpSystem.Api.Services.IEmployeeLinkResolutionService, ErpSystem.Api.Services.EmployeeLinkResolutionService>();

            services.AddScoped<ISecurityService, SecurityService>();
            services.AddScoped<ErpSystem.Api.Services.ISecurityOperationsService, ErpSystem.Api.Services.SecurityOperationsService>();
            // User context services
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, ErpSystem.Api.Services.CurrentUserService>();
            services.AddScoped<ICurrentUserProvider, ErpSystem.Api.Services.CurrentUserService>();
            services.AddScoped<ITenantContext, ErpSystem.Api.Services.TenantContext>();

            // Dashboard service
            services.AddScoped<ErpSystem.Core.Services.IDashboardService, ErpSystem.Data.Services.DashboardService>();

            // Reports service
            services.AddScoped<ErpSystem.Core.Services.Procurement.ProcurementStatutoryReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementStatutoryReportService>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Procurement.ProcurementStatutoryReportService>());
            services.AddScoped<ErpSystem.Core.Interfaces.ISystemReportProvider>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Procurement.ProcurementStatutoryReportService>());
            services.AddScoped<ErpSystem.Core.Services.Inventory.InventoryStatutoryReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryStatutoryReportService>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Inventory.InventoryStatutoryReportService>());
            services.AddScoped<ErpSystem.Core.Interfaces.ISystemReportProvider>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Inventory.InventoryStatutoryReportService>());
            // FR-HR-113. Unlike its procurement and inventory siblings this provider lives in the
            // API project, because its gate is the ASP.NET AwardsReadPolicy rather than a module
            // access-control service - see the remarks on HrAwardsReportService.
            services.AddScoped<ErpSystem.Api.Services.Reports.HrAwardsReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.ISystemReportProvider>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Reports.HrAwardsReportService>());
            services.AddScoped<ErpSystem.Core.Services.Procurement.AuditComplianceReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IAuditComplianceReportService>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Procurement.AuditComplianceReportService>());
            services.AddScoped<ErpSystem.Core.Interfaces.ISystemReportProvider>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Procurement.AuditComplianceReportService>());
            // Resolve every interface to the same scoped instance. The ad hoc lifecycle and the
            // shared execution/export engine must observe one EF change tracker and user context.
            services.AddScoped<ErpSystem.Api.Services.Finance.Reporting.FinanceAdHocReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceAdHocReportService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.Reporting.FinanceAdHocReportService>());
            services.AddScoped<ErpSystem.Core.Interfaces.ISystemReportProvider>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.Reporting.FinanceAdHocReportService>());
            services.AddScoped<ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyStatutoryReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyStatutoryReportService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyStatutoryReportService>());
            services.AddScoped<ErpSystem.Core.Interfaces.ISystemReportProvider>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyStatutoryReportService>());
            services.AddScoped<ErpSystem.Api.Services.Projects.CivilEngineeringStatutoryReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringStatutoryReportService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Projects.CivilEngineeringStatutoryReportService>());
            services.AddScoped<ErpSystem.Core.Interfaces.ISystemReportProvider>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Projects.CivilEngineeringStatutoryReportService>());
            services.AddScoped<IReportsService, ErpSystem.Data.Services.DatabaseReportsService>();
            services.AddScoped<IReportTemplateLifecycleService, ErpSystem.Data.Services.ReportTemplateLifecycleService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentOutputService, ErpSystem.Api.Services.Documents.DocumentOutputService>();
            // Controlled cash/bank documents reuse the shared renderer but persist their own
            // one-original/many-replacement issue sequence and emitted-byte audit evidence.
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IFinanceControlledDocumentIssueService, ErpSystem.Api.Services.Documents.Finance.FinanceControlledDocumentIssueService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.JournalVoucherDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ApPaymentVoucherDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.CashBankPaymentSlipDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.CustomerReceiptDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ApSupplierStatementDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ApAgingReportDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ApCashRequirementsDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ApMatchExceptionReportDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ApProcurementReconciliationDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ArAgingReportDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ArCustomerStatementDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.CashPositionReportDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.InputTaxRegisterDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.OutputTaxRegisterDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.VatReconciliationDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.WhtPayableReportDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.WhtCertificateRegisterDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.WhtCertificateDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.WhtRemittanceRegisterDocumentBuilder>();
              services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.ConsolidatedBudgetDocumentBuilder>();
              services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.BudgetScenarioComparisonDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.TrialBalanceDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.BaseDeltaReportDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.IncomeStatementDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.BalanceSheetDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.CashFlowStatementDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.MultiCurrencyDetailDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.DetailedLedgerDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Finance.FinanceClosePackDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Inventory.StoreIssueVoucherDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.Inventory.StoreReturnVoucherDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.QuantitySurvey.QuantitySurveyEscalationDisputeAuditPackDocumentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Documents.IDocumentBuilder, ErpSystem.Api.Services.Documents.QuantitySurvey.QuantitySurveyPaymentCertificateDocumentBuilder>();

            // Data source service
            services.AddScoped<IDataSourceService, EnterpriseDataSourceService>();

            // Workflow services - NOW ENABLED for maintenance integration
            services.AddScoped<ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService, ErpSystem.Core.Services.Workflow.WorkflowDefinitionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Services.IWorkflowInstanceService, ErpSystem.Core.Services.Workflow.WorkflowInstanceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Services.IWorkflowStepService, ErpSystem.Core.Services.Workflow.WorkflowStepService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Services.IWorkflowApprovalService, ErpSystem.Core.Services.Workflow.WorkflowApprovalService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Services.IWorkflowActivityService, ErpSystem.Core.Services.Workflow.WorkflowActivityService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Services.IWorkflowEntityTypeService, ErpSystem.Core.Services.Workflow.WorkflowEntityTypeService>();

            // Core workflow engine and supporting services
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowEngine, ErpSystem.Core.Services.Workflow.WorkflowEngine>();

            // Workflow API services (ErpSystem.Core.Interfaces.Workflow namespace - used by WorkflowController)
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService, ErpSystem.Api.Services.Workflow.WorkflowDefinitionServiceAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowInstanceService, ErpSystem.Api.Services.Workflow.WorkflowInstanceServiceAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowStepService, ErpSystem.Api.Services.Workflow.WorkflowStepServiceAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowApprovalService, ErpSystem.Api.Services.Workflow.WorkflowApprovalServiceAdapter>();

            // Workflow supporting services (stub implementations)
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowConditionEvaluator, ErpSystem.Core.Services.Workflow.WorkflowConditionEvaluator>();
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowEntityDisplayService, ErpSystem.Core.Services.Workflow.WorkflowEntityDisplayService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowNotificationService, ErpSystem.Core.Services.Workflow.WorkflowNotificationService>();

            // Enquiry, Helpdesk & Complaints (EHC) services
            services.AddScoped<ErpSystem.Core.Interfaces.Ehc.IEhcTicketService, ErpSystem.Core.Services.Ehc.EhcTicketService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Ehc.IPropertyEnquiryProspectService, ErpSystem.Api.Services.Ehc.PropertyEnquiryProspectService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Ehc.IProspectDepositFinancePostingService, ErpSystem.Api.Services.Ehc.ProspectDepositFinancePostingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Ehc.IPropertyEnquiryDepositApplicationService, ErpSystem.Api.Services.Ehc.PropertyEnquiryDepositApplicationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Ehc.IEhcProblemService, ErpSystem.Core.Services.Ehc.EhcProblemService>();

            // Enhanced maintenance workflow integration - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IEnhancedMaintenanceWorkflowService, ErpSystem.Core.Services.Maintenance.EnhancedMaintenanceWorkflowService>();

            // Maintenance asset service - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAssetService, ErpSystem.Api.Services.Maintenance.MaintenanceAssetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAssetMappingService, ErpSystem.Api.Services.Maintenance.MaintenanceAssetMappingService>();

            // Inventory repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryItemRepository, ErpSystem.Data.Repositories.Inventory.InventoryItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryCategoryRepository, ErpSystem.Data.Repositories.Inventory.InventoryCategoryRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IStockMovementRepository, ErpSystem.Data.Repositories.Inventory.StockMovementRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryLocationRepository, ErpSystem.Data.Repositories.Inventory.InventoryLocationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryAllocationRepository, ErpSystem.Data.Repositories.Inventory.InventoryAllocationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IWarehouseRepository, ErpSystem.Data.Repositories.Inventory.WarehouseRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IWarehouseLocationRepository, ErpSystem.Data.Repositories.Inventory.WarehouseLocationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IWarehouseQuantityRepository, ErpSystem.Data.Repositories.Inventory.WarehouseQuantityRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IStockAdjustmentRepository, ErpSystem.Data.Repositories.Inventory.StockAdjustmentRepository>();

            // Enhanced Inventory repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IUnitOfMeasureRepository, ErpSystem.Data.Repositories.Inventory.UnitOfMeasureRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IUnitOfMeasureConversionRepository, ErpSystem.Data.Repositories.Inventory.UnitOfMeasureConversionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IItemUnitOfMeasureRepository, ErpSystem.Data.Repositories.Inventory.ItemUnitOfMeasureRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IUnitOfMeasureScheduleRepository, ErpSystem.Data.Repositories.Inventory.UnitOfMeasureScheduleRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IUnitOfMeasureScheduleDetailRepository, ErpSystem.Data.Repositories.Inventory.UnitOfMeasureScheduleDetailRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IItemSupplierRepository, ErpSystem.Data.Repositories.Inventory.ItemSupplierRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IGoodsReceiptNoteRepository, ErpSystem.Data.Repositories.Inventory.GoodsReceiptNoteRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IGoodsReceiptNoteItemRepository, ErpSystem.Data.Repositories.Inventory.GoodsReceiptNoteItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryTransferRepository, ErpSystem.Data.Repositories.Inventory.InventoryTransferRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryTransferItemRepository, ErpSystem.Data.Repositories.Inventory.InventoryTransferItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IPhysicalCountRepository, ErpSystem.Data.Repositories.Inventory.PhysicalCountRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IPhysicalCountItemRepository, ErpSystem.Data.Repositories.Inventory.PhysicalCountItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryCostLayerRepository, ErpSystem.Data.Repositories.Inventory.InventoryCostLayerRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.ILandedCostRepository, ErpSystem.Data.Repositories.Inventory.LandedCostRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.ILandedCostItemRepository, ErpSystem.Data.Repositories.Inventory.LandedCostItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.ILandedCostAllocationRepository, ErpSystem.Data.Repositories.Inventory.LandedCostAllocationRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IPurchaseReturnRepository, ErpSystem.Data.Repositories.Inventory.PurchaseReturnRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IPurchaseReturnItemRepository, ErpSystem.Data.Repositories.Inventory.PurchaseReturnItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionRepository, ErpSystem.Data.Repositories.Inventory.InventoryRequisitionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionItemRepository, ErpSystem.Data.Repositories.Inventory.InventoryRequisitionItemRepository>();

            // Inventory Valuation repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryMovementRepository, ErpSystem.Data.Repositories.Inventory.InventoryMovementRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryLayerRepository, ErpSystem.Data.Repositories.Inventory.InventoryLayerRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryBalanceRepository, ErpSystem.Data.Repositories.Inventory.InventoryBalanceRepository>();

            // Inventory services
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryManagementService, ErpSystem.Core.Services.Inventory.InventoryManagementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryItemIdentifierService, ErpSystem.Core.Services.Inventory.InventoryItemIdentifierService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryItemProfileService, ErpSystem.Core.Services.Inventory.InventoryItemProfileService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryScanningService, ErpSystem.Core.Services.Inventory.InventoryScanningService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryTrackingControlService, ErpSystem.Core.Services.Inventory.InventoryTrackingControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryNegativeStockControlService, ErpSystem.Core.Services.Inventory.InventoryNegativeStockControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryNegativeStockMutationStore, ErpSystem.Data.Repositories.Inventory.InventoryNegativeStockMutationStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryProjectReservationService, ErpSystem.Core.Services.Inventory.InventoryProjectReservationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryWorkOrderReservationService, ErpSystem.Core.Services.Inventory.InventoryWorkOrderReservationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryReplenishmentService, ErpSystem.Core.Services.Inventory.InventoryReplenishmentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryDirectedOperationService, ErpSystem.Core.Services.Inventory.InventoryDirectedOperationService>();

            // Enhanced Inventory services
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IGoodsReceiptNoteService, ErpSystem.Core.Services.Inventory.GoodsReceiptNoteService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.ILandedCostService, ErpSystem.Core.Services.Inventory.LandedCostService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IPurchaseReturnService, ErpSystem.Core.Services.Inventory.PurchaseReturnService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryTransferService, ErpSystem.Core.Services.Inventory.InventoryTransferService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IItemSupplierService, ErpSystem.Core.Services.Inventory.ItemSupplierService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IPhysicalCountService, ErpSystem.Core.Services.Inventory.PhysicalCountService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IWarehouseDefaultLocationService, ErpSystem.Core.Services.Inventory.WarehouseDefaultLocationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryIssueFinanceAssetService, ErpSystem.Api.Services.Finance.InventoryIssueFinanceAssetPostingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryReturnControlService, ErpSystem.Core.Services.Inventory.InventoryReturnControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionService, ErpSystem.Core.Services.Inventory.InventoryRequisitionService>();
            services.AddScoped<ErpSystem.Core.Services.Inventory.StockAdjustmentService>();
            services.AddScoped<ErpSystem.Core.Services.Inventory.IStockAdjustmentService>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Inventory.StockAdjustmentService>());
            services.AddScoped<ErpSystem.Api.Services.Inventory.IInventoryDisposalStockAdjustmentParticipant,
                ErpSystem.Api.Services.Inventory.InventoryDisposalStockAdjustmentParticipant>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IStockAdjustmentValuationIntentBuilder, ErpSystem.Api.Services.Finance.StockAdjustmentValuationIntentBuilder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryAdjustmentFinancePostingService, ErpSystem.Api.Services.Finance.InventoryAdjustmentFinancePostingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryReceiptFinancePostingService, ErpSystem.Api.Services.Finance.InventoryReceiptFinancePostingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryLandedCostFinancePostingService, ErpSystem.Api.Services.Finance.InventoryLandedCostFinancePostingService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.InventoryValuationReconciliationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationReconciliationService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.InventoryValuationReconciliationService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationReconciliationReportSource>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.InventoryValuationReconciliationService>());
            services.AddScoped<ErpSystem.Api.Services.Inventory.InventoryAnalyticsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryAnalyticsService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Inventory.InventoryAnalyticsService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryAnalyticsReportSource>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Inventory.InventoryAnalyticsService>());
            services.AddScoped<ErpSystem.Api.Services.Inventory.InventoryDisposalService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryDisposalService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Inventory.InventoryDisposalService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryDisposalReportSource>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Inventory.InventoryDisposalService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService, ErpSystem.Core.Services.Inventory.InventoryValuationService>();

            // Price List repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListRepository, ErpSystem.Data.Repositories.Pricing.PriceListRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListLineRepository, ErpSystem.Data.Repositories.Pricing.PriceListLineRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.ICustomerGroupRepository, ErpSystem.Data.Repositories.Pricing.CustomerGroupRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.ISupplierGroupRepository, ErpSystem.Data.Repositories.Pricing.SupplierGroupRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListChangeHistoryRepository, ErpSystem.Data.Repositories.Pricing.PriceListChangeHistoryRepository>();

            // Price List services
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListService, ErpSystem.Core.Services.Pricing.PriceListService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListLineService, ErpSystem.Core.Services.Pricing.PriceListLineService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.ICustomerGroupService, ErpSystem.Core.Services.Pricing.CustomerGroupService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.ISupplierGroupService, ErpSystem.Core.Services.Pricing.SupplierGroupService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListChangeHistoryService, ErpSystem.Core.Services.Pricing.PriceListChangeHistoryService>();

            // Finance - Common services (Payment Terms, Currency)
            services.AddScoped<ErpSystem.Core.Interfaces.IFinanceSettingsService, ErpSystem.Api.Services.Finance.Settings.FinanceSettingsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IPaymentTermService, ErpSystem.Core.Services.Finance.PaymentTermService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICurrencyService, ErpSystem.Api.Services.Finance.MultiCurrency.CurrencyService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IExchangeRateService, ErpSystem.Api.Services.Finance.MultiCurrency.ExchangeRateService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.MultiCurrency.ExchangeRateWorkflowReconciliationService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.RecurringJournalWorkflowReconciliationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceExchangeRateOverrideService, ErpSystem.Api.Services.Finance.MultiCurrency.FinanceExchangeRateOverrideService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFiscalPeriodService, ErpSystem.Api.Services.Finance.Fiscal.FiscalPeriodService>();
            // BudgetService is the single Finance-owned aggregate for scenarios,
            // departmental returns, consolidated reporting, and governed revisions.
            // Register it here so callers do not create parallel budgeting services.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IBudgetService, ErpSystem.Api.Services.Finance.Budget.BudgetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceBudgetControlService, ErpSystem.Api.Services.Finance.Budget.FinanceBudgetControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceBudgetCommitmentService, ErpSystem.Api.Services.Finance.Budget.FinanceBudgetCommitmentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountingBookService, ErpSystem.Api.Services.Finance.Settings.AccountingBookService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountingBookPeriodService, ErpSystem.Api.Services.Finance.Settings.AccountingBookPeriodService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountingBookInitializationService, ErpSystem.Api.Services.Finance.Settings.AccountingBookInitializationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountingBookApplicabilityService, ErpSystem.Api.Services.Finance.Settings.AccountingBookApplicabilityService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceSourceBookAuthorityService, ErpSystem.Api.Services.Finance.GL.FinanceSourceBookAuthorityService>();
            services.AddOptions<ErpSystem.Api.Services.Finance.GL.AccountingEventOptions>()
                .BindConfiguration(ErpSystem.Api.Services.Finance.GL.AccountingEventOptions.SectionName);
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.AccountingEventService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountingEventService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.AccountingEventService>());
            services.AddOptions<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentOptions>()
                .BindConfiguration(ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentOptions.SectionName);
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceProducerIntentService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceProducerApprovedExecutionService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceProducerReversalPreparationService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceProducerReplayVerificationService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentService>());
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.IFinanceProducerApprovedExecution>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentService>());
            services.AddOptions<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentGroupOptions>()
                .BindConfiguration(ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentGroupOptions.SectionName);
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentGroupService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceProducerIntentGroupService>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentGroupService>());
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.IFinanceProducerIntentGroupApprovedExecution>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinanceProducerIntentGroupService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountClassificationService, ErpSystem.Api.Services.Finance.Settings.AccountClassificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountBookCurrencyPolicyService, ErpSystem.Api.Services.Finance.MultiCurrency.AccountBookCurrencyPolicyService>();
            services.AddFinanceAccountProvisioning();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinancialStatementLayoutService, ErpSystem.Api.Services.Finance.Reporting.FinancialStatementLayoutService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinancialStatementLayoutExecutionService, ErpSystem.Api.Services.Finance.Reporting.FinancialStatementLayoutExecutionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinancialStatementLayoutImportService, ErpSystem.Api.Services.Finance.Reporting.FinancialStatementLayoutImportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.IGeneralLedgerService, ErpSystem.Api.Services.Finance.GL.GeneralLedgerService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IJournalEntryService, ErpSystem.Api.Services.Finance.GL.JournalEntryService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.FinanceDimensionAdministrationService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.FinanceDimensionReportingFilterService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionCertificationService, ErpSystem.Api.Services.Finance.GL.FinanceDimensionCertificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceSourceDimensionAssignmentStore, ErpSystem.Api.Services.Finance.GL.FinanceSourceDimensionAssignmentStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceSourceDimensionService, ErpSystem.Api.Services.Finance.GL.FinanceSourceDimensionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceSettlementDimensionService, ErpSystem.Api.Services.Finance.GL.FinanceSettlementDimensionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinancePaymentDimensionAdapter, ErpSystem.Api.Services.Finance.GL.FinancePaymentDimensionAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IExternalFinancePostingAdapter, ErpSystem.Api.Services.Finance.GL.ExternalFinancePostingAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetDimensionService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetDimensionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                new ErpSystem.Api.Services.Finance.GL.FinanceOwnedSourceDimensionReadinessProvider(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceApVendorInvoice));
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                new ErpSystem.Api.Services.Finance.GL.FinanceOwnedSourceDimensionReadinessProvider(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceApSupplierDebitNote));
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                new ErpSystem.Api.Services.Finance.GL.FinanceOwnedSourceDimensionReadinessProvider(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceArCustomerInvoice));
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                new ErpSystem.Api.Services.Finance.GL.FinanceOwnedSourceDimensionReadinessProvider(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.InventoryDisposalAuctionInvoice));
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                new ErpSystem.Api.Services.Finance.GL.FinanceOwnedSourceDimensionReadinessProvider(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.SalesOrderCustomerInvoice));
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                new ErpSystem.Api.Services.Finance.GL.FinanceOwnedSourceDimensionReadinessProvider(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceBankDeposit));
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                new ErpSystem.Api.Services.Finance.GL.FinanceOwnedSourceDimensionReadinessProvider(
                    provider.GetRequiredService<ApplicationDbContext>(),
                    ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceReturnedCheque));
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider,
                ErpSystem.Api.Services.Finance.GL.FinanceReconciliationAdjustmentDimensionReadinessProvider>();
            foreach (var externalRoute in Enum.GetValues<ErpSystem.Core.Finance.Integration.FinanceExternalProducerContractId>())
            {
                var routeId = ErpSystem.Core.Finance.Integration.FinanceExternalProducerContractCatalog
                    .GetRequired(externalRoute).RouteId;
                services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                    new ErpSystem.Api.Services.Finance.GL.ExternalProducerDimensionReadinessProvider(
                        provider.GetRequiredService<ApplicationDbContext>(), routeId));
            }
            foreach (var fixedAssetRouteId in new[]
            {
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetCapitalization,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetCapitalizationReversal,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetDepreciation,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetDepreciationReversal,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetRevaluation,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetImpairment,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetImpairmentReversal,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetValuationCorrection,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetDisposal,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleInvoice,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleReceipt,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceFixedAssetReclassification,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceCapitalProjectSettlement,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceLeaseRecognition,
                ErpSystem.Core.Finance.Integration.FinanceDimensionRouteId.FinanceLeasePeriodPosting
            })
            {
                var routeId = fixedAssetRouteId;
                services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceDimensionReadinessProvider>(provider =>
                    new ErpSystem.Api.Services.Finance.FixedAssets.FinanceFixedAssetDimensionReadinessProvider(
                        provider.GetRequiredService<ApplicationDbContext>(), routeId));
            }
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IJournalBatchService, ErpSystem.Api.Services.Finance.GL.JournalBatchService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IJournalBatchSpreadsheetService, ErpSystem.Api.Services.Finance.GL.JournalBatchSpreadsheetService>();
            // Recurring journals extend the existing GL posting pipeline. The
            // processor is scoped because every run owns one EF unit of work;
            // the hosted service below only creates scopes and never posts.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IRecurringJournalService, ErpSystem.Api.Services.Finance.GL.RecurringJournalService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.RecurringJournalGenerationProcessor>();
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.RecurringJournalReversalProcessor>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceSystemPostingEngine, ErpSystem.Api.Services.Finance.GL.FinanceSystemPostingEngine>();
            services.AddScoped<ErpSystem.Core.Finance.IBusinessCalendarProvider, ErpSystem.Data.Services.PayrollBusinessCalendarProvider>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceAuditService, ErpSystem.Api.Services.Finance.FinanceAuditService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceAccessScopeService, ErpSystem.Api.Services.Finance.Security.FinanceAccessScopeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceReversalPolicyService, ErpSystem.Api.Services.Finance.FinanceReversalPolicyService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.GL.FinancePostingEngine>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinancePostingEngine>(provider =>
                provider.GetRequiredService<ErpSystem.Api.Services.Finance.GL.FinancePostingEngine>());
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IBookBalanceReadModelService, ErpSystem.Api.Services.Finance.GL.BookBalanceReadModelService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ISubledgerSettlementReadModelService, ErpSystem.Api.Services.Finance.SubledgerSettlementReadModelService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IOpeningBalanceService, ErpSystem.Api.Services.Finance.Migration.OpeningBalanceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IMigrationSignOffService, ErpSystem.Api.Services.Finance.Migration.MigrationSignOffService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitTypeService, ErpSystem.Api.Services.Finance.UnitAccounting.UnitTypeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceQuantityPrecisionAdapter, ErpSystem.Api.Services.Finance.UnitAccounting.FinanceQuantityPrecisionAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.ICommercialQuantityPolicyValidator, ErpSystem.Api.Services.Inventory.CommercialQuantityPolicyValidator>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitAccountService, ErpSystem.Api.Services.Finance.UnitAccounting.UnitAccountService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitJournalEntryService, ErpSystem.Api.Services.Finance.UnitAccounting.UnitJournalEntryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitBudgetService, ErpSystem.Api.Services.Finance.UnitAccounting.UnitBudgetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IRatioDefinitionService, ErpSystem.Api.Services.Finance.UnitAccounting.RatioDefinitionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAllocationService, ErpSystem.Api.Services.Finance.UnitAccounting.AllocationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IApReportsService, ErpSystem.Api.Services.Finance.AP.ApReportsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IArReportsService, ErpSystem.Api.Services.Finance.AR.ArReportsService>();
            // Finance collections reuse the existing Sales collection aggregate but
            // source balances exclusively from the posted AR settlement projection.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IArCollectionFollowUpService, ErpSystem.Api.Services.Finance.AR.ArCollectionFollowUpService>();
            // Customer account endpoints use the Finance AR source model and settlement projection.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICustomerService, ErpSystem.Api.Services.Finance.AR.CustomerService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetReportsService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetReportsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ITaxReportingService, ErpSystem.Api.Services.Finance.Taxation.TaxReportingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICashPositionReportService, ErpSystem.Api.Services.Finance.Cash.CashPositionReportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IWithholdingTaxCertificateService, ErpSystem.Api.Services.Finance.Taxation.WithholdingTaxCertificateService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IBankAccountService, ErpSystem.Api.Services.Finance.Cash.BankAccountService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IBankingSettlementService, ErpSystem.Api.Services.Finance.Cash.BankingSettlementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICashierTillService, ErpSystem.Api.Services.Finance.Cash.CashierTillService>();
            services.AddSingleton<ErpSystem.Api.Services.MobilePos.IMobilePosOfflineGrantTokenService, ErpSystem.Api.Services.MobilePos.MobilePosOfflineGrantTokenService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosFoundationService, ErpSystem.Api.Services.MobilePos.MobilePosFoundationService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosFinanceReadService, ErpSystem.Api.Services.MobilePos.MobilePosFinanceReadService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosCheckoutReadService, ErpSystem.Api.Services.MobilePos.MobilePosCheckoutReadService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosMutationExecutionService, ErpSystem.Api.Services.MobilePos.MobilePosMutationExecutionService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosOfflineGrantValidationService, ErpSystem.Api.Services.MobilePos.MobilePosOfflineGrantValidationService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosSaleService, ErpSystem.Api.Services.MobilePos.MobilePosSaleService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosSyncService, ErpSystem.Api.Services.MobilePos.MobilePosSyncService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosReceiptService, ErpSystem.Api.Services.MobilePos.MobilePosReceiptService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosTillSessionService, ErpSystem.Api.Services.MobilePos.MobilePosTillSessionService>();
            services.AddScoped<ErpSystem.Api.Services.MobilePos.IMobilePosTillFinalizationGuard, ErpSystem.Api.Services.MobilePos.MobilePosTillFinalizationGuard>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceReportExportService, ErpSystem.Api.Services.Finance.Reporting.FinanceReportExportService>();
            // FR-RP-010 deliberately extends the shared report/template model.
            // The processor is scoped because each scheduler pass owns one EF
            // unit of work; the hosted service below creates a fresh scope.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFinanceReportAutomationService, ErpSystem.Api.Services.Finance.Reporting.FinanceReportAutomationService>();
            services.AddScoped<ErpSystem.Api.Services.Finance.Reporting.FinanceReportAutomationProcessor>();
            services.AddScoped<ErpSystem.Api.Services.Finance.MultiCurrency.CurrencyRevaluationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICurrencyRevaluationService>(sp =>
                sp.GetRequiredService<ErpSystem.Api.Services.Finance.MultiCurrency.CurrencyRevaluationService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFxAccountingService>(sp =>
                sp.GetRequiredService<ErpSystem.Api.Services.Finance.MultiCurrency.CurrencyRevaluationService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IVendorInvoiceService, ErpSystem.Api.Services.Finance.AP.VendorInvoiceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IVendorInvoiceMatchExceptionService, ErpSystem.Api.Services.Finance.AP.VendorInvoiceMatchExceptionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IVendorPaymentService, ErpSystem.Api.Services.Finance.AP.VendorPaymentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ISupplierDebitNoteService, ErpSystem.Api.Services.Finance.AP.SupplierDebitNoteService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IInventorySupplierReturnFinanceHandoff>(provider =>
                (ErpSystem.Core.Interfaces.Finance.IInventorySupplierReturnFinanceHandoff)provider.GetRequiredService<ErpSystem.Core.Interfaces.Finance.ISupplierDebitNoteService>());
            // FIN-INT-012/013 is a Finance-owned, fail-closed consumer only. Procurement/Inventory
            // supplies approved immutable envelopes, never Finance account ids, and remains the
            // owner of return approval, dispatch, quantities, locations and carrying-cost evidence.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ISupplierReturnFinanceAdapter,
                ErpSystem.Api.Services.Finance.AP.SupplierReturnFinanceAdapter>();
            services.AddScoped<ErpSystem.Api.Services.Finance.AP.FinancePurchaseOrderReceiptPostingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetService>();
            // FIN-INT-007 consumes Procurement's accepted-supply contract but keeps all asset
            // policy, register reservation and capitalization posting inside Finance.
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IProcurementFixedAssetCapitalizationAdapter,
                ErpSystem.Api.Services.Finance.FixedAssets.ProcurementFixedAssetCapitalizationAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetCategoryService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetCategoryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetDepreciationService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetDepreciationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAssetValuationService, ErpSystem.Api.Services.Finance.FixedAssets.AssetValuationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAssetTransferService, ErpSystem.Api.Services.Finance.FixedAssets.AssetTransferService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAssetDisposalService, ErpSystem.Api.Services.Finance.FixedAssets.AssetDisposalService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICapitalProjectService, ErpSystem.Api.Services.Finance.FixedAssets.CapitalProjectService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ILeaseAccountingService, ErpSystem.Api.Services.Finance.FixedAssets.LeaseAccountingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.ITenantSettingsService, ErpSystem.Api.Services.TenantSettingsService>();

            // Phase 1: Core Maintenance Services - workflow-ready implementation - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderService, ErpSystem.Core.Services.Maintenance.WorkOrderService>();
            // MaintenanceInventoryService - now enabled with IInventoryManagementService dependency
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceInventoryService, ErpSystem.Core.Services.Maintenance.MaintenanceInventoryService>();


            // Asset Admission & Discharge services (P1-E4)
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetAdmissionService, ErpSystem.Core.Services.Maintenance.AssetAdmissionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetDischargeService, ErpSystem.Core.Services.Maintenance.AssetDischargeService>();

            // Asset Condition services - checklist for admission/discharge inspections
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetConditionRepository, ErpSystem.Data.Repositories.Maintenance.AssetConditionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetConditionService, ErpSystem.Core.Services.Maintenance.AssetConditionService>();

            // Tool Checkout Service - manages tool checkout/return operations
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IToolCheckoutService, ErpSystem.Core.Services.Maintenance.ToolCheckoutService>();
            // Work Order Tool Service - manages tool allocation and checkout for work orders
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderToolService, ErpSystem.Core.Services.Maintenance.WorkOrderToolService>();
            // Work Order Part Service - manages parts/consumables for work orders
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderPartService, ErpSystem.Core.Services.Maintenance.WorkOrderPartService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderSchedulingService, ErpSystem.Core.Services.Maintenance.WorkOrderSchedulingService>();
            // Work Order Labor Service - manages labor/time tracking for work orders
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderLaborService, ErpSystem.Core.Services.Maintenance.WorkOrderLaborService>();

            // Staff Schedule and Expense services - off-site maintenance tracking
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceStaffScheduleService, ErpSystem.Core.Services.Maintenance.MaintenanceStaffScheduleService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceExpenseService, ErpSystem.Core.Services.Maintenance.MaintenanceExpenseService>();

            // Phase 1.4: Quality Control Service - foundation for inspection workflows
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IQualityControlService, ErpSystem.Core.Services.Maintenance.QualityControlService>();

            // TODO: Quality Control requires inspection services - will be implemented in Phase 2
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IInspectionTemplateService, ErpSystem.Core.Services.Maintenance.InspectionTemplateService>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetInspectionService, ErpSystem.Core.Services.Maintenance.AssetInspectionService>();

            // Additional maintenance services - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianSchedulingService, ErpSystem.Core.Services.Maintenance.TechnicianSchedulingService>();

            // Asset type service - using Api namespace
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetTypeService, ErpSystem.Api.Services.Maintenance.AssetTypeService>();

            // New maintenance module services - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceScheduleService, ErpSystem.Core.Services.Maintenance.MaintenanceScheduleService>();
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IConditionEvaluationService, ErpSystem.Core.Services.Maintenance.ConditionEvaluationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicalSkillService, ErpSystem.Core.Services.Maintenance.TechnicalSkillService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ISafetyProtocolService, ErpSystem.Core.Services.Maintenance.SafetyProtocolService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianService, ErpSystem.Core.Services.Maintenance.TechnicianService>();

            // Phase 2 Enhanced Services - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IInspectionTemplateService, ErpSystem.Core.Services.Maintenance.InspectionTemplateService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAnalyticsService, ErpSystem.Core.Services.Maintenance.MaintenanceAnalyticsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceNotificationService, ErpSystem.Core.Services.Maintenance.MaintenanceNotificationService>();

            // Phase 3 IoT Integration - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IIoTMaintenanceService, ErpSystem.Core.Services.Maintenance.IoTMaintenanceService>();

            // Phase 2 Mobile API Enhancement - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMobileMaintenanceService, ErpSystem.Core.Services.Maintenance.MobileMaintenanceService>();

            // Phase 3 Advanced Reporting - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAdvancedMaintenanceReportingService, ErpSystem.Core.Services.Maintenance.AdvancedMaintenanceReportingService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceOperationalReportsService, ErpSystem.Core.Services.Maintenance.MaintenanceOperationalReportsService>();

            // Phase 3 Asset Performance Analytics - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetPerformanceAnalyticsService, ErpSystem.Core.Services.Maintenance.AssetPerformanceAnalyticsService>();

            // Missing maintenance services that controllers require - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IPriorityLevelService, ErpSystem.Api.Services.Maintenance.PriorityLevelService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderTypeService, ErpSystem.Api.Services.Maintenance.WorkOrderTypeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceTypeService, ErpSystem.Api.Services.Maintenance.MaintenanceTypeService>();

            // Additional maintenance services - ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAssetCategoryService, ErpSystem.Api.Services.Maintenance.MaintenanceAssetCategoryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IJobCardService, ErpSystem.Core.Services.Maintenance.JobCardService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITaskTemplateService, ErpSystem.Core.Services.Maintenance.TaskTemplateService>();

            // Usage Tracking and Trigger Evaluation - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetUsageTrackingService, ErpSystem.Core.Services.Maintenance.AssetUsageTrackingService>();
            services.AddScoped<ErpSystem.Core.Services.Maintenance.MaintenanceTriggerEvaluationService>();

            // Asset downtime tracking - REQUIRED by EmergencyMaintenanceController and MaintenanceDashboardController
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetDowntimeService, ErpSystem.Core.Services.Maintenance.AssetDowntimeService>();

            // TODO: Additional maintenance services to be implemented as needed:
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetInspectionService, ErpSystem.Core.Services.Maintenance.AssetInspectionService>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianTeamService, ErpSystem.Core.Services.Maintenance.TechnicianTeamService>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetDowntimeService, ErpSystem.Core.Services.Maintenance.AssetDowntimeService>();

            // Technician Scheduling services
            // TODO: Create missing maintenance services
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianSchedulingService, ErpSystem.Core.Services.Maintenance.TechnicianSchedulingService>();
            // services.AddScoped<ErpSystem.Core.Services.Maintenance.WorkOrderSchedulingService>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceInventoryService, ErpSystem.Core.Services.Maintenance.MaintenanceInventoryService>();

            #region HR Services

            // [HR-MODULE-PORT] All services/repositories ported from HRApi are registered here in one
            // call; the file is generated so re-syncing HR only rewrites HrModuleServiceRegistration.cs.
            // It is invoked FIRST on purpose: the explicit registrations below (RHEMA's retained
            // appraisal/performance model, salary mapping and hrdev bank services) are applied after and
            // therefore win for those interfaces. See HR_MODULE_PORT_PLAN.md.
            services.AddHrModuleServices();

            // [HR-MODULE-PORT] Public-recruitment catalogue services + their repository.
            // These concrete implementations were ported but never registered (the generated
            // HrModuleServiceRegistration only emits registrations it detected), so
            // PublicRecruitmentController could not be constructed. Registered here (after the
            // generated call) so they survive HR re-syncs.
            services.AddScoped<ErpSystem.Core.Interfaces.HR.ICountryRepository, ErpSystem.Data.Repositories.HR.CountryRepository>();
            services.AddScoped<ICountryService, CountryService>();
            services.AddScoped<ISkillService, SkillService>();
            services.AddScoped<IQualificationCatalogueService, QualificationCatalogueService>();

            // [HR-MODULE-PORT] Nominee availability service — ported from HRApi into
            // Core/Services/HR/Training (see NomineeAvailabilityService). The concrete class was
            // left behind during the port (it lived in HRApi's ErpSystem.Data/Services), so
            // TrainingNominationsController could not be activated.
            services.AddScoped<INomineeAvailabilityService, NomineeAvailabilityService>();

            // HR Services - NOW ENABLED
            services.AddScoped<IEmployeeService, EmployeeService>();
            // HR's payroll-membership statement beside payroll's own profile; the one place HR
            // reaches into payroll (through IPayrollService's public upsert, create-only).
            services.AddScoped<IPayrollMembershipService, PayrollMembershipService>();

            // Staff numbering is per-register configuration, not a compiled-in format.
            services.AddScoped<ErpSystem.Core.Services.HR.IStaffNumberService,
                               ErpSystem.Core.Services.HR.StaffNumberService>();

            // Employee bulk import (docs/HR/areas/employees/HR-EMPLOYEE-IMPORT-DESIGN.md): template, check, review,
            // commit. Lives in Api because it reads and writes workbooks (ClosedXML) and stores the
            // upload through the HR document gate; every employee still goes through IEmployeeService.
            services.AddScoped<ErpSystem.Core.Interfaces.HR.IEmployeeImportService,
                               ErpSystem.Api.Services.HR.EmployeeImport.EmployeeImportService>();

            // Reference dimensions: the qualification ladder, certifying bodies, and the
            // staff-numbering rules themselves.
            services.AddScoped<ErpSystem.Core.Services.HR.IReferenceDimensionService,
                               ErpSystem.Core.Services.HR.ReferenceDimensionService>();

            // Shared cross-module reference data: administrative geography (Region / District /
            // Town and their equivalents in any country). Registered here for now because this is
            // where the HR module's services are wired and HR is its first consumer; it belongs to
            // no module. See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
            services.AddScoped<ErpSystem.Core.Services.Reference.IGeographyService,
                               ErpSystem.Core.Services.Reference.GeographyService>();

            // Every module that stores a GeoAreaId registers a probe here, so the geography service
            // can refuse to delete an area still in use without having to know what a module is.
            // ⚠ A consumer that gains a GeoAreaId and forgets this line gets NO protection: the
            // deletes are soft, so the foreign key never fires.
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.EmployeeGeoAreaConsumer>();
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.LocationGeoAreaConsumer>();
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.CompanyProfileGeoAreaConsumer>();
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.HealthcareFacilityGeoAreaConsumer>();

            // Round 2, lane D2 — the addresses hanging off an employee. The employee's own address
            // was covered from the start; these four were not, and a soft-deleted area would have
            // taken a next of kin's address, a guarantor's and a previous employer's with it.
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.EmployeeContactGeoAreaConsumer>();
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.EmployeeEmergencyContactGeoAreaConsumer>();
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.EmployeeGuarantorGeoAreaConsumer>();
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.EmployeeWorkHistoryGeoAreaConsumer>();

            // Round 4, lane A — recruitment. The candidate's own address, and the areas a vacancy's
            // Location criterion screens on. The second is not an address and earns a probe anyway:
            // deleting an area a live vacancy accepts would silently stop matching the candidates
            // who are in it, and a shortlist that quietly shrinks explains nothing on screen.
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.JobCandidateGeoAreaConsumer>();
            services.AddScoped<ErpSystem.Core.Interfaces.Reference.IGeoAreaConsumer,
                               ErpSystem.Core.Services.HR.ShortlistingCriteriaGeoAreaConsumer>();

            // The company seal and signature, versioned rather than overwritten.
            services.AddScoped<ErpSystem.Core.Services.HR.ICompanySealAssetService,
                               ErpSystem.Core.Services.HR.CompanySealAssetService>();

            // The identification-expiry sweep. ⚠ The SERVICE and its HOST are registered together
            // on purpose: two HR engines existed, had endpoints, and had never
            // run because nothing hosted them.
            services.AddScoped<ErpSystem.Core.Services.HR.IIdentificationExpiryReminderService,
                               ErpSystem.Core.Services.HR.IdentificationExpiryReminderService>();

            // Demo feedback round 2, lane C2 — the certification model and its expiry sweep. The
            // sweep's host is registered with the other hosted services below.
            services.AddScoped<ErpSystem.Core.Services.HR.ICertificationService,
                               ErpSystem.Core.Services.HR.CertificationService>();
            services.AddScoped<ErpSystem.Core.Services.HR.ICertificationExpiryReminderService,
                               ErpSystem.Core.Services.HR.CertificationExpiryReminderService>();

            // Demo feedback round 2, lane C3 — named sets. The three masters, and the one service
            // that unions a position's attached sets with its individual rows. ⚠ Several existing
            // services now depend on IPositionNamedSetService for their EFFECTIVE reads (benefit
            // enrolment, succession matching, certification compliance, offer benefit seeding), so
            // it must be registered even where no set has been created yet.
            services.AddScoped<ErpSystem.Core.Services.HR.IPositionNamedSetService,
                               ErpSystem.Core.Services.HR.PositionNamedSetService>();
            services.AddScoped<ErpSystem.Core.Services.HR.IBenefitGroupService,
                               ErpSystem.Core.Services.HR.BenefitGroupService>();
            services.AddScoped<ErpSystem.Core.Services.HR.ISkillSetService,
                               ErpSystem.Core.Services.HR.SkillSetService>();
            services.AddScoped<ErpSystem.Core.Services.HR.ICertificationSetService,
                               ErpSystem.Core.Services.HR.CertificationSetService>();

            // Organization Structure Services
            services.AddScoped<IOrganizationStructureService, OrganizationStructureService>();
            services.AddScoped<IOrganizationLevelService, OrganizationLevelService>();
            services.AddScoped<IOrganizationUnitService, OrganizationUnitService>();
            services.AddScoped<IOrganizationUnitHistoryService, OrganizationUnitHistoryService>();

            // Location Structure Services
            services.AddScoped<ILocationStructureService, LocationStructureService>();
            services.AddScoped<ILocationLevelService, LocationLevelService>();
            services.AddScoped<ILocationService, LocationService>();
            services.AddScoped<ILocationContactService, LocationContactService>();

            // Payroll owns the salary structure; HR mirrors it. Registered before the salary services
            // because they depend on it for reconcile-on-read.
            services.AddScoped<ISalaryStructureProjectionService, SalaryStructureProjectionService>();
            services.AddScoped<ISalaryStructureService, SalaryStructureService>();
            services.AddScoped<ISalaryGradeService, SalaryStructureService>();
            services.AddScoped<ISalaryLevelService, SalaryStructureService>();
            services.AddScoped<ISalaryNotchService, SalaryStructureService>();
            services.AddScoped<IPayrollService, PayrollService>();

            services.AddScoped<ILeaveService, LeaveService>();

            services.AddScoped<IDepartmentService, DepartmentService>();
            services.AddScoped<IEmployeePositionService, EmployeePositionService>();
            services.AddScoped<IEmployeeBankService, EmployeeBankService>();
            services.AddScoped<IEmployeeBankBranchService, EmployeeBankBranchService>();
            
            services.AddScoped<IPerformanceImprovementPlanService, PerformanceImprovementPlanService>();
            
            #endregion HR Services
            
            // Maintenance background services - temporarily disabled to get API running
            // services.AddHostedService<ErpSystem.Api.Services.Maintenance.MaintenanceBackgroundService>();
            
            // Business Partner / Supplier & Contractor Management Services - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerService, ErpSystem.Core.Services.Procurement.BusinessPartnerService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerCategoryService, ErpSystem.Core.Services.Procurement.PartnerCategoryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractorSpecializationService, ErpSystem.Core.Services.Procurement.ContractorSpecializationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ILicenseTypeService, ErpSystem.Core.Services.Procurement.LicenseTypeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerRegistrationService, ErpSystem.Core.Services.Procurement.BusinessPartnerRegistrationService>();

            // Performance Tracking Services
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierPerformanceService, ErpSystem.Core.Services.Procurement.SupplierPerformanceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IQualityIncidentService, ErpSystem.Core.Services.Procurement.QualityIncidentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPerformanceReviewService, ErpSystem.Core.Services.Procurement.PerformanceReviewService>();

            services.AddScoped<ErpSystem.Core.Interfaces.IUnitOfWork, ErpSystem.Data.UnitOfWork>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerVerificationService, ErpSystem.Core.Services.Procurement.PartnerVerificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerPerformanceService, ErpSystem.Core.Services.Procurement.PartnerPerformanceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerBlacklistService, ErpSystem.Core.Services.Procurement.PartnerBlacklistService>();
            services.AddScoped<ErpSystem.Core.Services.Procurement.ISupplierValidationService, ErpSystem.Core.Services.Procurement.SupplierValidationService>();
            services.AddScoped<ErpSystem.Core.Services.Procurement.ISupplierReportingService, ErpSystem.Core.Services.Procurement.SupplierReportingService>();

            // Blacklist Appeal Services
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBlacklistAppealService, ErpSystem.Core.Services.Procurement.BlacklistAppealService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBlacklistHistoryService, ErpSystem.Core.Services.Procurement.BlacklistHistoryService>();

            // Tender Management Services
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderService, ErpSystem.Core.Services.Procurement.TenderService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderBidService, ErpSystem.Core.Services.Procurement.TenderBidService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderEvaluationService, ErpSystem.Core.Services.Procurement.TenderEvaluationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAwardService, ErpSystem.Core.Services.Procurement.TenderAwardService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderNotificationService, ErpSystem.Core.Services.Procurement.TenderNotificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderInvitationDocumentService, ErpSystem.Api.Services.TenderInvitationDocumentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderTemplateService, ErpSystem.Core.Services.Procurement.TenderTemplateService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEvaluationCriterionService, ErpSystem.Core.Services.Procurement.EvaluationCriterionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEvaluationTemplateService, ErpSystem.Core.Services.Procurement.EvaluationTemplateService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderDocumentTypeService, ErpSystem.Core.Services.Procurement.TenderDocumentTypeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerUserService, ErpSystem.Core.Services.Procurement.BusinessPartnerUserService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderAssignmentService, ErpSystem.Core.Services.Procurement.TenderAssignmentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IAwardVerificationService, ErpSystem.Core.Services.Procurement.AwardVerificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPerformanceBondService, ErpSystem.Core.Services.Procurement.PerformanceBondService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderNegotiationService, ErpSystem.Core.Services.Procurement.TenderNegotiationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSettingsService, ErpSystem.Core.Services.Procurement.ProcurementSettingsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementConfigurationService, ErpSystem.Core.Services.Procurement.ProcurementConfigurationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyConfigurationService, ErpSystem.Data.Services.QuantitySurveyConfigurationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringConfigurationService, ErpSystem.Data.Services.CivilEngineeringConfigurationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringDesignService, ErpSystem.Data.Services.CivilEngineeringDesignService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringSupervisionService, ErpSystem.Data.Services.CivilEngineeringSupervisionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringSiteInstructionService, ErpSystem.Data.Services.CivilEngineeringSiteInstructionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringRfiService, ErpSystem.Data.Services.CivilEngineeringRfiService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringQualityTestService, ErpSystem.Data.Services.CivilEngineeringQualityTestService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringInspectionControlService, ErpSystem.Data.Services.CivilEngineeringInspectionControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringIpcEndorsementService, ErpSystem.Data.Services.CivilEngineeringIpcEndorsementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringWeeklySupervisionService, ErpSystem.Data.Services.CivilEngineeringWeeklySupervisionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringExtensionOfTimeService, ErpSystem.Data.Services.CivilEngineeringExtensionOfTimeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringMaintenanceIntakeService, ErpSystem.Data.Services.CivilEngineeringMaintenanceIntakeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringDevelopmentApprovalFileService, ErpSystem.Data.Services.CivilEngineeringDevelopmentApprovalFileService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringDevelopmentApprovalFileHandoffService, ErpSystem.Data.Services.CivilEngineeringDevelopmentApprovalFileHandoffService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringPermittingEngineeringReviewService, ErpSystem.Data.Services.CivilEngineeringPermittingEngineeringReviewService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringPermittingHodDecisionService, ErpSystem.Data.Services.CivilEngineeringPermittingHodDecisionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringDirectTaskService, ErpSystem.Data.Services.CivilEngineeringDirectTaskService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringMigrationService, ErpSystem.Data.Services.CivilEngineeringMigrationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringMaintenanceAssessmentService, ErpSystem.Data.Services.CivilEngineeringMaintenanceAssessmentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringMaintenanceCostingHandoffService, ErpSystem.Data.Services.CivilEngineeringMaintenanceCostingHandoffService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringMaintenanceExecutionLinkService, ErpSystem.Data.Services.CivilEngineeringMaintenanceExecutionLinkService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringMaintenanceCompletionControlService, ErpSystem.Data.Services.CivilEngineeringMaintenanceCompletionControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.ICivilEngineeringComplaintResolutionService, ErpSystem.Data.Services.CivilEngineeringComplaintResolutionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyBoqSpreadsheetService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyBoqSpreadsheetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyTenderBoqSubmissionService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyTenderBoqSubmissionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyRateLibraryService, ErpSystem.Data.Services.QuantitySurveyRateLibraryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyEscalationFormulaService, ErpSystem.Data.Services.QuantitySurveyEscalationFormulaService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyPriceIndexImportService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyPriceIndexImportService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyEscalationCalculationService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyEscalationCalculationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyEscalationDisputeService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyEscalationDisputeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyMeasurementService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyMeasurementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyJointMeasurementService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyJointMeasurementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyDesignRevisionImpactService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyDesignRevisionImpactService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyValuationWorksheetService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyValuationWorksheetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyPaymentCertificateService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyPaymentCertificateService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyAdvanceRecoveryService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyAdvanceRecoveryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyFinalAccountService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyFinalAccountService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyMaterialReconciliationService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyMaterialReconciliationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyVariationService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyVariationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyContractClaimService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyContractClaimService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyDayworkService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyDayworkService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveyContractCommercialTermsService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveyContractCommercialTermsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveySubcontractService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveySubcontractService>();
            services.AddScoped<ErpSystem.Core.Interfaces.QuantitySurvey.IQuantitySurveySubcontractChargeService, ErpSystem.Api.Services.QuantitySurvey.QuantitySurveySubcontractChargeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPolicyService, ErpSystem.Core.Services.Procurement.ProcurementPolicyService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementComplianceDecisionService, ErpSystem.Core.Services.Procurement.ProcurementComplianceDecisionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSodGuardService, ErpSystem.Core.Services.Procurement.ProcurementSodGuardService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSodPolicy, ErpSystem.Core.Services.Procurement.ProcurementSodPolicy>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementAccessControlService, ErpSystem.Core.Services.Procurement.ProcurementAccessControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementControlEventService, ErpSystem.Core.Services.Procurement.ProcurementControlEventService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Audit.IAuditRecordProvider, ErpSystem.Core.Services.Audit.PlatformAuditLogRecordProvider>();
            services.AddScoped<ErpSystem.Core.Interfaces.Audit.IAuditRecordProvider, ErpSystem.Core.Services.Audit.ProcurementControlEventAuditRecordProvider>();
            services.AddScoped<ErpSystem.Core.Interfaces.Audit.IAuditGovernanceService, ErpSystem.Core.Services.Audit.AuditGovernanceService>();
            services.AddSingleton<ErpSystem.Core.Interfaces.Audit.IAuditEventCoverageContributor, ErpSystem.Core.Services.Audit.ProcurementInventoryAuditEventCoverageContributor>();
            services.AddSingleton<ErpSystem.Core.Interfaces.Audit.IAuditEventCoverageContributor, ErpSystem.Core.Services.QuantitySurvey.QuantitySurveyAuditEventCoverageContributor>();
            services.AddSingleton<ErpSystem.Core.Interfaces.Audit.IAuditEventCoverageContributor, ErpSystem.Core.Services.Projects.CivilEngineeringAuditEventCoverageContributor>();
            services.AddSingleton<ErpSystem.Core.Interfaces.Audit.IAuditEventCoverageService, ErpSystem.Core.Services.Audit.AuditEventCoverageService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementAppSubmissionService, ErpSystem.Core.Services.Procurement.ProcurementAppSubmissionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSpecificationTemplateService, ErpSystem.Core.Services.Procurement.ProcurementSpecificationTemplateService>();
            services.AddScoped<ErpSystem.Core.Services.Procurement.ProcurementCalendarService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementCalendarService>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Procurement.ProcurementCalendarService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementCalendarProcessor>(provider =>
                provider.GetRequiredService<ErpSystem.Core.Services.Procurement.ProcurementCalendarService>());
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementRequisitionLinkageService, ErpSystem.Core.Services.Procurement.ProcurementRequisitionLinkageService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementRequisitionSubmissionControlService, ErpSystem.Core.Services.Procurement.ProcurementRequisitionSubmissionControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementBudgetReservationStore, ErpSystem.Data.Repositories.Procurement.ProcurementBudgetReservationStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementBudgetCommitmentLifecycleService, ErpSystem.Core.Services.Procurement.ProcurementBudgetCommitmentLifecycleService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementRequisitionBudgetControlService, ErpSystem.Core.Services.Procurement.ProcurementRequisitionBudgetControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementRequisitionAuthorityRouteService, ErpSystem.Core.Services.Procurement.ProcurementRequisitionAuthorityRouteService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementRequisitionSourcingReleaseStore, ErpSystem.Data.Repositories.Procurement.ProcurementRequisitionSourcingReleaseStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementRequisitionSourcingReleaseService, ErpSystem.Core.Services.Procurement.ProcurementRequisitionSourcingReleaseService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSourcingCaseService, ErpSystem.Core.Services.Procurement.ProcurementSourcingCaseService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementRfqControlService, ErpSystem.Core.Services.Procurement.ProcurementRfqControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementTenderControlService, ErpSystem.Core.Services.Procurement.ProcurementTenderControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementTenderDocumentControlService, ErpSystem.Core.Services.Procurement.ProcurementTenderDocumentControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierEvidencePackService, ErpSystem.Core.Services.Procurement.ProcurementSupplierEvidencePackService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierOnboardingTokenService, ErpSystem.Core.Services.Procurement.ProcurementSupplierOnboardingTokenService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierApplicantContactCorrectionStore, ErpSystem.Data.Repositories.Procurement.ProcurementSupplierApplicantContactCorrectionStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierApplicantAccessService, ErpSystem.Core.Services.Procurement.ProcurementSupplierApplicantAccessService>();
            services.AddScoped<ErpSystem.Api.Services.IProcurementSupplierApplicantJwtService, ErpSystem.Api.Services.ProcurementSupplierApplicantJwtService>();
            services.AddTransient<ErpSystem.Api.Middleware.SupplierApplicantAccessMiddleware>();
            services.AddTransient<ErpSystem.Api.Middleware.TemporaryPasswordChangeMiddleware>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierDueDiligenceService, ErpSystem.Core.Services.Procurement.ProcurementSupplierDueDiligenceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementFrameworkAgreementService, ErpSystem.Core.Services.Procurement.ProcurementFrameworkAgreementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementFrameworkCallOffService, ErpSystem.Core.Services.Procurement.ProcurementFrameworkCallOffService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPurchaseOrderSourceService, ErpSystem.Core.Services.Procurement.ProcurementPurchaseOrderSourceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementAcceptedSupplyService, ErpSystem.Core.Services.Procurement.ProcurementAcceptedSupplyService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementReceiptSourceControlService, ErpSystem.Core.Services.Procurement.ProcurementReceiptSourceControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementReceiptInspectionService, ErpSystem.Core.Services.Procurement.ProcurementReceiptInspectionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementReceiptInspectionStore, ErpSystem.Data.Repositories.Procurement.ProcurementReceiptInspectionStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPurchaseOrderComplianceService, ErpSystem.Core.Services.Procurement.ProcurementPurchaseOrderComplianceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPurchaseOrderSodService, ErpSystem.Core.Services.Procurement.ProcurementPurchaseOrderSodService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementInvoicePaymentSodService, ErpSystem.Core.Services.Procurement.ProcurementInvoicePaymentSodService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPurchaseOrderAmendmentStore, ErpSystem.Data.Repositories.Procurement.ProcurementPurchaseOrderAmendmentStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPurchaseOrderAmendmentService, ErpSystem.Core.Services.Procurement.ProcurementPurchaseOrderAmendmentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementContractActivationStore, ErpSystem.Data.Repositories.Procurement.ProcurementContractActivationStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementContractActivationService, ErpSystem.Core.Services.Procurement.ProcurementContractActivationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementContractOperationsService, ErpSystem.Core.Services.Procurement.ProcurementContractOperationsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementWorksCloseoutService, ErpSystem.Core.Services.Procurement.ProcurementWorksCloseoutService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementWorksCloseoutStore, ErpSystem.Data.Repositories.Procurement.ProcurementWorksCloseoutStore>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierAvlService, ErpSystem.Core.Services.Procurement.ProcurementSupplierAvlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierRiskService, ErpSystem.Core.Services.Procurement.ProcurementSupplierRiskService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSupplierPerformanceScorecardService, ErpSystem.Core.Services.Procurement.ProcurementSupplierPerformanceScorecardService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementEvaluationCommitteeControlService, ErpSystem.Core.Services.Procurement.ProcurementEvaluationCommitteeControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementAwardReadinessService, ErpSystem.Core.Services.Procurement.ProcurementAwardReadinessService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementBidderCommunicationService, ErpSystem.Core.Services.Procurement.ProcurementBidderCommunicationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementGhanepsExchangeService, ErpSystem.Core.Services.Procurement.ProcurementGhanepsExchangeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementExceptionalSourcingControlService, ErpSystem.Core.Services.Procurement.ProcurementExceptionalSourcingControlService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPrequalificationService, ErpSystem.Core.Services.Procurement.ProcurementPrequalificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementMasterDataChangeService, ErpSystem.Core.Services.Procurement.ProcurementMasterDataChangeService>();
            services.AddScoped<ErpSystem.Data.Seeders.ProcurementConfigurationProfileSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.QuantitySurveyConfigurationProfileSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.QuantitySurveyAccessControlSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.CivilEngineeringConfigurationProfileSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.CivilEngineeringAccessControlSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.ProcurementSupplierOnboardingTestSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.ProcurementAccessControlSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.ProcurementStatutoryReportSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.InventoryStatutoryReportSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.HrAwardsReportSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.AuditComplianceReportSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.QuantitySurveyStatutoryReportSeeder>();
            services.AddScoped<ErpSystem.Data.Seeders.CivilEngineeringStatutoryReportSeeder>();
            services.AddScoped<ErpSystem.Core.Interfaces.Crm.ICrmService, ErpSystem.Core.Services.Crm.CrmService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesAgreementService, ErpSystem.Api.Services.Sales.SalesAgreementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesOrderService, ErpSystem.Core.Services.Sales.SalesOrderService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesOrderCustomerDepositService, ErpSystem.Api.Services.Sales.SalesOrderCustomerDepositService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesOrderInvoiceService, ErpSystem.Api.Services.Sales.SalesOrderInvoiceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.IDeliveryService, ErpSystem.Core.Services.Sales.DeliveryService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesSetupService, ErpSystem.Core.Services.Sales.SalesSetupService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesAllocationService, ErpSystem.Core.Services.Sales.SalesAllocationService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.IOpportunityService, ErpSystem.Core.Services.Sales.OpportunityService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ICompetitorService, ErpSystem.Core.Services.Sales.CompetitorService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesReportingService, ErpSystem.Core.Services.Sales.SalesReportingService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesSaleableSourceAdapter, ErpSystem.Core.Services.Sales.ProjectUnitSaleableSourceAdapter>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesSaleableSourceAdapter, ErpSystem.Core.Services.Sales.InventorySaleableSourceAdapter>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesSaleableSourceAdapter, ErpSystem.Core.Services.Sales.FixedAssetSaleableSourceAdapter>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesSaleableSourceAdapter, ErpSystem.Core.Services.Sales.PropertyRegisterSaleableSourceAdapter>();
        services.AddScoped<ErpSystem.Core.Interfaces.Sales.ISalesSaleableSourceAdapter, ErpSystem.Core.Services.Sales.LandManagementSaleableSourceAdapter>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.IQuoteService, ErpSystem.Core.Services.Sales.QuoteService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.ICommissionService, ErpSystem.Core.Services.Sales.CommissionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Sales.IReturnOrderService, ErpSystem.Core.Services.Sales.ReturnOrderService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceSettingsService, ErpSystem.Core.Services.Maintenance.MaintenanceSettingsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectService, ErpSystem.Core.Services.Projects.ProjectService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectSetupService, ErpSystem.Core.Services.Projects.ProjectSetupService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectManagementSettingsService, ErpSystem.Core.Services.Projects.ProjectManagementSettingsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAttachmentService, ErpSystem.Core.Services.Maintenance.MaintenanceAttachmentService>();

            // Fleet (Maintenance)
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetVehicleService, ErpSystem.Core.Services.Maintenance.Fleet.FleetVehicleService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetDriverDirectoryService, ErpSystem.Core.Services.Maintenance.Fleet.FleetDriverDirectoryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetTripService, ErpSystem.Core.Services.Maintenance.Fleet.FleetTripService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetTripDestinationService, ErpSystem.Core.Services.Maintenance.Fleet.FleetTripDestinationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetComplianceService, ErpSystem.Core.Services.Maintenance.Fleet.FleetComplianceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetComplianceTemplateService, ErpSystem.Core.Services.Maintenance.Fleet.FleetComplianceTemplateService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetFuelService, ErpSystem.Core.Services.Maintenance.Fleet.FleetFuelService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetAssignmentService, ErpSystem.Core.Services.Maintenance.Fleet.FleetAssignmentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetInspectionService, ErpSystem.Core.Services.Maintenance.Fleet.FleetInspectionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetDefectService, ErpSystem.Core.Services.Maintenance.Fleet.FleetDefectService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetIncidentService, ErpSystem.Core.Services.Maintenance.Fleet.FleetIncidentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetTyreService, ErpSystem.Core.Services.Maintenance.Fleet.FleetTyreService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetBatteryService, ErpSystem.Core.Services.Maintenance.Fleet.FleetBatteryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetExternalRepairService, ErpSystem.Core.Services.Maintenance.Fleet.FleetExternalRepairService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetDashboardService, ErpSystem.Core.Services.Maintenance.Fleet.FleetDashboardService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetCostService, ErpSystem.Core.Services.Maintenance.Fleet.FleetCostService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetReportsService, ErpSystem.Core.Services.Maintenance.Fleet.FleetReportsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IFleetHealthService, ErpSystem.Core.Services.Maintenance.Fleet.FleetHealthService>();
            services.AddScoped<ErpSystem.Core.Interfaces.IDistributedLockService, ErpSystem.Api.Services.DistributedLockService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IConsignmentSettlementService, ErpSystem.Core.Services.Procurement.ConsignmentSettlementService>();
            services.AddScoped<ErpSystem.Api.Services.ProcurementInventoryManagementDashboardService>();
            services.AddScoped<ErpSystem.Api.Services.EnterpriseDashboardService>();
            services.AddScoped<ErpSystem.Api.Services.EnterpriseDashboardProjectionService>();

            // RFQ (Request For Quotation) - separate from Tender
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRfqService, ErpSystem.Core.Services.Procurement.RfqService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRfqNotificationService, ErpSystem.Core.Services.Procurement.RfqNotificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IRfqInvitationDocumentService, ErpSystem.Api.Services.RfqInvitationDocumentService>();

            // Contract Management Services
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractService, ErpSystem.Core.Services.Procurement.ContractService>();

            // Procurement Planning Services
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementPlanService, ErpSystem.Core.Services.Procurement.ProcurementPlanService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementBudgetService, ErpSystem.Core.Services.Procurement.ProcurementBudgetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementScheduleService, ErpSystem.Core.Services.Procurement.ProcurementScheduleService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IMarketAnalysisService, ErpSystem.Core.Services.Procurement.MarketAnalysisService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierConsolidationService, ErpSystem.Core.Services.Procurement.SupplierConsolidationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IEmergencyProcurementPlanService, ErpSystem.Core.Services.Procurement.EmergencyProcurementPlanService>();

            // Maintenance background services
            services.AddHostedService<ErpSystem.Api.Services.Maintenance.MaintenanceTriggerEvaluationBackgroundService>();
            services.AddHostedService<ErpSystem.Api.Services.Maintenance.FleetComplianceReminderBackgroundService>();

            // Notification dispatcher background service - sends pending notifications on schedule with dead-letter support
            services.AddHostedService<ErpSystem.Api.Services.NotificationDispatcherBackgroundService>();
            services.AddHostedService<ErpSystem.Api.Services.ProcurementCalendarBackgroundService>();
            services.AddScoped<ErpSystem.Core.Services.Procurement.ProcurementTenderClosingProcessor>();
            services.AddHostedService<ErpSystem.Api.Services.ProcurementTenderClosingBackgroundService>();
            services.AddHostedService<ErpSystem.Api.Services.ProcurementSupplierDueDiligenceBackgroundService>();
            services.AddHostedService<ErpSystem.Api.Services.ProcurementSupplierAvlBackgroundService>();

            // Blacklist expiry background service
            services.AddHostedService<ErpSystem.Core.Services.Procurement.BlacklistExpiryBackgroundService>();

            // Exception log maintenance (retention purge)
            services.AddHostedService<ErpSystem.Api.Services.ExceptionLogMaintenanceBackgroundService>();

            // Reconciles temporary fiscal-period module reopenings and sends expiry notices.
            services.AddHostedService<ErpSystem.Api.Services.Finance.Fiscal.ModuleLockExpiryBackgroundService>();

            // Finance close deadlines and maker-checker approvals use the shared notification
            // pipeline. A scoped processor keeps the durable idempotency state testable while the
            // lightweight host invokes it for every tenant at a controlled interval.
            services.AddScoped<ErpSystem.Api.Services.Finance.Fiscal.FinanceCloseAlertService>();
            services.AddHostedService<ErpSystem.Api.Services.Finance.Fiscal.FinanceCloseAlertBackgroundService>();

            // Materialize due recurring-journal occurrences independently of a
            // signed-in browser. Generated items remain Pending Approval, so
            // automation cannot bypass Finance maker-checker controls.
            services.AddHostedService<ErpSystem.Api.Services.Finance.GL.RecurringJournalBackgroundService>();

            // Generates private, retained Finance report artifacts even when no
            // user has the report workspace open.
            services.AddHostedService<ErpSystem.Api.Services.Finance.Reporting.FinanceReportAutomationBackgroundService>();

            // Tenant data retention (audit/security logs, notifications, EHC audit events)
            services.AddHostedService<ErpSystem.Api.Services.DataRetentionBackgroundService>();

            // EHC SLA monitoring / escalation (tickets)
            services.AddHostedService<ErpSystem.Api.Services.Ehc.EhcSlaMonitoringBackgroundService>();

            // EHC Microsoft 365 inbound email polling (email-to-ticket)
            services.AddHostedService<ErpSystem.Api.Services.Ehc.EhcMicrosoft365InboundEmailBackgroundService>();

            // Notification template and escalation services
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IMaintenanceNotificationTemplateService,
                ErpSystem.Core.Services.Maintenance.MaintenanceNotificationTemplateService>();
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IMaintenanceEscalationService,
                ErpSystem.Core.Services.Maintenance.MaintenanceEscalationService>();
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IDeadLetterNotificationService,
                ErpSystem.Core.Services.Maintenance.DeadLetterNotificationService>();

            // Add AutoMapper - using assembly scanning approach
            services.AddAutoMapper(
                _ => { },
                typeof(Program).Assembly,
                typeof(ErpSystem.Core.Services.TenantService).Assembly,
                typeof(ErpSystem.Data.ApplicationDbContext).Assembly);

            return services;
        }

        public static IServiceCollection AddErpSystemAuthorization(this IServiceCollection services)
        {
            services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
                StructuredAuthorizationMiddlewareResultHandler>();

            var authorizationBuilder = services.AddAuthorizationBuilder()
                .AddPolicy("SuperAdmin", policy =>
                    policy.RequireRole("SuperAdmin"))
                .AddPolicy("TenantAdmin", policy =>
                    policy.RequireRole("TenantAdmin", "SuperAdmin"))
                .AddPolicy("Manager", policy =>
                    policy.RequireRole("Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("Employee", policy =>
                    policy.RequireRole("Employee", "Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("ExternalOnly", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User?.Identity?.IsAuthenticated == true &&
                        ctx.User.IsInRole(Constants.Roles.ExternalUser)))
                .AddPolicy("SupplierApplicantOnly", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User?.Identity?.IsAuthenticated == true &&
                        ctx.User.HasClaim(claim =>
                            claim.Type == "supplier_applicant_session") &&
                        ctx.User.HasClaim(
                            "auth_provider", "ApplicantToken")))
                // The candidate-facing recruitment surface (api/candidate/*). Role-based like
                // ExternalOnly; CandidateAccessMiddleware is the outer fence that keeps the same
                // tokens off everything else.
                .AddPolicy("CandidateOnly", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User?.Identity?.IsAuthenticated == true &&
                        ctx.User.IsInRole(Constants.Roles.Candidate)))
                // The consultant-client surface (api/client-portal/*). Role-based like
                // CandidateOnly; ConsultantClientAccessMiddleware is the outer fence that keeps
                // the same tokens off everything else. Replaced the PortalBearer-scheme
                // "ConsultantClientPortal" policy 2026-08-31 when client contacts moved onto the
                // main JWT scheme (invite-only, no self-registration).
                .AddPolicy("ConsultantClientOnly", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User?.Identity?.IsAuthenticated == true &&
                        ctx.User.IsInRole(Constants.Roles.ConsultantClient)))
                // ⚠ InternalOnly is a BLOCKLIST, not an allowlist: every self-registered public
                // role must be named here or its holders satisfy the policy on every internal
                // endpoint that uses it. Candidate was added 2026-08-30 when candidates moved
                // onto the main JWT scheme — without it, a careers signup would have counted as
                // "internal" everywhere this policy guards. ConsultantClient followed 2026-08-31
                // for exactly the same reason (invited, but still a member of the public).
                .AddPolicy("InternalOnly", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User?.Identity?.IsAuthenticated == true &&
                        !ctx.User.IsInRole(Constants.Roles.ExternalUser) &&
                        !ctx.User.IsInRole(Constants.Roles.Candidate) &&
                        !ctx.User.IsInRole(Constants.Roles.ConsultantClient)))
                .AddPolicy("AuditGovernanceRead", policy =>
                    policy.Requirements.Add(new PermissionRequirement("audit.read", "procurement.audit.read")))
                .AddPolicy("SecurityManagementRead", policy =>
                    policy.Requirements.Add(new PermissionRequirement("audit.read", "settings.read", "settings.update")))
                .AddPolicy("AuditGovernanceManage", policy =>
                    policy.Requirements.Add(new PermissionRequirement("settings.update")))
                .AddPolicy("HrIdentityReconciliationRead", policy =>
                    policy.Requirements.Add(new PermissionRequirement("settings.read", "settings.update")))
                .AddPolicy("HrIdentityReconciliationManage", policy =>
                    policy.Requirements.Add(new PermissionRequirement("settings.update")))
                .AddPolicy("dashboard.read", policy =>
                    policy.Requirements.Add(new PermissionRequirement("dashboard.read")))
                .AddPolicy("Finance", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User?.Identity?.IsAuthenticated == true
                        && (
                            ctx.User.Claims.Any(c =>
                                string.Equals(c.Type, Constants.Claims.Module, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.Value, Constants.Modules.Finance, StringComparison.OrdinalIgnoreCase))
                            || ctx.User.IsInRole("Finance User")
                            || ctx.User.IsInRole("Manager")
                            || ctx.User.IsInRole("TenantAdmin")
                            || ctx.User.IsInRole("SuperAdmin")
                        )))
                .AddPolicy("HR", policy =>
                    policy.RequireClaim("module", "HR"))
                .AddPolicy("Sales", policy =>
                    policy.RequireClaim("module", "Sales"))
                .AddPolicy("Inventory", policy =>
                    policy.RequireClaim("module", "Inventory"))
                .AddPolicy("Procurement", policy =>
                    policy.RequireClaim("module", "Procurement"))
                .AddPolicy("Marketing", policy =>
                    policy.RequireClaim("module", "Marketing"))
                .AddPolicy("MaintenanceAccess", policy =>
                    policy.RequireRole("Employee", "Manager", "MaintenanceManager", "Maintenance Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("MaintenanceRead", policy =>
                    policy.RequireRole("Employee", "Manager", "MaintenanceManager", "Maintenance Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("MaintenanceWrite", policy =>
                    policy.RequireRole("Manager", "MaintenanceManager", "Maintenance Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("MaintenanceApprove", policy =>
                    policy.RequireRole("Manager", "MaintenanceManager", "Maintenance Manager", "TenantAdmin", "SuperAdmin"))
                // Fleet inspections are typically performed by drivers/employees, so allow Employee role to write inspections
                // without granting broader MaintenanceWrite permissions.
                .AddPolicy("FleetInspectionWrite", policy =>
                    policy.RequireRole("Employee", "Manager", "MaintenanceManager", "Maintenance Manager", "TenantAdmin", "SuperAdmin"))
                // The dedicated PortalBearer scheme and its "ConsultantClientPortal" policy were
                // retired 2026-08-31 with the bespoke consultant-client portal — its last tenant.
                // Client contacts now hold main-scheme tokens under the ConsultantClient role,
                // fenced by ConsultantClientAccessMiddleware and the "ConsultantClientOnly"
                // policy above (the "CandidatePortal" sibling went the same way 2026-08-30).
                ;

            authorizationBuilder
                .AddPolicy(FinancePermissions.ConfigureChartOfAccountsPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        FinancePermissions.AdministerFinance,
                        FinancePermissions.ManageChartOfAccounts)))
                .AddPolicy(FinancePermissions.ConfigureTaxPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        FinancePermissions.AdministerFinance,
                        FinancePermissions.ManageTaxConfiguration)))
                .AddPolicy(FinancePermissions.ConfigureFixedAssetCategoriesPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        FinancePermissions.AdministerFinance,
                        FinancePermissions.ManageFixedAssets)))
                .AddPolicy(FinancePermissions.WithdrawJournalApprovalPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        FinancePermissions.SubmitJournalEntries,
                        FinancePermissions.WorkflowCancel)))
                .AddPolicy(FinancePermissionPolicyMap.ProjectCurrencyLookupPolicy, policy =>
                    policy.RequireAuthenticatedUser().Requirements.Add(new PermissionRequirement(
                        FinancePermissions.ViewFinance, "project.access")))
                .AddPolicy(FinancePermissions.ViewTenderPaymentJournalPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        FinancePermissions.ViewFinance,
                        ProcurementAccessControlRegistry.TenderPaymentVerifyPermission)));

            foreach (var permission in FinancePermissions.All)
            {
                authorizationBuilder.AddPolicy(permission.Name, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission.Name)));
            }

            foreach (var permission in MobilePosPermissions.All.Where(permission => permission.Name != MobilePosPermissions.ViewStore))
            {
                authorizationBuilder.AddPolicy(permission.Name, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission.Name)));
            }

            // Store managers and device approvers need the same tenant-scoped reference and
            // store/till reads as a viewer. These remain capabilities assigned to dynamic roles;
            // no role name is embedded in the policy.
            authorizationBuilder.AddPolicy(MobilePosPermissions.ViewStore, policy =>
                policy.Requirements.Add(new PermissionRequirement(
                    MobilePosPermissions.ViewStore,
                    MobilePosPermissions.ManageStore,
                    MobilePosPermissions.ApproveDevice)));

            // CRM follows the same capability ladder as the other governed modules. Roles are
            // dynamic and are authorized by their assigned permissions; no Sales role name is
            // embedded here. A managing role can also open and read the workspace it manages.
            authorizationBuilder
                .AddPolicy(CrmPermissions.Access, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        CrmPermissions.Access,
                        CrmPermissions.Read,
                        CrmPermissions.Manage)))
                .AddPolicy(CrmPermissions.Read, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        CrmPermissions.Read,
                        CrmPermissions.Manage)))
                .AddPolicy(CrmPermissions.Manage, policy =>
                    policy.Requirements.Add(new PermissionRequirement(CrmPermissions.Manage)));

            // Sales roles are tenant-defined. These policies compose capabilities without
            // depending on a role name: higher capabilities include the lower read/access tier.
            authorizationBuilder
                .AddPolicy(SalesPermissions.Access, policy =>
                    policy.Requirements.Add(new PermissionRequirement(SalesPermissions.AllNames)))
                .AddPolicy(SalesPermissions.Read, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        SalesPermissions.Read,
                        SalesPermissions.Manage,
                        SalesPermissions.Approve,
                        SalesPermissions.Configure)))
                .AddPolicy(SalesPermissions.Manage, policy =>
                    policy.Requirements.Add(new PermissionRequirement(SalesPermissions.Manage)))
                .AddPolicy(SalesPermissions.Approve, policy =>
                    policy.Requirements.Add(new PermissionRequirement(SalesPermissions.Approve)))
                .AddPolicy(SalesPermissions.Configure, policy =>
                    policy.Requirements.Add(new PermissionRequirement(SalesPermissions.Configure)))
                .AddPolicy(SalesPermissions.ViewReports, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        SalesPermissions.ViewReports,
                        SalesPermissions.Manage)));

            // HR leave policies. The organisation-wide surface — every request,
            // everyone's balances, the adjustment ledger, the type catalogue's writes and the
            // year-end jobs — authorizes on these. Employee self-service (file, amend, cancel,
            // view OWN leave) deliberately does NOT: those stay on InternalOnly with a
            // self-or-permission ownership check at the endpoint, and approve/reject stay with
            // the workflow assignee, validated per request by CanUserApproveAsync.
            // Same ladder — Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.LeaveReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewLeave,
                        HrPermissions.MaintainLeave,
                        HrPermissions.AdministerLeave)))
                .AddPolicy(HrPermissions.LeaveWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainLeave,
                        HrPermissions.AdministerLeave)))
                .AddPolicy(HrPermissions.LeaveAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerLeave)));

            // HR attendance & time policies — attendance proper plus the consultant
            // timesheet/engagement/invoicing registers. Same split as leave: org-wide surfaces
            // authorize here, token-actor acts (punch, raising your own regularization) and own-
            // record reads stay on InternalOnly with ownership checks, and approvals stay with
            // the workflow assignee. Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.AttendanceReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewAttendance,
                        HrPermissions.MaintainAttendance,
                        HrPermissions.AdministerAttendance)))
                .AddPolicy(HrPermissions.AttendanceWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainAttendance,
                        HrPermissions.AdministerAttendance)))
                .AddPolicy(HrPermissions.AttendanceAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerAttendance)));

            // HR compensation & benefits policies: emoluments, pay components,
            // salary structure, benefit policies and enrollments. Money data follows the medical
            // shape — org-wide surfaces authorize here, an employee's own pay makeup, benefits,
            // dependents and beneficiaries are ownership checks on the endpoint, and the benefit
            // CATALOGUE reads stay open (an employee needs to see what benefits exist).
            // Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.CompensationReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewCompensation,
                        HrPermissions.MaintainCompensation,
                        HrPermissions.AdministerCompensation)))
                .AddPolicy(HrPermissions.CompensationWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainCompensation,
                        HrPermissions.AdministerCompensation)))
                .AddPolicy(HrPermissions.CompensationAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerCompensation)));

            // HR training & learning policies: programs, plans, schedules,
            // nominations, requests, completions, needs assessments, budgets, vendors, trainers,
            // learning paths, mentoring, compliance training and service bonds. Same split as the
            // earlier slices: org-wide surfaces authorize here, an employee's own training record
            // and self-service acts (nominate self, respond, withdraw) are ownership checks on
            // the endpoint, and workflow-validated approvals stay with the assignee.
            // Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.TrainingReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewTraining,
                        HrPermissions.MaintainTraining,
                        HrPermissions.AdministerTraining)))
                .AddPolicy(HrPermissions.TrainingWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainTraining,
                        HrPermissions.AdministerTraining)))
                .AddPolicy(HrPermissions.TrainingAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerTraining)));

            // HR recruitment policies: requisitions, vacancies, adverts, candidates,
            // applications, interviews, offers, hires and pre-employment checks. Same split as the
            // earlier slices: org-wide surfaces authorize here, a panelist's own interview surface
            // is a membership check on the endpoint, the anonymous surfaces (public job listings,
            // email-token offer responses and panelist confirms) keep their deliberate
            // AllowAnonymous + rate limits, and the candidate portal keeps its own scheme.
            // Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.RecruitmentReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewRecruitment,
                        HrPermissions.MaintainRecruitment,
                        HrPermissions.AdministerRecruitment)))
                .AddPolicy(HrPermissions.RecruitmentWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainRecruitment,
                        HrPermissions.AdministerRecruitment)))
                .AddPolicy(HrPermissions.RecruitmentAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerRecruitment)));
            foreach (var permission in ProcurementAccessControlRegistry.Permissions)
            {
                authorizationBuilder.AddPolicy(permission.Code, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission.Code)));
            }

            foreach (var permission in ErpSystem.Core.Services.QuantitySurvey.QuantitySurveyAccessControlRegistry.Permissions)
            {
                authorizationBuilder.AddPolicy(permission.Code, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission.Code)));
            }

            foreach (var permission in ErpSystem.Core.Services.Projects.CivilEngineeringAccessControlRegistry.Permissions)
            {
                authorizationBuilder.AddPolicy(permission.Code, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission.Code)));
            }

            // HR occupational-health policies. The medical controllers previously carried a bare
            // [Authorize], so every authenticated employee could read and delete medical records.
            // Administer implies Write implies Read, so an admin does not need all three granted.
            authorizationBuilder
                .AddPolicy(HrPermissions.MedicalReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewMedicalRecords,
                        HrPermissions.MaintainMedicalRecords,
                        HrPermissions.AdministerMedical)))
                .AddPolicy(HrPermissions.MedicalWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainMedicalRecords,
                        HrPermissions.AdministerMedical)))
                .AddPolicy(HrPermissions.MedicalAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerMedical)));

            // HR staff-travel policies. Same story as medical: all eight travel controllers
            // carried a bare [Authorize], so any authenticated employee could read colleagues'
            // passport and visa records, the cash-advance register and the expense-claim ledger.
            // Same ladder — Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.TravelReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewTravel,
                        HrPermissions.MaintainTravel,
                        HrPermissions.AdministerTravel)))
                .AddPolicy(HrPermissions.TravelWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainTravel,
                        HrPermissions.AdministerTravel)))
                .AddPolicy(HrPermissions.TravelAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerTravel)));

            // HR succession & talent policies. Measured 2026-08-18: all nine controllers carried a
            // bare [Authorize] with no method-level policy anywhere, so a plain Employee account
            // listed every succession plan in the tenant, read candidate readiness and
            // retention-risk flags, and both created and deleted a plan. Same ladder again —
            // Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.SuccessionReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewSuccession,
                        HrPermissions.MaintainSuccession,
                        HrPermissions.AdministerSuccession)))
                .AddPolicy(HrPermissions.SuccessionWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainSuccession,
                        HrPermissions.AdministerSuccession)))
                .AddPolicy(HrPermissions.SuccessionAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerSuccession)));

            // HR probation & confirmation policies. Measured 2026-08-18: ProbationController
            // carried a bare [Authorize], so any authenticated employee could list who in the
            // tenant is on probation, read their performance/conduct/attitude ratings and the
            // reviewer's comments, extend a probation period, and confirm or terminate it — the
            // last of those decides whether a colleague's employment becomes permanent. Same
            // ladder again — Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.ProbationReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewProbation,
                        HrPermissions.MaintainProbation,
                        HrPermissions.AdministerProbation)))
                .AddPolicy(HrPermissions.ProbationWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainProbation,
                        HrPermissions.AdministerProbation)))
                .AddPolicy(HrPermissions.ProbationAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerProbation)));

            // HR job architecture, competency and manpower budget policies.
            // Measured 2026-08-19: all five controllers — JobAnalysis, hr/job-architecture,
            // competencies, employee-competencies, position-competencies — carried a bare
            // [Authorize] across 149 endpoints. Any authenticated employee could read every
            // colleague's competency assessment and gap analysis, rewrite an approved job
            // description, and approve a manpower budget. Three families rather than one because
            // the audiences differ: HR authors job descriptions, line managers assess
            // competencies, and budget holders own the establishment. Same ladder throughout —
            // Administer implies Write implies Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.JobArchitectureReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewJobArchitecture,
                        HrPermissions.MaintainJobArchitecture,
                        HrPermissions.AdministerJobArchitecture)))
                .AddPolicy(HrPermissions.JobArchitectureWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainJobArchitecture,
                        HrPermissions.AdministerJobArchitecture)))
                .AddPolicy(HrPermissions.JobArchitectureAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerJobArchitecture)))
                .AddPolicy(HrPermissions.CompetencyReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewCompetency,
                        HrPermissions.MaintainCompetency,
                        HrPermissions.AdministerCompetency)))
                .AddPolicy(HrPermissions.CompetencyWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainCompetency,
                        HrPermissions.AdministerCompetency)))
                .AddPolicy(HrPermissions.CompetencyAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerCompetency)))
                .AddPolicy(HrPermissions.ManpowerBudgetReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewManpowerBudget,
                        HrPermissions.MaintainManpowerBudget,
                        HrPermissions.AdministerManpowerBudget)))
                .AddPolicy(HrPermissions.ManpowerBudgetWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainManpowerBudget,
                        HrPermissions.AdministerManpowerBudget)))
                .AddPolicy(HrPermissions.ManpowerBudgetAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerManpowerBudget)));

            // HR separation, clearance & exit policies. Registered ahead of the
            // controllers so the seed lands before any gate goes on — permissions resolve from the
            // database, so gating first would 403 every endpoint for all but SuperAdmin.
            //
            // Same ladder — Administer implies Write implies Read — but note what is deliberately
            // NOT here. FR-HR-092 (the MD signs every non-procedural termination) and FR-HR-185
            // (Internal Audit reviews the settlement before payment) are anchored on
            // Constants.Roles.ManagingDirectorAny and Constants.Roles.InternalAudit and read off
            // the record, not off this family: HR holds Read and Write, and must never be able to
            // sign off its own terminations or release its own payments.
            authorizationBuilder
                .AddPolicy(HrPermissions.SeparationReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewSeparation,
                        HrPermissions.MaintainSeparation,
                        HrPermissions.AdministerSeparation)))
                .AddPolicy(HrPermissions.SeparationWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainSeparation,
                        HrPermissions.AdministerSeparation)))
                .AddPolicy(HrPermissions.SeparationAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerSeparation)))
                // Finance's valuation of the pay HR records (leave settings audit 2, P2). Not on
                // the separation ladder: Administer Separation must NOT imply it — the one who
                // counts is not the one who prices.
                .AddPolicy(HrPermissions.PayValuePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ValueHrPay)));

            // Staff Awards & Recognition. Same ladder: Administer implies Write implies
            // Read. What is deliberately NOT gated on this family is the employee's own surface:
            // nominating a colleague and voting for a nominee are acts every employee performs, so
            // they live on the self-service controller behind bare [Authorize] with the actor taken
            // from the token. Gating them here would lock the whole workforce out of the feature
            // the area exists for — the area-15b trap, where a permission gate was used for an
            // actor who is defined by the record rather than by a grant.
            authorizationBuilder
                .AddPolicy(HrPermissions.AwardsReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewAwards,
                        HrPermissions.MaintainAwards,
                        HrPermissions.AdministerAwards)))
                .AddPolicy(HrPermissions.AwardsWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainAwards,
                        HrPermissions.AdministerAwards)))
                .AddPolicy(HrPermissions.AwardsAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerAwards)));

            // Performance. Same ladder: Administer implies Write implies Read.
            // Deliberately NOT gated on this family: every token-actor self surface (self/manager/
            // peer evaluation, acknowledge, appeals, own goals and journal), goal approve/reject
            // (the goal workflow validates the direct manager per goal), PIP approve/reject (the
            // workflow engine validates the assignee per instance), and the PIP authoring role gate
            // (managers author PIPs for their reports — a permission would lock every line manager
            // out of the corrective surface).
            authorizationBuilder
                .AddPolicy(HrPermissions.PerformanceReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewPerformance,
                        HrPermissions.MaintainPerformance,
                        HrPermissions.AdministerPerformance)))
                .AddPolicy(HrPermissions.PerformanceWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainPerformance,
                        HrPermissions.AdministerPerformance)))
                .AddPolicy(HrPermissions.PerformanceAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerPerformance)))
                // The pay and employment proposals: the desk's Read ladder, or the Managing Director, who decides
                // them (F3, D-94) and holds no other performance permission.
                .AddPolicy(HrPermissions.PerformanceProposalsReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewPerformance,
                        HrPermissions.MaintainPerformance,
                        HrPermissions.AdministerPerformance,
                        HrPermissions.ViewPerformanceProposals)));

            // Employee records & foundation. Same ladder. Deliberately NOT gated on
            // this family: the lean directory reads that feed the shared employee picker (POST
            // paged and the by-unit/level/location, direct-report, management-chain, by-id and
            // by-number reads all return the summary EmployeeDto — no DOB, pay or identifiers),
            // the aggregate stats tiles on the HR landing page, the dead technician routes
            // (in-process maintenance consumers bypass HTTP), the foundation reference reads
            // (org/location structures, positions lists, lookups — pickers feed every module),
            // and the whole employee portal (token-actor by construction, service-enforced
            // self-or-HR). The PII line: {id}/details and {id}/profile and every per-employee
            // sub-record read carry salary, tax/SSN, address and bank data — those are Read.
            authorizationBuilder
                .AddPolicy(HrPermissions.EmployeeReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewEmployees,
                        HrPermissions.MaintainEmployees,
                        HrPermissions.AdministerEmployees)))
                .AddPolicy(HrPermissions.EmployeeWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainEmployees,
                        HrPermissions.AdministerEmployees)))
                .AddPolicy(HrPermissions.EmployeeAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerEmployees)));

            // SHE. Same ladder, converted from the area's SuperAdmin/HR role gates
            // by verb (reads → Read, writes and every desk decision → Write, deletes → Admin), so
            // the SHE desk keeps its exact reach and a future SHE-officer role can be granted the
            // family without the HR role. Deliberately NOT gated: incident,
            // hazard and environmental-incident reporting, stop-work raise, PPE and stop-work
            // "mine" reads, and risk-assessment acknowledgements — any-internal-actor by design.
            // The two SHE health controllers stay on the HR.Medical.* policies (the slice-9
            // ownership boundary), not this family.
            authorizationBuilder
                .AddPolicy(HrPermissions.SheReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewShe,
                        HrPermissions.MaintainShe,
                        HrPermissions.AdministerShe)))
                .AddPolicy(HrPermissions.SheWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainShe,
                        HrPermissions.AdministerShe)))
                .AddPolicy(HrPermissions.SheAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerShe)));

            // The four mechanically converted areas (orientation, assets, movements,
            // discipline & grievance). Every endpoint gate in these areas was HR-desk-shaped
            // (SuperAdmin/HR variants, no decision roles at the attribute level), so the conversion
            // is verb-mechanical per family: reads → Read, desk ops → Write, deletes → Admin. The
            // areas' deliberate opens (self-service acknowledge/respond/mine surfaces, the employee
            // portal) are untouched, and the record-level actor rules (movement workflow assignees,
            // discipline natural justice) stay in the services where their areas put them.
            authorizationBuilder
                .AddPolicy(HrPermissions.OrientationReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewOrientation,
                        HrPermissions.MaintainOrientation,
                        HrPermissions.AdministerOrientation)))
                .AddPolicy(HrPermissions.OrientationWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainOrientation,
                        HrPermissions.AdministerOrientation)))
                .AddPolicy(HrPermissions.OrientationAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerOrientation)))
                .AddPolicy(HrPermissions.AssetsReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewAssets,
                        HrPermissions.MaintainAssets,
                        HrPermissions.AdministerAssets)))
                .AddPolicy(HrPermissions.AssetsWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainAssets,
                        HrPermissions.AdministerAssets)))
                .AddPolicy(HrPermissions.AssetsAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerAssets)))
                .AddPolicy(HrPermissions.MovementsReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewMovements,
                        HrPermissions.MaintainMovements,
                        HrPermissions.AdministerMovements)))
                .AddPolicy(HrPermissions.MovementsWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainMovements,
                        HrPermissions.AdministerMovements)))
                .AddPolicy(HrPermissions.MovementsAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerMovements)))
                .AddPolicy(HrPermissions.DisciplineReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewDiscipline,
                        HrPermissions.MaintainDiscipline,
                        HrPermissions.AdministerDiscipline)))
                .AddPolicy(HrPermissions.DisciplineWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainDiscipline,
                        HrPermissions.AdministerDiscipline)))
                .AddPolicy(HrPermissions.DisciplineAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerDiscipline)));

            // The company/administration tail: the company profile, the HR policy
            // settings, the external-associate register and the (dormant, UI-less) company
            // schedule. Verb-mechanical as everywhere — reads → Read, desk work → Write,
            // deletes → Admin — with one deliberate exception: writing the HR policy settings is
            // Admin, not Write, because its knobs move trust boundaries (the procedural-absence
            // threshold of FR-HR-092, the enforcement modes of FR-HR-136) and the previous role
            // gate held that write to SuperAdmin/TenantAdmin. HR's Read+Write grant preserves the
            // desk's exact reach; granting HR the Admin permission is TDC's one-line change if it
            // ever wants HR to hold those knobs.
            authorizationBuilder
                .AddPolicy(HrPermissions.CompanyReadPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.ViewCompany,
                        HrPermissions.MaintainCompany,
                        HrPermissions.AdministerCompany)))
                .AddPolicy(HrPermissions.CompanyWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.MaintainCompany,
                        HrPermissions.AdministerCompany)))
                .AddPolicy(HrPermissions.CompanyAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        HrPermissions.AdministerCompany)));

            foreach (var permission in HrPermissions.All)
            {
                authorizationBuilder.AddPolicy(permission.Name, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission.Name)));
            }

            // Shared cross-module reference data (administrative geography). Its own family
            // because the data is not HR's — see ReferenceDataPermissions. There is no Read
            // policy on purpose: every module's address form lists regions, so reads are
            // InternalOnly.
            authorizationBuilder
                .AddPolicy(ReferenceDataPermissions.GeographyWritePolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        ReferenceDataPermissions.MaintainGeography,
                        ReferenceDataPermissions.AdministerGeography)))
                .AddPolicy(ReferenceDataPermissions.GeographyAdminPolicy, policy =>
                    policy.Requirements.Add(new PermissionRequirement(
                        ReferenceDataPermissions.AdministerGeography)));

            foreach (var permission in ReferenceDataPermissions.All)
            {
                authorizationBuilder.AddPolicy(permission.Name, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission.Name)));
            }

            // Second handler for PermissionRequirement: keeps existing HR roles working on tenants
            // provisioned before the HR permission seed. Handlers are OR-ed, so this widens nothing
            // that the database-backed handler already decides.
            services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
                HrPermissionRoleFallbackAuthorizationHandler>();

            return services;
        }
        private static readonly string[] stringArray = new[] { "🔐 Authentication & Security" };

        public static IServiceCollection AddErpSystemApi(this IServiceCollection services)
        {
            // Custom middleware registered as IMiddleware
            services.AddTransient<ErpSystem.Api.Middleware.ExternalUserAccessMiddleware>();
            services.AddTransient<ErpSystem.Api.Middleware.CandidateAccessMiddleware>();
            services.AddTransient<ErpSystem.Api.Middleware.ConsultantClientAccessMiddleware>();
            services.AddScoped<ErpSystem.Api.Filters.SystemExceptionResultLoggingFilter>();

            services.AddControllers(options =>
                {
                    options.Conventions.Add(new FinancePermissionAuthorizationConvention());
                    options.Filters.AddService<ErpSystem.Api.Filters.SystemExceptionResultLoggingFilter>();

                    // [Required] on a non-nullable Guid accepts Guid.Empty, so an
                    // omitted foreign key passed validation and died on the FK as a 500 naming
                    // nothing. HR DTOs only — see HrRequiredGuidActionFilter.
                    options.Filters.Add<ErpSystem.Api.Filters.HrRequiredGuidActionFilter>();
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.WriteIndented = true;
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

            // TODO: Add API versioning later
            // services.AddApiVersioning(...);
            // services.AddVersionedApiExplorer(...);

            // Configure Swagger/OpenAPI
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ERP System API",
                    Version = "v1",
                    Description = @"A comprehensive ERP system API with multi-tenant architecture.

**Authentication:**
1. Use POST /api/auth/login to get a JWT token
2. Copy the token from the response
3. Click 'Authorize' button below
4. Enter: Bearer [your-token] (include 'Bearer ' prefix)
5. Click 'Authorize' and 'Close'
6. Now you can test all protected endpoints!

**Available Test User:**
- Username: admin@system.com
- Password: Admin123!",
                    Contact = new OpenApiContact
                    {
                        Name = "ERP System API",
                        Email = "admin@system.com"
                    }
                });

                // Fix schema ID conflicts by using fully qualified names
                c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

                // Add JWT Authentication to Swagger
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme.
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer 12345abcdef'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // Enable XML comments if available
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

                 // Group controllers by modules/tags for better organization
                 c.TagActionsBy(api =>
                 {
                     var controllerName = api.ActionDescriptor.RouteValues["controller"];

                     // Categorize by module
                     return controllerName switch
                     {
                         // Enquiry, Helpdesk & Complaints (EHC) Module
                         string name when !string.IsNullOrWhiteSpace(name) && name.StartsWith("Ehc", StringComparison.OrdinalIgnoreCase)
                             => new[] { "🎫 Enquiry, Helpdesk & Complaints" },

                         // Authentication & Authorization
                         "Auth" or "Security" or "Users" or "Roles" or "Permissions" => stringArray,

                         // Administration
                         "Tenants" or "Settings" or "AuditLogs" or "SecurityLogs" => new[] { "⚙️ Administration" },

                        // HR Module
                        "Employees" or "Departments" or "Sections" or "EmployeeSkills" => new[] { "👥 Human Resources" },

                        // Maintenance Module
                        "Assets" or "WorkOrders" or "Maintenance" or "AssetCategories" or "WorkOrderTypes" or "MaintenanceTypes" or "PriorityLevels" => new[] { "🔧 Maintenance" },

                        // Procurement Module
                        "PurchaseOrders" or "Suppliers" or "PurchaseRequisitions" or "Procurement" => new[] { "🛒 Procurement" },

                        // Inventory Module
                        "Inventory" or "InventoryItems" or "StockMovements" or "Warehouses" => new[] { "📦 Inventory" },

                        // Workflow Module
                        "Workflows" or "WorkflowDefinitions" or "WorkflowInstances" or "WorkflowSteps" or "WorkflowApprovals" => new[] { "🔄 Workflow" },

                        // Reports Module
                        "Reports" or "ReportTemplates" or "ReportSchedules" or "ReportExecutions" => new[] { "📊 Reports" },

                        // Data Sources
                        "DataSources" or "DatabaseMetadata" => new[] { "🗃️ Data Sources" },

                        // Development & Testing
                        "Development" or "DevInfo" or "Features" or "SampleData" => new[] { "🔬 Development" },

                        // System Health
                        "Health" or "System" or "Monitoring" => new[] { "💚 System Health" },

                        // Dashboard
                        "Dashboard" or "Analytics" => new[] { "📈 Dashboard" },

                        // File Management
                        "Files" or "Storage" or "Upload" => new[] { "📁 File Management" },

                        // Default fallback
                        _ => new[] { $"📋 {controllerName}" }
                    };
                });
                c.DocInclusionPredicate((name, api) => true);
            });

            return services;
        }


        public static IServiceCollection AddErpSystemLogging(this IServiceCollection services, IConfiguration configuration)
        {
            var loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName();

            // The VPS deploy preserves server settings and may start without an appsettings file.
            // Keep errors visible through the service's captured stdout in that case.
            if (!configuration.GetSection("Serilog:WriteTo").Exists())
            {
                if (!configuration.GetSection("Serilog:MinimumLevel").Exists())
                    loggerConfiguration.MinimumLevel.Warning();
                loggerConfiguration.WriteTo.Console();
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            services.AddSerilog();

            return services;
        }
        private static readonly string[] tags = new[] { "db", "sql", "ready" };

        public static IServiceCollection AddErpSystemHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var redisConnectionString = configuration.GetConnectionString("Redis");

            var healthChecksBuilder = services.AddHealthChecks()
                // Liveness must prove only that the API process can respond. Dependency failures
                // belong to readiness; including SQL/Redis here can create a restart storm during
                // a recoverable infrastructure outage.
                .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running"),
                    tags: new[] { "live" })
                .AddCheck("startup", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API started successfully"),
                    tags: new[] { "ready" });

            // Add database health check
            if (!string.IsNullOrEmpty(connectionString))
            {
                healthChecksBuilder.AddDbContextCheck<ErpSystem.Data.ApplicationDbContext>("database",
                    tags: tags);
            }

            // Add Redis health check if configured
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddScoped<RedisHealthCheck>();
                healthChecksBuilder.AddCheck<RedisHealthCheck>("redis", tags: new[] { "cache", "redis", "ready" });
                services.Configure<RedisHealthCheckOptions>(options =>
                {
                    options.ConnectionString = redisConnectionString;
                });
            }

            // Add memory health check
            healthChecksBuilder.AddCheck("memory", () =>
            {
                var gc = GC.GetTotalMemory(false);
                var workingSet = Environment.WorkingSet;

                if (workingSet > 1024 * 1024 * 1024) // > 1GB
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"High memory usage: {workingSet / 1024 / 1024} MB");
                }

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    $"Memory usage: {workingSet / 1024 / 1024} MB");
            }, tags: new[] { "memory", "performance" });

            return services;
        }

        public static IServiceCollection AddErpSystemCaching(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();

            // Check if Redis is configured
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                var redisConfigurationOptions = BuildRedisConfigurationOptions(redisConnectionString, configuration);

                // Add Redis connection multiplexer for advanced operations
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(redisConfigurationOptions));

                // Add StackExchange Redis cache
                services.AddStackExchangeRedisCache(options =>
                {
                    options.ConfigurationOptions = redisConfigurationOptions;
                    options.InstanceName = "ErpSystem";
                });

                // Register Redis service with connection multiplexer
                services.AddScoped<IRedisService>(sp =>
                    new RedisService(
                        sp.GetRequiredService<IDistributedCache>(),
                        sp.GetRequiredService<ILogger<RedisService>>(),
                        sp.GetRequiredService<IConnectionMultiplexer>()));
            }
            else
            {
                services.AddDistributedMemoryCache();

                // Register Redis service without connection multiplexer (fallback mode)
                services.AddScoped<IRedisService>(sp =>
                    new RedisService(
                        sp.GetRequiredService<IDistributedCache>(),
                        sp.GetRequiredService<ILogger<RedisService>>()));
            }

            // Add response caching
            services.AddResponseCaching(options =>
            {
                options.MaximumBodySize = 64 * 1024; // 64KB
                options.SizeLimit = 50 * 1024 * 1024; // 50MB
            });

            return services;
        }

        private static ConfigurationOptions BuildRedisConfigurationOptions(string connectionString, IConfiguration configuration)
        {
            var options = ConfigurationOptions.Parse(connectionString, ignoreUnknown: true);
            var environmentName =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                configuration["Application:EnvironmentName"] ??
                configuration["Application:Environment"] ??
                Environments.Production;

            var isDevelopment = string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);

            if (isDevelopment)
            {
                // Let local startup continue even if Redis is not installed or not running yet.
                options.AbortOnConnectFail = false;
                options.ConnectRetry = Math.Max(options.ConnectRetry, 3);
                options.ReconnectRetryPolicy ??= new ExponentialRetry(5000);
            }

            return options;
        }

        public static IServiceCollection AddErpSystemWebFarm(this IServiceCollection services, IConfiguration configuration)
        {
            var dataProtectionKeysPath = configuration["DataProtection:KeysPath"];
            var redisConnectionString = configuration.GetConnectionString("Redis");

            var dataProtectionBuilder = services.AddDataProtection();

            if (!string.IsNullOrEmpty(dataProtectionKeysPath))
            {
                // TODO: Add Microsoft.AspNetCore.DataProtection.Extensions package
                // dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
            }
            else if (!string.IsNullOrEmpty(redisConnectionString))
            {
                // TODO: Store data protection keys in Redis for web farm scenarios
                // dataProtectionBuilder.PersistKeysToStackExchangeRedis(...);
            }

            return services;
        }

        public static IServiceCollection AddErpSystemSearch(this IServiceCollection services, IConfiguration configuration)
        {
            // Add search capabilities
            services.AddScoped<ISearchService, SearchService>();

            var elasticSearchUrl = configuration["Search:ElasticsearchUrl"];
            if (!string.IsNullOrEmpty(elasticSearchUrl))
            {
                // TODO: Add Elasticsearch configuration when implementing advanced search
                // services.AddElasticsearch(elasticSearchUrl);
            }

            return services;
        }

        public static IServiceCollection AddErpSystemLifecycle(this IServiceCollection services)
        {
            // Graceful shutdown and resource cleanup
            services.AddSingleton<IResourceCleanupService, ResourceCleanupService>();
            services.AddSingleton<IGracefulShutdownService, GracefulShutdownService>();
            services.AddHostedService<GracefulShutdownService>(serviceProvider =>
                (GracefulShutdownService)serviceProvider.GetRequiredService<IGracefulShutdownService>());

            return services;
        }

        public static IServiceCollection AddDevelopmentServices(this IServiceCollection services, IWebHostEnvironment environment)
        {
            // Always register these services, but they will behave differently based on environment
            services.AddSingleton<IFeatureService, SimpleFeatureService>();
            services.AddScoped<IDevInfoService, DevInfoService>();

            // Add database seeding service
            services.AddDatabaseSeeding();

            return services;
        }

        /// <summary>
        /// Add file upload services and configuration
        /// </summary>
        public static IServiceCollection AddErpSystemFileUpload(this IServiceCollection services, IConfiguration configuration)
        {
            // Legacy - kept for backward compatibility
            services.Configure<ErpSystem.Api.Controllers.FileUploadOptions>(configuration.GetSection(ErpSystem.Api.Controllers.FileUploadOptions.SectionName));

            // Configure new storage system
            services.Configure<StorageProviderOptions>(configuration.GetSection(StorageProviderOptions.SectionName));

            // Add storage services
            services.AddStorageServices();

            services.AddOptions<ErpSystem.Api.Services.ClamAvVirusScanOptions>()
                .Bind(configuration.GetSection(
                    ErpSystem.Api.Services.ClamAvVirusScanOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Host),
                    "FileVirusScan:ClamAv:Host is required.")
                .Validate(
                    options => options.Port is > 0 and <= 65535,
                    "FileVirusScan:ClamAv:Port must be between 1 and 65535.")
                .Validate(
                    options => options.ConnectTimeoutSeconds is >= 1 and <= 60,
                    "ClamAV connect timeout must be between 1 and 60 seconds.")
                .Validate(
                    options => options.ScanTimeoutSeconds is >= 1 and <= 600,
                    "ClamAV scan timeout must be between 1 and 600 seconds.")
                .Validate(
                    options => options.ChunkSizeBytes is >= 1024 and <= 1024 * 1024,
                    "ClamAV chunk size must be between 1 KiB and 1 MiB.")
                .Validate(
                    options => options.MaximumResponseBytes is >= 1024 and <= 1024 * 1024,
                    "ClamAV maximum response size must be between 1 KiB and 1 MiB.")
                .ValidateOnStart();

            // Central malware-scanning boundary. TryAdd keeps a real custom
            // provider registered by the host from being overwritten.
            services.TryAddSingleton<IFileVirusScanService,
                ErpSystem.Api.Services.ClamAvFileVirusScanService>();
            services.AddHealthChecks()
                .AddCheck<ErpSystem.Api.Services.FileVirusScanHealthCheck>(
                    "file-virus-scanner",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: ["ready", "security"]);
            services.AddScoped<IControlledFileUploadService,
                ErpSystem.Api.Services.ControlledFileUploadService>();
            services.AddScoped<ErpSystem.Api.Services.FileStorageCleanupProcessor>();
            services.AddHostedService<
                ErpSystem.Api.Services.FileStorageCleanupBackgroundService>();

            // Single entry point every HR upload site goes through, so the
            // upload -> DMS-register -> compensate-on-failure sequence is written once.
            services.AddScoped<ErpSystem.Api.Services.HR.IHrControlledDocumentService,
                ErpSystem.Api.Services.HR.HrControlledDocumentService>();

            // Reclaims public CV uploads that were never attached to an application.
            services.AddHostedService<ErpSystem.Api.Services.HR.PublicCvUploadTicketSweeper>();

            // SHE reminder engine: hourly sweep — permit auto-expiry,
            // due-date reminder ladders, tiered escalation. Sweep logic is scoped
            // (ISheReminderService) so the run-now endpoint shares it.
            services.AddHostedService<ErpSystem.Api.Services.HR.SheReminderBackgroundService>();

            // Staff movement reminder engine: daily sweep — assignments ending,
            // returns and approvals overdue, effective dates reached with nobody implementing.
            // Sweep logic is scoped (IStaffMovementReminderService) so run-now shares it.
            services.AddHostedService<ErpSystem.Api.Services.HR.StaffMovementReminderBackgroundService>();

            // Discipline reminder engine: daily sweep — the 48-hour written query,
            // the four-week investigation, hearings coming up, both appeal windows, corrective
            // actions, expiring warnings, unpaid fines, and grievances at an unanswered rung.
            // Sweep logic is scoped (IDisciplineReminderService) so run-now shares it.
            services.AddHostedService<ErpSystem.Api.Services.HR.DisciplineReminderBackgroundService>();

            // Probation reminder engine: daily sweep — FR-HR-032's confirmation
            // form a month before the end, FR-HR-140's expiry notice at the tenant's lead time, a
            // probation past its end date with no outcome recorded, overdue reviews, and reviews the
            // employee has never acknowledged. Sweep logic is scoped (IProbationReminderService) so
            // run-now shares it.
            services.AddHostedService<ErpSystem.Api.Services.HR.ProbationReminderBackgroundService>();
            services.AddHostedService<ErpSystem.Api.Services.HR.IdentificationExpiryReminderBackgroundService>();
            services.AddHostedService<ErpSystem.Api.Services.HR.CertificationExpiryReminderBackgroundService>();

            // Sweep logic is scoped (IStaffTravelReminderService) so run-now shares it.
            services.AddHostedService<ErpSystem.Api.Services.HR.StaffTravelReminderBackgroundService>();

            // Leave reminder engine (closure plan wave E, slice E2): daily sweep — approved leave
            // about to start with nobody confirming it is still going, leave that ended and was
            // never closed, a request nobody has decided, mandatory leave still outstanding late in
            // the year, and carry-over about to lapse. Sweep logic is scoped (ILeaveReminderService)
            // so run-now shares it.
            //
            // ⚠ This engine WARNS ONLY. LeaveYearEndService, which actually moves carry-over and
            // forfeiture, stays deliberately unhosted — those two acts change people's entitlements
            // and automating them is TDC's policy call, not a defect to fix.
            services.AddHostedService<ErpSystem.Api.Services.HR.LeaveReminderBackgroundService>();

            // Separation reminder engine (FR-HR-111): daily sweep — a retirement
            // or a contract expiry approaching with no exit raised, a clearance with mandatory lines
            // unanswered, a settlement sitting with Internal Audit, and a settlement approved and
            // never completed. Sweep logic is scoped (ISeparationReminderService) so run-now shares
            // it. ⚠ Until this registration the engine ran only when somebody pressed the button,
            // which is what FR-HR-093's "advance alerts" actually depended on.
            services.AddHostedService<ErpSystem.Api.Services.HR.SeparationReminderBackgroundService>();

            // Asset reminder engine: daily sweep — maintenance due within
            // the horizon, maintenance already overdue on the escalation ladder, and assets
            // that require regular servicing with no next date at all. Sweep logic is scoped
            // (IAssetReminderService) so run-now shares it.
            services.AddHostedService<ErpSystem.Api.Services.HR.AssetReminderBackgroundService>();

            // Recruitment lifecycle sweep (G-2.4, G-6.2): daily — an offer whose expiry passed
            // unanswered, an advert past its closing date, and an anticipated vacancy whose day has
            // come. ⚠ This is the recruitment module's FIRST scheduled job: before it, no offer was
            // ever marked Expired and no advert ever closed itself, so every counter built on those
            // statuses drifted without bound. Sweep logic is scoped
            // (IRecruitmentLifecycleSweepService) so run-now shares it.
            services.AddHostedService<ErpSystem.Api.Services.HR.RecruitmentLifecycleSweepBackgroundService>();

            // Orientation triggers (round 4, lane I3): nightly — scheduled audience rules, plus the
            // dated ones (hire, transfer, promotion) whose day has come or whose event hook failed.
            // Before this, OrientationEnrollmentTrigger was written by the seeder and read by
            // nothing. Logic is scoped (IOrientationEnrollmentTriggerService.RunSweepForTenantAsync)
            // so POST api/orientation-programs/triggers/run shares it.
            // ⚠ Registered in the same change as the service, because two HR sweeps here were not.
            services.AddHostedService<ErpSystem.Api.Services.HR.OrientationTriggerBackgroundService>();

            // Orientation & onboarding reminders (round 4, lane K): daily — onboarding tasks due soon,
            // overdue or awaiting sign-off; orientations due soon, overdue, or waiting on an assessment
            // or an acknowledgement; certificates expiring. The first HR sweep that DELIVERS: one
            // in-app notification per person, emailed too. Logic is scoped
            // (IOnboardingOrientationReminderService) so POST api/orientation-reminders/run shares it.
            // ⚠ Registered in the same change as the service (lane K5) — two HR sweeps here were not.
            services.AddHostedService<ErpSystem.Api.Services.HR.OnboardingOrientationReminderBackgroundService>();

            // Orientation & onboarding notice emails (round 4, lane K-b): every minute, sends what the
            // lifecycle notices queued — enrolled, a session placed, moved or called off, completed,
            // certificate issued. The notice itself is written with the event; the email follows.
            // Logic is scoped (IOrientationNoticeEmailDispatcher), shared with
            // POST api/orientation-notifications/send-queued. ⚠ Registered with its class.
            services.AddHostedService<ErpSystem.Api.Services.HR.OrientationNoticeEmailBackgroundService>();

            // Company schedule reminders (round 4, lane N-b2): hourly — each event's reminder the days
            // before its form asks for, and the chase of unanswered invitations ahead of the RSVP
            // deadline, each ONCE (stamped on the event). Logic is scoped
            // (ICompanyEventService.SendDueRemindersAsync), shared with
            // POST api/CompanySchedule/reminders/run. ⚠ Registered with its class.
            services.AddHostedService<ErpSystem.Api.Services.HR.CompanyScheduleReminderBackgroundService>();

            // Team reminder engine (round 2, lane F2): daily sweep — a task due within the tenant's
            // lead time, a task already overdue, an objective past its date, a meeting tomorrow, and
            // terms of reference lapsing within thirty days. Sweep logic is scoped
            // (ITeamReminderService) so run-now shares it.
            // ⚠ Without THIS LINE the engine runs only when somebody presses the button — which is
            // exactly what happened to two other HR sweeps here, unnoticed, for months.
            services.AddHostedService<ErpSystem.Api.Services.HR.TeamReminderBackgroundService>();

            // Employee bulk-import committer: polls for sessions HR has confirmed and writes them
            // in batches, one fresh scope per batch. There is no job queue in this API, so the
            // session row IS the work item (Status = CommitRequested). Logic is scoped
            // (IEmployeeImportService.CommitBatchAsync); the host only paces and locks.
            services.AddHostedService<ErpSystem.Api.Services.HR.EmployeeImport.EmployeeImportCommitBackgroundService>();

            // Durable delivery for emails an account is unusable without (portal verification).
            services.AddScoped<ErpSystem.Core.Interfaces.Common.ITransactionalEmailQueue,
                ErpSystem.Api.Services.TransactionalEmailQueue>();

            // One-off adoption of pre-boundary HR files; driven by the SuperAdmin-only endpoint.
            services.AddScoped<ErpSystem.Api.Services.HR.HrLegacyFileMigrationService>();

            // Configure multipart body length limit for file uploads
            services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                // Set limits for file uploads
                options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
                options.ValueLengthLimit = 50 * 1024 * 1024; // 50MB
                options.MultipartBoundaryLengthLimit = 128;
                options.MultipartHeadersCountLimit = 16;
                options.MultipartHeadersLengthLimit = 16384;
            });

            return services;
        }

        public static IApplicationBuilder UseSimpleDevelopmentMiddleware(this IApplicationBuilder app, IWebHostEnvironment environment)
        {
            if (environment.IsDevelopment())
            {
                // Add development-specific middleware
                app.UseMiddleware<SimpleDevMiddleware>();
            }

            // Security headers middleware for all environments
            app.UseMiddleware<DevSecurityHeadersMiddleware>();

            return app;
        }

        public static IServiceCollection AddErpSystemSignalR(this IServiceCollection services)
        {
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
                options.StreamBufferCapacity = 10;
                options.MaximumParallelInvocationsPerClient = 2;

                // Configure timeouts - more lenient for reconnections
                options.ClientTimeoutInterval = TimeSpan.FromMinutes(5); // Increase from 60s to 5 minutes
                options.HandshakeTimeout = TimeSpan.FromSeconds(60); // Increase from 30s to 60s
                options.KeepAliveInterval = TimeSpan.FromSeconds(30); // Increase from 15s to 30s

                // Add connection state management
                options.StatefulReconnectBufferSize = 1000;
            });

            // Register Hub notification service
            services.AddScoped<ErpSystem.Core.Interfaces.IHubNotificationService, ErpSystem.Api.Services.HubNotificationService>();

            return services;
        }
        private static readonly string[] collection = new[]
                {
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://localhost:3001",
                    "https://localhost:3001",
                    "http://localhost:4200",
                    "https://localhost:4200",
                    "http://127.0.0.1:3000",
                    "https://127.0.0.1:3000"
                };

        public static IServiceCollection AddErpSystemCors(this IServiceCollection services, IConfiguration configuration)
        {
            // Get allowed origins from environment variables or configuration
            var allowedOriginsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
            var allowedOrigins = new List<string>();

            if (!string.IsNullOrEmpty(allowedOriginsEnv))
            {
                allowedOrigins.AddRange(allowedOriginsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrEmpty(o)));
            }
            else
            {
                // Fallback to configuration
                var configOrigins = configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
                if (configOrigins != null && configOrigins.Length > 0)
                {
                    allowedOrigins.AddRange(configOrigins.Where(o => !string.IsNullOrEmpty(o)));
                }
            }

            // If no origins configured, use development defaults
            if (!allowedOrigins.Any())
            {
                allowedOrigins.AddRange(collection);
            }

            services.AddCors(options =>
            {
                options.AddPolicy("ErpSystemCorsPolicy", policy =>
                {
                    // Check if we're in development mode for more permissive CORS
                    var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

                    if (isDevelopment)
                    {
                        // More permissive CORS for development
                        policy.SetIsOriginAllowed(_ => true) // Allow any origin in development
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials()
                              .WithExposedHeaders("Content-Disposition");
                    }
                    else
                    {
                        // Strict CORS for production
                        policy.WithOrigins(allowedOrigins.ToArray())
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials()
                              .SetIsOriginAllowedToAllowWildcardSubdomains()
                              .WithExposedHeaders("Content-Disposition");
                    }
                });
            });

            return services;
        }

        public static IServiceCollection AddErpSystemRateLimiting(
            this IServiceCollection services,
            IHostEnvironment environment,
            IConfiguration? configuration = null)
        {
            // Optional overrides — RateLimiting:AnonymousAuthPermitLimit, :AuthPermitLimit,
            // :ExternalPermitLimit and :InternalPermitLimit, requests a minute — for an API under automated
            // test only. The HR performance harness sets them for the API it starts: its suites run one call
            // after another, and spent most of a run queued for the next window. Unset (every other
            // environment), each limit is exactly its default below. Each one loosens the brute-force and
            // abuse protection it names, so a start with any of them set says so in the log.
            int Limit(string key, int defaultLimit)
            {
                var configured = configuration?.GetValue<int?>($"RateLimiting:{key}");
                if (configured is not > 0) return defaultLimit;
                Log.Warning(
                    "Rate limit RateLimiting:{Key} overridden by configuration: {Configured} a minute (default {Default})",
                    key, configured.Value, defaultLimit);
                return configured.Value;
            }

            var anonymousAuthPermitLimit = Limit("AnonymousAuthPermitLimit", environment.IsDevelopment() ? 120 : 30);
            var authPolicyPermitLimit = Limit("AuthPermitLimit", environment.IsDevelopment() ? 120 : 10);
            var externalPermitLimit = Limit("ExternalPermitLimit", 90);
            var internalPermitLimit = Limit("InternalPermitLimit", 300);

            services.AddRateLimiter(rateLimiterOptions =>
            {
                // Per-caller partition key: authenticated -> user id, anonymous -> client IP.
                // Mirrors the GlobalLimiter keying below so every named policy is scoped PER CALLER
                // instead of one shared bucket for the whole service (a few callers would otherwise
                // exhaust the quota and 429 everyone else). Behind a proxy the client IP is only
                // accurate when ForwardedHeaders is configured (see Program.cs / trust-none default).
                static string CallerKey(HttpContext ctx)
                {
                    var user = ctx.User;
                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        var uid = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        if (!string.IsNullOrEmpty(uid)) return $"user:{uid}";
                    }
                    return $"ip:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
                }

                // [HR-MODULE-PORT] Named policies required by the ported HR portal/recruitment
                // controllers ([EnableRateLimiting("...")]). Without them those endpoints throw
                // "no such policy exists" at run time. These are ADDITIVE — named policies apply only
                // to the endpoints that opt in, and do not alter the global limiter below.
                // NOTE: "AuthPolicy" is NOT defined here — RHEMA already declares it further down.

                // Public career portal — browsing (vacancies, catalogue, tracking)
                rateLimiterOptions.AddPolicy("PublicPortalPolicy", httpContext =>
                    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: CallerKey(httpContext),
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 60,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 5
                        }));

                // Public career portal — application submission (strict, to prevent spam)
                rateLimiterOptions.AddPolicy("PublicApplyPolicy", httpContext =>
                    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: CallerKey(httpContext),
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(10),
                            QueueLimit = 0
                        }));

                // Public career portal — CV upload. Separate from PublicApplyPolicy on purpose:
                // sharing one budget meant an applicant who re-uploaded their CV twice had spent
                // the allowance they needed to actually submit. Tighter than apply because each
                // request costs a malware scan and storage quota against an anonymous caller.
                rateLimiterOptions.AddPolicy("PublicUploadPolicy", httpContext =>
                    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: CallerKey(httpContext),
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 3,
                            Window = TimeSpan.FromMinutes(10),
                            QueueLimit = 0
                        }));

                // Global limiter applies to every request (external portal included)
                rateLimiterOptions.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var path = context.Request.Path.Value ?? string.Empty;
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var user = context.User;

                    var isAuthenticated = user?.Identity?.IsAuthenticated == true;
                    var authProvider = isAuthenticated ? user?.FindFirst("auth_provider")?.Value : null;
                    var isExternal = isAuthenticated && string.Equals(authProvider, "Local", StringComparison.OrdinalIgnoreCase);

                    // Anonymous traffic (public portal endpoints)
                    if (!isAuthenticated)
                    {
                        var isAuth = path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase);
                        var key = isAuth ? $"anon-auth:{ip}" : $"anon:{ip}";

                        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: key,
                            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                            {
                                PermitLimit = isAuth ? anonymousAuthPermitLimit : 120,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0
                            });
                    }

                    // External portal (Local auth) traffic
                    if (isExternal)
                    {
                        var userId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? ip;
                        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: $"external:{userId}",
                            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                            {
                                PermitLimit = externalPermitLimit,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                                QueueLimit = 10
                            });
                    }

                    // Internal ERP traffic (LDAP)
                    var internalUserId = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? ip;
                    return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"internal:{internalUserId}",
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = internalPermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                            QueueLimit = 50
                        });
                });

                // General API rate limiting
                rateLimiterOptions.AddPolicy("ApiPolicy", httpContext =>
                    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: CallerKey(httpContext),
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 10
                        }));

                // Stricter rate limiting for authentication endpoints (per caller — mostly by IP since
                // login is pre-auth, which is the correct key for brute-force protection).
                // PermitLimit is environment-scoped (relaxed in Development) per master.
                rateLimiterOptions.AddPolicy("AuthPolicy", httpContext =>
                    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: CallerKey(httpContext),
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authPolicyPermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 2
                        }));

                // Very strict rate limiting for sensitive endpoints
                rateLimiterOptions.AddPolicy("SensitivePolicy", httpContext =>
                    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: CallerKey(httpContext),
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0 // No queuing for sensitive endpoints
                        }));

                // Global rejection response
                rateLimiterOptions.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    context.HttpContext.Response.ContentType = "application/json";

                    var response = new
                    {
                        error = "Rate limit exceeded",
                        message = "Too many requests. Please try again later.",
                        retryAfter = "60" // Fixed retry time since metadata access may not work
                    };

                    await context.HttpContext.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response), token);
                };
            });

            return services;
        }
    }
}
