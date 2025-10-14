using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Service for managing maintenance contractors
/// </summary>
public interface IContractorService
{
    /// <summary>
    /// Gets all contractors with optional filtering
    /// </summary>
    Task<PagedResult<MaintenanceContractorDto>> GetContractorsAsync(ContractorFilterDto filter);

    /// <summary>
    /// Gets a contractor by ID
    /// </summary>
    Task<MaintenanceContractorDto?> GetContractorByIdAsync(Guid id);

    /// <summary>
    /// Creates a new contractor
    /// </summary>
    Task<MaintenanceContractorDto> CreateContractorAsync(CreateMaintenanceContractorDto createDto);

    /// <summary>
    /// Updates an existing contractor
    /// </summary>
    Task<MaintenanceContractorDto> UpdateContractorAsync(Guid id, UpdateMaintenanceContractorDto updateDto);

    /// <summary>
    /// Deletes a contractor
    /// </summary>
    Task DeleteContractorAsync(Guid id);

    /// <summary>
    /// Gets contractors by capability
    /// </summary>
    Task<IEnumerable<MaintenanceContractorDto>> GetContractorsByCapabilityAsync(string capability);

    /// <summary>
    /// Gets contractors available in a service area
    /// </summary>
    Task<IEnumerable<MaintenanceContractorDto>> GetContractorsByServiceAreaAsync(string serviceArea);

    /// <summary>
    /// Updates contractor rating based on performance reviews
    /// </summary>
    Task UpdateContractorRatingAsync(Guid contractorId);

    /// <summary>
    /// Gets contractor performance summary
    /// </summary>
    Task<ContractorSummaryDto> GetContractorSummaryAsync();
}

/// <summary>
/// Service for managing contractor work orders
/// </summary>
public interface IContractorWorkOrderService
{
    /// <summary>
    /// Assigns a work order to a contractor
    /// </summary>
    Task<ContractorWorkOrderDto> AssignWorkOrderToContractorAsync(AssignWorkOrderToContractorDto assignDto);

    /// <summary>
    /// Updates contractor work order progress
    /// </summary>
    Task<ContractorWorkOrderDto> UpdateContractorWorkOrderAsync(Guid contractorWorkOrderId, UpdateContractorWorkOrderDto updateDto);

    /// <summary>
    /// Gets contractor work orders
    /// </summary>
    Task<IEnumerable<ContractorWorkOrderDto>> GetContractorWorkOrdersAsync(Guid? contractorId = null, Guid? workOrderId = null);

    /// <summary>
    /// Completes a contractor work order
    /// </summary>
    Task<ContractorWorkOrderDto> CompleteContractorWorkOrderAsync(Guid contractorWorkOrderId, UpdateContractorWorkOrderDto completionDto);

    /// <summary>
    /// Cancels a contractor work order assignment
    /// </summary>
    Task CancelContractorWorkOrderAsync(Guid contractorWorkOrderId, string reason);

    /// <summary>
    /// Gets work orders by contractor and status
    /// </summary>
    Task<IEnumerable<ContractorWorkOrderDto>> GetWorkOrdersByContractorAndStatusAsync(Guid contractorId, string status);
}

/// <summary>
/// Service for managing contractor invoices
/// </summary>
public interface IContractorInvoiceService
{
    /// <summary>
    /// Gets contractor invoices with filtering
    /// </summary>
    Task<PagedResult<ContractorInvoiceDto>> GetContractorInvoicesAsync(ContractorInvoiceFilterDto filter);

    /// <summary>
    /// Gets a contractor invoice by ID
    /// </summary>
    Task<ContractorInvoiceDto?> GetContractorInvoiceByIdAsync(Guid id);

    /// <summary>
    /// Creates a new contractor invoice
    /// </summary>
    Task<ContractorInvoiceDto> CreateContractorInvoiceAsync(CreateContractorInvoiceDto createDto);

    /// <summary>
    /// Updates an existing contractor invoice
    /// </summary>
    Task<ContractorInvoiceDto> UpdateContractorInvoiceAsync(Guid id, UpdateContractorInvoiceDto updateDto);

