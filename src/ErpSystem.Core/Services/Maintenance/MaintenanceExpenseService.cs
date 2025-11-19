using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class MaintenanceExpenseService : IMaintenanceExpenseService
{
    private readonly IMaintenanceExpenseRepository _expenseRepository;
    private readonly ErpSystem.Core.Interfaces.HR.IEmployeeRepository _employeeRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MaintenanceExpenseService> _logger;

    public MaintenanceExpenseService(
        IMaintenanceExpenseRepository expenseRepository,
        ErpSystem.Core.Interfaces.HR.IEmployeeRepository employeeRepository,
        IWorkOrderRepository workOrderRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<MaintenanceExpenseService> logger)
    {
        _expenseRepository = expenseRepository;
        _employeeRepository = employeeRepository;
        _workOrderRepository = workOrderRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<MaintenanceExpenseDto> CreateExpenseAsync(CreateMaintenanceExpenseDto createDto)
    {
        try
        {
            var expense = new MaintenanceExpense
            {
                WorkOrderId = createDto.WorkOrderId,
                ScheduleId = createDto.ScheduleId,
                ExpenseType = createDto.ExpenseType,
                Description = createDto.Description,
                Amount = createDto.Amount,
                ExpenseDate = createDto.ExpenseDate,
                MileageDriven = createDto.MileageDriven,
                MileageRate = createDto.MileageRate,
                FuelQuantity = createDto.FuelQuantity,
                FuelPricePerUnit = createDto.FuelPricePerUnit,
                VehicleId = createDto.VehicleId,
                ReceiptPath = createDto.ReceiptPath,
                VendorName = createDto.VendorName,
                ReferenceNumber = createDto.ReferenceNumber,
                IsReimbursable = createDto.IsReimbursable,
                Location = createDto.Location,
                Latitude = createDto.Latitude,
                Longitude = createDto.Longitude,
                Status = "Pending",
                TenantId = _currentUserService.TenantId ?? Guid.Empty
            };

            await _expenseRepository.AddAsync(expense);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDto(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance expense");
            throw;
        }
    }

    public async Task<MaintenanceExpenseDto> UpdateExpenseAsync(Guid id, UpdateMaintenanceExpenseDto updateDto)
    {
        try
        {
            var expense = await _expenseRepository.GetByIdAsync(id);
            if (expense == null)
                throw new ArgumentException($"Expense {id} not found");

            if (!string.IsNullOrEmpty(updateDto.ExpenseType))
                expense.ExpenseType = updateDto.ExpenseType;
            if (!string.IsNullOrEmpty(updateDto.Description))
                expense.Description = updateDto.Description;
            if (updateDto.Amount.HasValue)
                expense.Amount = updateDto.Amount.Value;
            if (updateDto.ExpenseDate.HasValue)
                expense.ExpenseDate = updateDto.ExpenseDate.Value;
            if (updateDto.MileageDriven.HasValue)
                expense.MileageDriven = updateDto.MileageDriven;
            if (updateDto.MileageRate.HasValue)
                expense.MileageRate = updateDto.MileageRate;
            if (updateDto.FuelQuantity.HasValue)
                expense.FuelQuantity = updateDto.FuelQuantity;
            if (updateDto.FuelPricePerUnit.HasValue)
                expense.FuelPricePerUnit = updateDto.FuelPricePerUnit;
            if (updateDto.VehicleId.HasValue)
                expense.VehicleId = updateDto.VehicleId;
            if (!string.IsNullOrEmpty(updateDto.ReceiptPath))
                expense.ReceiptPath = updateDto.ReceiptPath;
            if (!string.IsNullOrEmpty(updateDto.VendorName))
                expense.VendorName = updateDto.VendorName;
            if (!string.IsNullOrEmpty(updateDto.ReferenceNumber))
                expense.ReferenceNumber = updateDto.ReferenceNumber;
            if (!string.IsNullOrEmpty(updateDto.Status))
                expense.Status = updateDto.Status;
            if (!string.IsNullOrEmpty(updateDto.ApprovalNotes))
                expense.ApprovalNotes = updateDto.ApprovalNotes;
            if (updateDto.IsReimbursable.HasValue)
                expense.IsReimbursable = updateDto.IsReimbursable.Value;
            if (updateDto.IsReimbursed.HasValue)
                expense.IsReimbursed = updateDto.IsReimbursed.Value;
            if (updateDto.ReimbursedDate.HasValue)
                expense.ReimbursedDate = updateDto.ReimbursedDate;
            if (!string.IsNullOrEmpty(updateDto.Location))
                expense.Location = updateDto.Location;
            if (updateDto.Latitude.HasValue)
                expense.Latitude = updateDto.Latitude;
            if (updateDto.Longitude.HasValue)
                expense.Longitude = updateDto.Longitude;

            expense.UpdatedAt = DateTime.UtcNow;

            await _expenseRepository.UpdateAsync(expense);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDto(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", id);
            throw;
        }
    }

    public async Task DeleteExpenseAsync(Guid id)
    {
        try
        {
            await _expenseRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", id);
            throw;
        }
    }

    public async Task<MaintenanceExpenseDto?> GetExpenseByIdAsync(Guid id)
    {
        try
        {
            var expense = await _expenseRepository.GetByIdWithDetailsAsync(id);
            return expense == null ? null : await MapToDto(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {ExpenseId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceExpenseDto>> GetExpensesByWorkOrderIdAsync(Guid workOrderId)
    {
        try
        {
            var expenses = await _expenseRepository.GetByWorkOrderIdAsync(workOrderId);
            return await Task.WhenAll(expenses.Select(e => MapToDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceExpenseDto>> GetExpensesByTechnicianIdAsync(Guid technicianId)
    {
        try
        {
            var expenses = await _expenseRepository.GetByTechnicianIdAsync(technicianId);
            return await Task.WhenAll(expenses.Select(e => MapToDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses for technician {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceExpenseDto>> GetPendingExpensesAsync()
    {
        try
        {
            var expenses = await _expenseRepository.GetPendingExpensesAsync();
            return await Task.WhenAll(expenses.Select(e => MapToDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending expenses");
            throw;
        }
    }

    public async Task<MaintenanceExpenseDto> ApproveExpenseAsync(Guid id, ApproveExpenseDto approveDto)
    {
        try
        {
            var expense = await _expenseRepository.GetByIdAsync(id);
            if (expense == null)
                throw new ArgumentException($"Expense {id} not found");

            expense.Status = approveDto.Status;
            expense.ApprovedById = _currentUserService.EmployeeId ?? 
                (Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : Guid.Empty);
            expense.ApprovedDate = DateTime.UtcNow;
            expense.ApprovalNotes = approveDto.ApprovalNotes;
            expense.UpdatedAt = DateTime.UtcNow;

            await _expenseRepository.UpdateAsync(expense);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDto(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", id);
            throw;
        }
    }

    public async Task<decimal> GetTotalExpensesByWorkOrderIdAsync(Guid workOrderId)
    {
        try
        {
            return await _expenseRepository.GetTotalExpensesByWorkOrderIdAsync(workOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total expenses for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    private async Task<MaintenanceExpenseDto> MapToDto(MaintenanceExpense expense)
    {
        string approvedByName = string.Empty;
        if (expense.ApprovedBy != null)
        {
            approvedByName = $"{expense.ApprovedBy.FirstName} {expense.ApprovedBy.LastName}";
        }
        else if (expense.ApprovedById.HasValue)
        {
            try
            {
                var approver = await _employeeRepository.GetByIdAsync(expense.ApprovedById.Value);
                if (approver != null)
                    approvedByName = $"{approver.FirstName} {approver.LastName}";
            }
            catch { }
        }

        return new MaintenanceExpenseDto
        {
            Id = expense.Id,
            WorkOrderId = expense.WorkOrderId,
            WorkOrderNumber = expense.WorkOrder?.WorkOrderNumber ?? string.Empty,
            ScheduleId = expense.ScheduleId,
            ExpenseType = expense.ExpenseType,
            Description = expense.Description,
            Amount = expense.Amount,
            ExpenseDate = expense.ExpenseDate,
            MileageDriven = expense.MileageDriven,
            MileageRate = expense.MileageRate,
            FuelQuantity = expense.FuelQuantity,
            FuelPricePerUnit = expense.FuelPricePerUnit,
            VehicleId = expense.VehicleId,
            VehicleName = expense.Vehicle?.Name,
            ReceiptPath = expense.ReceiptPath,
            VendorName = expense.VendorName,
            ReferenceNumber = expense.ReferenceNumber,
            Status = expense.Status,
            ApprovedById = expense.ApprovedById,
            ApprovedByName = approvedByName,
            ApprovedDate = expense.ApprovedDate,
            ApprovalNotes = expense.ApprovalNotes,
            IsReimbursable = expense.IsReimbursable,
            IsReimbursed = expense.IsReimbursed,
            ReimbursedDate = expense.ReimbursedDate,
            Location = expense.Location,
            Latitude = expense.Latitude,
            Longitude = expense.Longitude,
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt
        };
    }
}
