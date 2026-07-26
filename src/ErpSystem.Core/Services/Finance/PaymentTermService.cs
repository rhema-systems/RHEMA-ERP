using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Finance;

public class PaymentTermService : IPaymentTermService
{
    private readonly IPaymentTermRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PaymentTermService> _logger;

    public PaymentTermService(
        IPaymentTermRepository repository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        ILogger<PaymentTermService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PaymentTermDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<PaymentTermDto?> GetByCodeAsync(string code)
    {
        var entity = await _repository.GetByCodeAsync(code);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<IEnumerable<PaymentTermDto>> GetAllAsync()
    {
        var entities = await _repository.GetAllAsync();
        return entities.Where(e => !e.IsDeleted).Select(MapToDto);
    }

    public async Task<IEnumerable<PaymentTermDto>> GetActiveAsync()
    {
        var entities = await _repository.GetActiveAsync();
        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<PaymentTermDto>> GetByApplicableToAsync(string applicableTo)
    {
        var entities = await _repository.GetByApplicableToAsync(applicableTo);
        return entities.Select(MapToDto);
    }

    public async Task<PaymentTermDto?> GetDefaultAsync(string? applicableTo = null)
    {
        var entity = await _repository.GetDefaultAsync(applicableTo);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<PaymentTermDto> CreateAsync(CreatePaymentTermDto dto)
    {
        NormalizeAndValidate(dto.DueDays, dto.DiscountPercent, dto.DiscountDays, dto.IsActive, dto.IsDefault, dto.ApplicableTo);
        dto.Code = dto.Code.Trim().ToUpperInvariant();
        dto.Name = dto.Name.Trim();
        dto.ApplicableTo = NormalizeApplicableTo(dto.ApplicableTo);

        // Check for unique code
        if (!await IsCodeUniqueAsync(dto.Code))
        {
            throw new InvalidOperationException($"Payment term with code '{dto.Code}' already exists.");
        }

        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot create payment term without a valid tenant context.");
        }

        var entity = new PaymentTerm
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            DueDays = dto.DueDays,
            DiscountPercent = dto.DiscountPercent,
            DiscountDays = dto.DiscountDays,
            IsActive = dto.IsActive,
            IsDefault = dto.IsDefault,
            DisplayOrder = dto.DisplayOrder,
            ApplicableTo = dto.ApplicableTo,
            CreatedAt = DateTime.UtcNow
        };

        // If this is set as default, unset other defaults
        if (dto.IsDefault)
        {
            await UnsetOtherDefaultsAsync(entity.Id, dto.ApplicableTo);
        }

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Created payment term {Code} with ID {Id}", entity.Code, entity.Id);

        return MapToDto(entity);
    }

    public async Task<PaymentTermDto> UpdateAsync(Guid id, UpdatePaymentTermDto dto)
    {
        dto.Name = dto.Name.Trim();
        dto.ApplicableTo = NormalizeApplicableTo(dto.ApplicableTo);
        NormalizeAndValidate(dto.DueDays, dto.DiscountPercent, dto.DiscountDays, dto.IsActive, dto.IsDefault, dto.ApplicableTo);

        var entity = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Payment term with ID {id} not found.");

        if (entity.IsDefault && !dto.IsDefault)
        {
            throw new InvalidOperationException("The default flag cannot be cleared directly. Set another active term as default instead.");
        }

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.DueDays = dto.DueDays;
        entity.DiscountPercent = dto.DiscountPercent;
        entity.DiscountDays = dto.DiscountDays;
        entity.IsActive = dto.IsActive;
        entity.IsDefault = dto.IsDefault;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.ApplicableTo = dto.ApplicableTo;
        entity.UpdatedAt = DateTime.UtcNow;

        // If this is set as default, unset other defaults
        if (dto.IsDefault)
        {
            await UnsetOtherDefaultsAsync(entity.Id, dto.ApplicableTo);
        }

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Updated payment term {Code} with ID {Id}", entity.Code, entity.Id);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        if (entity.IsDefault)
        {
            throw new InvalidOperationException("The default payment term cannot be deleted. Set another active term as default first.");
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Deleted payment term {Code} with ID {Id}", entity.Code, entity.Id);

        return true;
    }

    public async Task<bool> SetDefaultAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        if (!entity.IsActive)
        {
            throw new InvalidOperationException("An inactive payment term cannot be set as default.");
        }

        await UnsetOtherDefaultsAsync(id, entity.ApplicableTo);

        entity.IsDefault = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ToggleActiveAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        if (entity.IsActive && entity.IsDefault)
        {
            throw new InvalidOperationException("The default payment term cannot be deactivated. Set another active term as default first.");
        }

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        return await _repository.IsCodeUniqueAsync(code, excludeId);
    }

    private async Task UnsetOtherDefaultsAsync(Guid excludeId, string applicableTo)
    {
        var allTerms = await _repository.GetAllAsync();
        foreach (var term in allTerms.Where(t => t.Id != excludeId && t.IsDefault && 
            (t.ApplicableTo == applicableTo || t.ApplicableTo == "All" || applicableTo == "All")))
        {
            term.IsDefault = false;
            term.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(term);
        }
    }

    private static string NormalizeApplicableTo(string? applicableTo)
    {
        var normalized = string.IsNullOrWhiteSpace(applicableTo) ? "All" : applicableTo.Trim();
        return normalized.ToUpperInvariant() switch
        {
            "ALL" => "All",
            "CUSTOMER" or "CLIENT" => "Customer",
            "SUPPLIER" or "VENDOR" => "Supplier",
            "CONTRACTOR" => "Contractor",
            _ => throw new InvalidOperationException("Applicable To must be All, Customer, Supplier, or Contractor.")
        };
    }

    private static void NormalizeAndValidate(
        int dueDays,
        decimal discountPercent,
        int discountDays,
        bool isActive,
        bool isDefault,
        string? applicableTo)
    {
        _ = NormalizeApplicableTo(applicableTo);

        if (isDefault && !isActive)
        {
            throw new InvalidOperationException("A default payment term must be active.");
        }

        if (discountPercent == 0m && discountDays != 0)
        {
            throw new InvalidOperationException("Discount days must be zero when no discount percentage is configured.");
        }

        if (discountPercent > 0m && discountDays <= 0)
        {
            throw new InvalidOperationException("Discount days must be greater than zero when a discount is configured.");
        }

        if (discountPercent > 0m && (dueDays == 0 || discountDays > dueDays))
        {
            throw new InvalidOperationException("The discount window must fall within the payment due period.");
        }
    }

    private static PaymentTermDto MapToDto(PaymentTerm entity)
    {
        return new PaymentTermDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            DueDays = entity.DueDays,
            DiscountPercent = entity.DiscountPercent,
            DiscountDays = entity.DiscountDays,
            IsActive = entity.IsActive,
            IsDefault = entity.IsDefault,
            DisplayOrder = entity.DisplayOrder,
            ApplicableTo = entity.ApplicableTo,
            CreatedAt = entity.CreatedAt
        };
    }
}