    /// <summary>
    /// Processes invoice approval or rejection
    /// </summary>
    Task<ContractorInvoiceDto> ProcessContractorInvoiceAsync(Guid id, ProcessContractorInvoiceDto processDto);

    /// <summary>
    /// Marks invoice as paid
    /// </summary>
    Task<ContractorInvoiceDto> MarkInvoiceAsPaidAsync(Guid id);

    /// <summary>
    /// Gets overdue invoices
    /// </summary>
    Task<IEnumerable<ContractorInvoiceDto>> GetOverdueInvoicesAsync();

    /// <summary>
    /// Gets invoices pending approval
    /// </summary>
    Task<IEnumerable<ContractorInvoiceDto>> GetInvoicesPendingApprovalAsync();

    /// <summary>
    /// Deletes a contractor invoice
    /// </summary>
    Task DeleteContractorInvoiceAsync(Guid id);
}

/// <summary>
/// Service for managing contractor expenses
/// </summary>
public interface IContractorExpenseService
{
    /// <summary>
    /// Gets contractor expenses for an invoice
    /// </summary>
    Task<IEnumerable<ContractorExpenseDto>> GetContractorExpensesAsync(Guid contractorInvoiceId);

    /// <summary>
    /// Gets a contractor expense by ID
    /// </summary>
    Task<ContractorExpenseDto?> GetContractorExpenseByIdAsync(Guid id);

    /// <summary>
    /// Creates a new contractor expense
    /// </summary>
    Task<ContractorExpenseDto> CreateContractorExpenseAsync(CreateContractorExpenseDto createDto);

    /// <summary>
    /// Updates an existing contractor expense
    /// </summary>
    Task<ContractorExpenseDto> UpdateContractorExpenseAsync(Guid id, UpdateContractorExpenseDto updateDto);

    /// <summary>
    /// Processes expense approval or rejection
    /// </summary>
    Task<ContractorExpenseDto> ProcessContractorExpenseAsync(Guid id, ProcessContractorExpenseDto processDto);

    /// <summary>
    /// Deletes a contractor expense
    /// </summary>
    Task DeleteContractorExpenseAsync(Guid id);

    /// <summary>
    /// Gets expenses by type for analysis
    /// </summary>
    Task<ContractorCostAnalysisDto> GetExpenseAnalysisAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Gets expenses pending approval
    /// </summary>
    Task<IEnumerable<ContractorExpenseDto>> GetExpensesPendingApprovalAsync();
}

/// <summary>
/// Service for managing contractor performance reviews
/// </summary>
public interface IContractorPerformanceService
{
    /// <summary>
    /// Gets performance reviews for a contractor
    /// </summary>
    Task<IEnumerable<ContractorPerformanceReviewDto>> GetContractorPerformanceReviewsAsync(Guid contractorId);

    /// <summary>
    /// Gets a performance review by ID
    /// </summary>
    Task<ContractorPerformanceReviewDto?> GetPerformanceReviewByIdAsync(Guid id);

    /// <summary>
    /// Creates a new performance review
    /// </summary>
    Task<ContractorPerformanceReviewDto> CreatePerformanceReviewAsync(CreateContractorPerformanceReviewDto createDto);

    /// <summary>
    /// Gets performance reviews for a work order
    /// </summary>
    Task<IEnumerable<ContractorPerformanceReviewDto>> GetWorkOrderPerformanceReviewsAsync(Guid workOrderId);

    /// <summary>
    /// Calculates average contractor rating
    /// </summary>
    Task<double> CalculateContractorRatingAsync(Guid contractorId);

    /// <summary>
    /// Gets top performing contractors
    /// </summary>
    Task<IEnumerable<MaintenanceContractorDto>> GetTopPerformingContractorsAsync(int count = 10);

    /// <summary>
    /// Deletes a performance review
    /// </summary>
    Task DeletePerformanceReviewAsync(Guid id);
}