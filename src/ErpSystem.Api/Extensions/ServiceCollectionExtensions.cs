using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Serilog;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Web.Services;
using ErpSystem.Web.Middleware;
using ErpSystem.Web.HealthChecks;
using ErpSystem.Web.Configuration;
using ErpSystem.Api.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.Fiscal;
using ErpSystem.Api.Services.Finance.Segments;
using ErpSystem.Api.Services.Finance.UnitAccounting;
using ErpSystem.Api.Services.Finance.Budget;
using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Api.Services.Finance.Taxation;
using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.Models;
using static ErpSystem.Core.Services.StorageServiceExtensions;

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
                    ValidateIssuer = !string.IsNullOrEmpty(jwtSettings["Issuer"]),
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = !string.IsNullOrEmpty(jwtSettings["Audience"]),
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Register JWT service
            services.AddScoped<IJwtTokenService, JwtTokenService>();

            return services;
        }

        public static IServiceCollection AddErpSystemRepositories(this IServiceCollection services)
        {
            // Generic repository
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // Specific repositories
            services.AddScoped<ITenantRepository, TenantRepository>();
            
            // Finance Repositories
            services.AddScoped<IPaymentTermRepository, ErpSystem.Data.Repositories.Finance.PaymentTermRepository>();
            services.AddScoped<ICurrencyRepository, ErpSystem.Data.Repositories.Finance.CurrencyRepository>();

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

            // Data source repositories
            services.AddScoped<IDataSourceRepository, DataSourceRepository>();

            // HR repositories using consolidated HR namespace interfaces
            services.AddScoped<ErpSystem.Core.Interfaces.HR.IEmployeeRepository, ErpSystem.Data.Repositories.EmployeeRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.HR.IDepartmentRepository, ErpSystem.Data.Repositories.DepartmentRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.HR.ISectionRepository, ErpSystem.Data.Repositories.SectionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.HR.IEmployeeSkillRepository, ErpSystem.Data.Repositories.EmployeeSkillRepository>();

            // Additional HR repositories
            services.AddScoped<ErpSystem.Core.Interfaces.HR.IEmployeePositionRepository, ErpSystem.Data.Repositories.EmployeePositionRepository>();

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

            // Procurement repositories
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPurchaseOrderItemRepository, ErpSystem.Data.Repositories.Procurement.PurchaseOrderItemRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ISupplierRepository, ErpSystem.Data.Repositories.Procurement.SupplierRepository>();
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
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ITenderRepository, ErpSystem.Data.Repositories.Procurement.TenderRepository>();

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
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetConditionRepository, ErpSystem.Data.Repositories.Maintenance.AssetConditionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceToolRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceToolRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IToolCheckoutRepository, ErpSystem.Data.Repositories.Maintenance.ToolCheckoutRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderToolRepository, ErpSystem.Data.Repositories.Maintenance.WorkOrderToolRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceStaffScheduleRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceStaffScheduleRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceExpenseRepository, ErpSystem.Data.Repositories.Maintenance.MaintenanceExpenseRepository>();

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
            // Unified notification service - comprehensive notification handling for all ERP modules
            services.AddScoped<ErpSystem.Core.Interfaces.INotificationService, ErpSystem.Api.Services.UnifiedNotificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.INotificationTopicPublisher, ErpSystem.Core.Services.Notifications.NotificationTopicPublisher>();
            services.AddScoped<ErpSystem.Core.Interfaces.Events.IAppEventBus, ErpSystem.Core.Services.Events.AppEventBus>();
            services.AddScoped<ErpSystem.Core.Interfaces.Events.IAppEventHandler<ErpSystem.Core.Interfaces.Events.EntityActivityEvent>, ErpSystem.Core.Services.Notifications.EntityActivityNotificationTopicHandler>();
            services.AddScoped<ErpSystem.Core.Interfaces.IWorkflowService, ErpSystem.Api.Services.SimpleWorkflowService>();
            services.AddScoped<ErpSystem.Core.Interfaces.IWorkflowIntegrationService, ErpSystem.Core.Services.Workflow.WorkflowIntegrationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.IWorkflowStatusAdapterRegistry, ErpSystem.Core.Services.Workflow.WorkflowStatusAdapterRegistry>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<IUserTenantService, UserTenantService>();
            services.AddScoped<ILdapAuthenticationService, LdapAuthenticationService>();
            services.AddScoped<ISearchService, SearchService>();

            // Identity services
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ErpSystem.Data.Services.IPermissionService, ErpSystem.Data.Services.PermissionService>();
            services.AddScoped<ErpSystem.Data.Services.IRolePermissionService, ErpSystem.Data.Services.RolePermissionService>();

            // Settings services
            services.AddScoped<ISettingsService, SettingsService>();
            services.AddScoped<ITenantSettingsService, ErpSystem.Api.Services.TenantSettingsService>();

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

            services.AddScoped<ISecurityService, SecurityService>();
            // User context services
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, ErpSystem.Api.Services.CurrentUserService>();
            services.AddScoped<ICurrentUserProvider, ErpSystem.Api.Services.CurrentUserService>();
            services.AddScoped<ITenantContext, ErpSystem.Api.Services.CurrentUserService>();

            // Dashboard service
            services.AddScoped<ErpSystem.Core.Services.IDashboardService, ErpSystem.Data.Services.DashboardService>();

            // Reports service
            services.AddScoped<IReportsService, ErpSystem.Data.Services.DatabaseReportsService>();

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
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowNotificationService, ErpSystem.Core.Services.Workflow.WorkflowNotificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowEntityDisplayService, ErpSystem.Core.Services.Workflow.WorkflowEntityDisplayService>();

            // Enquiry, Helpdesk & Complaints (EHC) services
            services.AddScoped<ErpSystem.Core.Interfaces.Ehc.IEhcTicketService, ErpSystem.Core.Services.Ehc.EhcTicketService>();

            // Enhanced maintenance workflow integration - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IEnhancedMaintenanceWorkflowService, ErpSystem.Core.Services.Maintenance.EnhancedMaintenanceWorkflowService>();

            // Maintenance asset service - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAssetService, ErpSystem.Api.Services.Maintenance.MaintenanceAssetService>();

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
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryTransferRepository, ErpSystem.Data.Repositories.Inventory.InventoryTransferRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionRepository, ErpSystem.Data.Repositories.Inventory.InventoryRequisitionRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListRepository, ErpSystem.Data.Repositories.Pricing.PriceListRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Pricing.IPriceListLineRepository, ErpSystem.Data.Repositories.Pricing.PriceListLineRepository>();

            // Inventory services
            services.AddScoped<ErpSystem.Core.Interfaces.Inventory.IInventoryManagementService, ErpSystem.Core.Services.Inventory.InventoryManagementService>();

            // Phase 1: Core Maintenance Services - workflow-ready implementation - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderService, ErpSystem.Core.Services.Maintenance.WorkOrderService>();
            // MaintenanceInventoryService - now enabled with IInventoryManagementService dependency
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceInventoryService, ErpSystem.Core.Services.Maintenance.MaintenanceInventoryService>();


            // Asset Admission & Discharge services (P1-E4)
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetAdmissionService, ErpSystem.Core.Services.Maintenance.AssetAdmissionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetDischargeService, ErpSystem.Core.Services.Maintenance.AssetDischargeService>();

            // Tool Checkout Service - manages tool checkout/return operations
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IToolCheckoutService, ErpSystem.Core.Services.Maintenance.ToolCheckoutService>();
            // Work Order Tool Service - manages tool allocation and checkout for work orders
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderToolService, ErpSystem.Core.Services.Maintenance.WorkOrderToolService>();
            // Work Order Part Service - manages parts/consumables for work orders
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderPartService, ErpSystem.Core.Services.Maintenance.WorkOrderPartService>();

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

            // Asset type service - using core namespace
            // NOTE: This service registration was temporarily commented out as IAssetTypeService might not be needed
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetTypeService, ErpSystem.Core.Services.Maintenance.AssetTypeService>();

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

            // Phase 3 Asset Performance Analytics - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetPerformanceAnalyticsService, ErpSystem.Core.Services.Maintenance.AssetPerformanceAnalyticsService>();

            // Missing maintenance services that controllers require - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IPriorityLevelService, ErpSystem.Api.Services.Maintenance.PriorityLevelService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IWorkOrderTypeService, ErpSystem.Api.Services.Maintenance.WorkOrderTypeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceTypeService, ErpSystem.Api.Services.Maintenance.MaintenanceTypeService>();

            // Additional maintenance services - ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceAssetCategoryService, ErpSystem.Api.Services.Maintenance.MaintenanceAssetCategoryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IJobCardService, ErpSystem.Core.Services.Maintenance.JobCardService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetConditionService, ErpSystem.Core.Services.Maintenance.AssetConditionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITaskTemplateService, ErpSystem.Core.Services.Maintenance.TaskTemplateService>();

            // Usage Tracking and Trigger Evaluation - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetUsageTrackingService, ErpSystem.Core.Services.Maintenance.AssetUsageTrackingService>();
            services.AddScoped<ErpSystem.Core.Services.Maintenance.MaintenanceTriggerEvaluationService>();

            // Asset downtime tracking - REQUIRED by EmergencyMaintenanceController and MaintenanceDashboardController
            services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetDowntimeService, ErpSystem.Core.Services.Maintenance.AssetDowntimeService>();

            // TODO: Additional maintenance services to be implemented as needed:
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IAssetInspectionService, ErpSystem.Core.Services.Maintenance.AssetInspectionService>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianTeamService, ErpSystem.Core.Services.Maintenance.TechnicianTeamService>();

            // Technician Scheduling services
            // TODO: Create missing maintenance services
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.ITechnicianSchedulingService, ErpSystem.Core.Services.Maintenance.TechnicianSchedulingService>();
            // services.AddScoped<ErpSystem.Core.Services.Maintenance.WorkOrderSchedulingService>();
            // services.AddScoped<ErpSystem.Core.Interfaces.Maintenance.IMaintenanceInventoryService, ErpSystem.Core.Services.Maintenance.MaintenanceInventoryService>();

            // HR Services - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.HR.IEmployeeService, ErpSystem.Core.Services.HR.EmployeeService>();

            // Business Partner / Supplier & Contractor Management Services - NOW ENABLED
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerService, ErpSystem.Core.Services.Procurement.BusinessPartnerService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerCategoryService, ErpSystem.Core.Services.Procurement.PartnerCategoryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IContractorSpecializationService, ErpSystem.Core.Services.Procurement.ContractorSpecializationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.ILicenseTypeService, ErpSystem.Core.Services.Procurement.LicenseTypeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerRegistrationService, ErpSystem.Core.Services.Procurement.BusinessPartnerRegistrationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerVerificationService, ErpSystem.Core.Services.Procurement.PartnerVerificationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerPerformanceService, ErpSystem.Core.Services.Procurement.PartnerPerformanceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IPartnerBlacklistService, ErpSystem.Core.Services.Procurement.PartnerBlacklistService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBlacklistHistoryRepository, ErpSystem.Data.Repositories.Procurement.BlacklistHistoryRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementSettingsRepository, ErpSystem.Data.Repositories.Procurement.ProcurementSettingsRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBusinessPartnerUserRepository, ErpSystem.Data.Repositories.Procurement.BusinessPartnerUserRepository>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IConsignmentSettlementService, ErpSystem.Core.Services.Procurement.ConsignmentSettlementService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IBlacklistHistoryService, ErpSystem.Core.Services.Procurement.BlacklistHistoryService>();

            // Maintenance background services - NOW ENABLED
            services.AddHostedService<ErpSystem.Api.Services.Maintenance.MaintenanceTriggerEvaluationBackgroundService>();

            // Notification dispatcher background service - sends pending notifications on schedule with dead-letter support
            services.AddHostedService<ErpSystem.Api.Services.NotificationDispatcherBackgroundService>();

            // Blacklist expiry background service
            services.AddHostedService<ErpSystem.Core.Services.Procurement.BlacklistExpiryBackgroundService>();

            // Exception log maintenance (retention purge)
            services.AddHostedService<ErpSystem.Api.Services.ExceptionLogMaintenanceBackgroundService>();

            // EHC SLA monitoring / escalation (tickets)
            services.AddHostedService<ErpSystem.Api.Services.Ehc.EhcSlaMonitoringBackgroundService>();

            // Notification template and escalation services
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IMaintenanceNotificationTemplateService,
                ErpSystem.Core.Services.Maintenance.MaintenanceNotificationTemplateService>();
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IMaintenanceEscalationService,
                ErpSystem.Core.Services.Maintenance.MaintenanceEscalationService>();
            services.AddScoped<ErpSystem.Core.Services.Maintenance.IDeadLetterNotificationService,
                ErpSystem.Core.Services.Maintenance.DeadLetterNotificationService>();


            // Finance Services

            services.AddScoped<IGeneralLedgerService, GeneralLedgerService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IBudgetService, BudgetService>();
            services.AddScoped<IFinanceSettingsService, FinanceSettingsService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFiscalPeriodService, FiscalPeriodService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICurrencyRevaluationService, CurrencyRevaluationService>();

            services.AddScoped<IPaymentTermService, ErpSystem.Core.Services.Finance.PaymentTermService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountService, AccountService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICurrencyService, ErpSystem.Api.Services.Finance.MultiCurrency.CurrencyService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IJournalEntryService, ErpSystem.Api.Services.Finance.GL.JournalEntryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetCategoryService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetCategoryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetDepreciationService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetDepreciationService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAssetTransferService, ErpSystem.Api.Services.Finance.FixedAssets.AssetTransferService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAssetDisposalService, ErpSystem.Api.Services.Finance.FixedAssets.AssetDisposalService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAssetVerificationService, ErpSystem.Api.Services.Finance.FixedAssets.AssetVerificationService>();
        services.AddScoped<ErpSystem.Core.Interfaces.Finance.IFixedAssetReportsService, ErpSystem.Api.Services.Finance.FixedAssets.FixedAssetReportsService>();

            // Tax Services
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ITaxCalculationEngine, TaxCalculationEngine>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ITaxConfigurationService, TaxConfigurationService>();

            // Segment Services
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ISegmentStructureService, SegmentStructureService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountSegmentStructureService, AccountSegmentStructureService>();
            services.AddScoped<ErpSystem.Core.Services.Finance.IAccountSegmentValueService, AccountSegmentValueService>();
            services.AddScoped<ErpSystem.Core.Services.Finance.ISegmentLookupValueService, SegmentLookupValueService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAccountCombinationService, AccountCombinationService>();

            // Unit Accounts Services
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitTypeService, UnitTypeService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitAccountService, UnitAccountService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitJournalEntryService, UnitJournalEntryService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IRatioDefinitionService, RatioDefinitionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IUnitBudgetService, UnitBudgetService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IAllocationService, AllocationService>();

            // Cash Management Services
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IBankAccountService, BankAccountService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICashTransactionService, CashTransactionService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IBankReconciliationService, BankReconciliationService>();
            services.AddScoped<BankReconciliationEngine>();

            // Accounts Receivable (AR) Services
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.ICustomerService, ErpSystem.Api.Services.Finance.AR.CustomerService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IInvoiceService, ErpSystem.Api.Services.Finance.AR.InvoiceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IPaymentService, ErpSystem.Api.Services.Finance.AR.PaymentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IArReportsService, ErpSystem.Api.Services.Finance.AR.ArReportsService>();

            // Accounts Payable (AP) Services
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IVendorInvoiceService, ErpSystem.Api.Services.Finance.AP.VendorInvoiceService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IVendorPaymentService, ErpSystem.Api.Services.Finance.AP.VendorPaymentService>();
            services.AddScoped<ErpSystem.Core.Interfaces.Finance.IApReportsService, ErpSystem.Api.Services.Finance.AP.ApReportsService>();

            // Add AutoMapper - using assembly scanning approach
            services.AddAutoMapper(typeof(Program).Assembly, typeof(ErpSystem.Core.Services.TenantService).Assembly, typeof(ErpSystem.Data.ApplicationDbContext).Assembly);

            return services;
        }

        public static IServiceCollection AddErpSystemAuthorization(this IServiceCollection services)
        {
            services.AddAuthorizationBuilder()
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
                        string.Equals(ctx.User.FindFirst("auth_provider")?.Value, "Local", StringComparison.OrdinalIgnoreCase)))
                .AddPolicy("InternalOnly", policy =>
                    policy.RequireAssertion(ctx =>
                        ctx.User?.Identity?.IsAuthenticated == true &&
                        !string.Equals(ctx.User.FindFirst("auth_provider")?.Value, "Local", StringComparison.OrdinalIgnoreCase)))
                .AddPolicy("Finance", policy =>
                    policy.RequireClaim("module", "Finance"))
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
                    policy.RequireRole("Employee", "Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("MaintenanceRead", policy =>
                    policy.RequireRole("Employee", "Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("MaintenanceWrite", policy =>
                    policy.RequireRole("Manager", "TenantAdmin", "SuperAdmin"))
                .AddPolicy("MaintenanceApprove", policy =>
                    policy.RequireRole("Manager", "TenantAdmin", "SuperAdmin"));

            return services;
        }

        public static IServiceCollection AddErpSystemApi(this IServiceCollection services)
        {
            // Custom middleware registered as IMiddleware
            services.AddTransient<ErpSystem.Api.Middleware.ExternalUserAccessMiddleware>();

            services.AddControllers()
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
                        // Authentication & Authorization
                        "Auth" or "Security" or "Users" or "Roles" or "Permissions" => new[] { "🔐 Authentication & Security" },

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
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .CreateLogger();

            services.AddSerilog();

            return services;
        }

        public static IServiceCollection AddErpSystemHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var redisConnectionString = configuration.GetConnectionString("Redis");

            var healthChecksBuilder = services.AddHealthChecks()
                .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running"))
                .AddCheck("startup", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API started successfully"));

            // Add database health check
            if (!string.IsNullOrEmpty(connectionString))
            {
                healthChecksBuilder.AddDbContextCheck<ErpSystem.Data.ApplicationDbContext>("database",
                    tags: new[] { "db", "sql", "ready" });
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
                // Add Redis connection multiplexer for advanced operations
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(redisConnectionString));

                // Add StackExchange Redis cache
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
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
                allowedOrigins.AddRange(new[]
                {
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://localhost:3001",
                    "https://localhost:3001",
                    "http://localhost:4200",
                    "https://localhost:4200",
                    "http://127.0.0.1:3000",
                    "https://127.0.0.1:3000"
                });
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
                              .AllowCredentials();
                    }
                    else
                    {
                        // Strict CORS for production
                        policy.WithOrigins(allowedOrigins.ToArray())
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials()
                              .SetIsOriginAllowedToAllowWildcardSubdomains();
                    }
                });
            });

            return services;
        }

        public static IServiceCollection AddErpSystemRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(rateLimiterOptions =>
            {
                // Global limiter applies to every request (external portal included)
                rateLimiterOptions.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var path = context.Request.Path.Value ?? string.Empty;
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var user = context.User;

                    var isAuthenticated = user?.Identity?.IsAuthenticated == true;
                    var authProvider = isAuthenticated ? user.FindFirst("auth_provider")?.Value : null;
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
                                PermitLimit = isAuth ? 30 : 120,
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
                                PermitLimit = 90,
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
                            PermitLimit = 300,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                            QueueLimit = 50
                        });
                });

                // General API rate limiting
                rateLimiterOptions.AddFixedWindowLimiter(policyName: "ApiPolicy", options =>
                {
                    options.PermitLimit = 100;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 10;
                });

                // Stricter rate limiting for authentication endpoints
                rateLimiterOptions.AddFixedWindowLimiter(policyName: "AuthPolicy", options =>
                {
                    options.PermitLimit = 10;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.QueueLimit = 2;
                });

                // Very strict rate limiting for sensitive endpoints
                rateLimiterOptions.AddFixedWindowLimiter(policyName: "SensitivePolicy", options =>
                {
                    options.PermitLimit = 5;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.QueueLimit = 0; // No queuing for sensitive endpoints
                });

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
