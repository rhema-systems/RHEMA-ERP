using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ContractService : IContractService
{
    private readonly IContractRepository _contractRepository;
    private readonly IContractMilestoneRepository _milestoneRepository;
    private readonly IContractAmendmentRepository _amendmentRepository;
    private readonly IContractDocumentRepository _documentRepository;
    private readonly ITenderAwardRepository _awardRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ContractService> _logger;

    public ContractService(
        IContractRepository contractRepository,
        IContractMilestoneRepository milestoneRepository,
        IContractAmendmentRepository amendmentRepository,
        IContractDocumentRepository documentRepository,
        ITenderAwardRepository awardRepository,
        ITenderRepository tenderRepository,
        ITenderBidRepository bidRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ContractService> logger)
    {
        _contractRepository = contractRepository;
        _milestoneRepository = milestoneRepository;
        _amendmentRepository = amendmentRepository;
        _documentRepository = documentRepository;
        _awardRepository = awardRepository;
        _tenderRepository = tenderRepository;
        _bidRepository = bidRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region Contract CRUD

    public async Task<ContractDto?> GetByIdAsync(Guid id)
    {
        try
        {
            var contract = await _contractRepository.GetByIdWithDetailsAsync(id);
            return contract != null ? MapToDto(contract) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contract {ContractId}", id);
            throw;
        }
    }

    public async Task<ContractDto?> GetByContractNumberAsync(string contractNumber)
    {
        try
        {
            var contract = await _contractRepository.GetByContractNumberAsync(contractNumber);
            if (contract == null) return null;

            var fullContract = await _contractRepository.GetByIdWithDetailsAsync(contract.Id);
            return fullContract != null ? MapToDto(fullContract) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contract by number {ContractNumber}", contractNumber);
            throw;
        }
    }

    public async Task<ContractDto?> GetByAwardIdAsync(Guid awardId)
    {
        try
        {
            var contract = await _contractRepository.GetByAwardIdAsync(awardId);
            if (contract == null) return null;

            var fullContract = await _contractRepository.GetByIdWithDetailsAsync(contract.Id);
            return fullContract != null ? MapToDto(fullContract) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contract for award {AwardId}", awardId);
            throw;
        }
    }

    public async Task<PagedResult<ContractListDto>> GetContractsAsync(int page, int pageSize, string? search = null, string? status = null, string? contractType = null)
    {
        try
        {
            var pagedContracts = await _contractRepository.GetContractsAsync(page, pageSize, search, status, contractType);
            return new PagedResult<ContractListDto>
            {
                Items = pagedContracts.Items.Select(MapToListDto).ToList(),
                TotalCount = pagedContracts.TotalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contracts");
            throw;
        }
    }

    public async Task<IEnumerable<ContractListDto>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        try
        {
            var contracts = await _contractRepository.GetByBusinessPartnerIdAsync(businessPartnerId);
            return contracts.Select(MapToListDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contracts for business partner {PartnerId}", businessPartnerId);
            throw;
        }
    }

    public async Task<IEnumerable<ContractListDto>> GetActiveContractsAsync()
    {
        try
        {
            var contracts = await _contractRepository.GetActiveContractsAsync();
            return contracts.Select(MapToListDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active contracts");
            throw;
        }
    }

    public async Task<IEnumerable<ContractListDto>> GetExpiringContractsAsync(int daysThreshold = 30)
    {
        try
        {
            var contracts = await _contractRepository.GetExpiringContractsAsync(daysThreshold);
            return contracts.Select(MapToListDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expiring contracts");
            throw;
        }
    }

    public async Task<ContractDto> CreateFromAwardAsync(CreateContractDto dto)
    {
        try
        {
            var award = await _awardRepository.GetByIdAsync(dto.TenderAwardId)
                ?? throw new InvalidOperationException($"Award with ID {dto.TenderAwardId} not found");

            // Check if contract already exists for this award
            var existingContract = await _contractRepository.GetByAwardIdAsync(dto.TenderAwardId);
            if (existingContract != null)
            {
                throw new InvalidOperationException($"Contract already exists for this award: {existingContract.ContractNumber}");
            }

            var tender = await _tenderRepository.GetByIdAsync(award.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {award.TenderId} not found");

            var contract = new Contract
            {
                ContractNumber = await _contractRepository.GenerateContractNumberAsync(),
                ContractTitle = dto.ContractTitle,
                ContractType = dto.ContractType,
                Status = "Draft",
                TenderAwardId = dto.TenderAwardId,
                TenderId = award.TenderId,
                BusinessPartnerId = award.BusinessPartnerId,
                TenderBidId = award.TenderBidId,
                ContractValue = dto.ContractValue,
                Currency = dto.Currency,
                PaymentTerms = dto.PaymentTerms,
                RetentionPercentage = dto.RetentionPercentage,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                DurationDays = dto.DurationDays ?? (dto.StartDate.HasValue && dto.EndDate.HasValue
                    ? (int)(dto.EndDate.Value - dto.StartDate.Value).TotalDays
                    : null),
                WarrantyPeriodDays = dto.WarrantyPeriodDays,
                ScopeOfWork = dto.ScopeOfWork,
                Deliverables = dto.Deliverables,
                SpecialConditions = dto.SpecialConditions,
                PenaltyClause = dto.PenaltyClause,
                Notes = dto.Notes,
                CreatedById = _currentUserProvider.UserId,
                TenantId = award.TenantId
            };

            await _contractRepository.CreateAsync(contract);

            // Create milestones if provided
            if (dto.Milestones != null && dto.Milestones.Any())
            {
                var sequenceNumber = 1;
                foreach (var milestoneDto in dto.Milestones.OrderBy(m => m.SequenceNumber))
                {
                    var milestone = new ContractMilestone
                    {
                        ContractId = contract.Id,
                        MilestoneName = milestoneDto.MilestoneName,
                        Description = milestoneDto.Description,
                        SequenceNumber = sequenceNumber++,
                        PaymentPercentage = milestoneDto.PaymentPercentage,
                        PaymentAmount = dto.ContractValue * (milestoneDto.PaymentPercentage / 100),
                        PlannedDate = milestoneDto.PlannedDate,
                        Notes = milestoneDto.Notes,
                        TenantId = award.TenantId
                    };
                    await _milestoneRepository.CreateAsync(milestone);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created contract {ContractNumber} for award {AwardId}", contract.ContractNumber, dto.TenderAwardId);

            var createdContract = await _contractRepository.GetByIdWithDetailsAsync(contract.Id);
            return MapToDto(createdContract!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contract for award {AwardId}", dto.TenderAwardId);
            throw;
        }
    }

    public async Task<ContractDto> UpdateAsync(Guid id, UpdateContractDto dto)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Contract with ID {id} not found");

            if (contract.Status == "Active" || contract.Status == "Completed" || contract.Status == "Terminated")
            {
                throw new InvalidOperationException($"Cannot edit contract in {contract.Status} status. Use amendments for changes.");
            }

            if (!string.IsNullOrEmpty(dto.ContractTitle)) contract.ContractTitle = dto.ContractTitle;
            if (!string.IsNullOrEmpty(dto.ContractType)) contract.ContractType = dto.ContractType;
            if (dto.ContractValue.HasValue) contract.ContractValue = dto.ContractValue.Value;
            if (!string.IsNullOrEmpty(dto.PaymentTerms)) contract.PaymentTerms = dto.PaymentTerms;
            if (dto.RetentionPercentage.HasValue) contract.RetentionPercentage = dto.RetentionPercentage.Value;
            if (dto.StartDate.HasValue) contract.StartDate = dto.StartDate.Value;
            if (dto.EndDate.HasValue) contract.EndDate = dto.EndDate.Value;
            if (dto.DurationDays.HasValue) contract.DurationDays = dto.DurationDays.Value;
            if (dto.WarrantyPeriodDays.HasValue) contract.WarrantyPeriodDays = dto.WarrantyPeriodDays.Value;
            if (!string.IsNullOrEmpty(dto.ScopeOfWork)) contract.ScopeOfWork = dto.ScopeOfWork;
            if (!string.IsNullOrEmpty(dto.Deliverables)) contract.Deliverables = dto.Deliverables;
            if (!string.IsNullOrEmpty(dto.SpecialConditions)) contract.SpecialConditions = dto.SpecialConditions;
            if (!string.IsNullOrEmpty(dto.PenaltyClause)) contract.PenaltyClause = dto.PenaltyClause;
            if (!string.IsNullOrEmpty(dto.Notes)) contract.Notes = dto.Notes;

            await _contractRepository.UpdateAsync(contract);
            await _unitOfWork.SaveChangesAsync();

            var updatedContract = await _contractRepository.GetByIdWithDetailsAsync(id);
            return MapToDto(updatedContract!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contract {ContractId}", id);
            throw;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Contract with ID {id} not found");

            if (contract.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft contracts can be deleted");
            }

            await _contractRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted contract {ContractId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting contract {ContractId}", id);
            throw;
        }
    }

    #endregion

    #region Status Management

    public async Task<ContractDto> UpdateStatusAsync(Guid id, UpdateContractStatusDto dto)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Contract with ID {id} not found");

            contract.Status = dto.Status;

            if (dto.SignedDate.HasValue) contract.SignedDate = dto.SignedDate.Value;
            if (!string.IsNullOrEmpty(dto.SignedByName)) contract.SignedByName = dto.SignedByName;
            if (!string.IsNullOrEmpty(dto.ContractorSignatoryName)) contract.ContractorSignatoryName = dto.ContractorSignatoryName;
            if (dto.ContractorSignedDate.HasValue) contract.ContractorSignedDate = dto.ContractorSignedDate.Value;
            if (!string.IsNullOrEmpty(dto.TerminationReason)) contract.TerminationReason = dto.TerminationReason;
            if (!string.IsNullOrEmpty(dto.Notes)) contract.Notes = dto.Notes;

            await _contractRepository.UpdateAsync(contract);
            await _unitOfWork.SaveChangesAsync();

            var updatedContract = await _contractRepository.GetByIdWithDetailsAsync(id);
            return MapToDto(updatedContract!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contract status {ContractId}", id);
            throw;
        }
    }

    public async Task<ContractDto> ActivateContractAsync(Guid id, UpdateContractStatusDto dto)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Contract with ID {id} not found");

            if (contract.Status != "Draft" && contract.Status != "PendingSignature")
            {
                throw new InvalidOperationException($"Cannot activate contract in {contract.Status} status");
            }

            contract.Status = "Active";
            contract.ActivatedAt = DateTime.UtcNow;
            contract.SignedDate = dto.SignedDate ?? DateTime.UtcNow;
            contract.SignedByName = dto.SignedByName;
            contract.SignedById = _currentUserProvider.UserId;
            contract.ContractorSignatoryName = dto.ContractorSignatoryName;
            contract.ContractorSignedDate = dto.ContractorSignedDate;

            await _contractRepository.UpdateAsync(contract);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Activated contract {ContractNumber}", contract.ContractNumber);

            var updatedContract = await _contractRepository.GetByIdWithDetailsAsync(id);
            return MapToDto(updatedContract!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating contract {ContractId}", id);
            throw;
        }
    }

    public async Task<ContractDto> CompleteContractAsync(Guid id)
    {
        try
        {
            var contract = await _contractRepository.GetByIdWithDetailsAsync(id)
                ?? throw new InvalidOperationException($"Contract with ID {id} not found");

            if (contract.Status != "Active")
            {
                throw new InvalidOperationException($"Cannot complete contract in {contract.Status} status");
            }

            // Check if all milestones are completed or paid
            var incompleteMilestones = contract.Milestones.Where(m => !m.IsDeleted && m.Status != "Completed" && m.Status != "Paid").ToList();
            if (incompleteMilestones.Any())
            {
                throw new InvalidOperationException($"Cannot complete contract. {incompleteMilestones.Count} milestones are not completed");
            }

            contract.Status = "Completed";
            contract.CompletedAt = DateTime.UtcNow;

            await _contractRepository.UpdateAsync(contract);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Completed contract {ContractNumber}", contract.ContractNumber);

            var updatedContract = await _contractRepository.GetByIdWithDetailsAsync(id);
            return MapToDto(updatedContract!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing contract {ContractId}", id);
            throw;
        }
    }

    public async Task<ContractDto> TerminateContractAsync(Guid id, string reason)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Contract with ID {id} not found");

            if (contract.Status == "Completed" || contract.Status == "Terminated")
            {
                throw new InvalidOperationException($"Contract is already {contract.Status}");
            }

            contract.Status = "Terminated";
            contract.TerminatedAt = DateTime.UtcNow;
            contract.TerminationReason = reason;

            await _contractRepository.UpdateAsync(contract);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Terminated contract {ContractNumber}: {Reason}", contract.ContractNumber, reason);

            var updatedContract = await _contractRepository.GetByIdWithDetailsAsync(id);
            return MapToDto(updatedContract!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating contract {ContractId}", id);
            throw;
        }
    }

    #endregion

    #region Milestones

    public async Task<ContractMilestoneDto> AddMilestoneAsync(Guid contractId, CreateContractMilestoneDto dto)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractId)
                ?? throw new InvalidOperationException($"Contract with ID {contractId} not found");

            var existingMilestones = await _milestoneRepository.GetByContractIdAsync(contractId);
            var nextSequence = existingMilestones.Any() ? existingMilestones.Max(m => m.SequenceNumber) + 1 : 1;

            var milestone = new ContractMilestone
            {
                ContractId = contractId,
                MilestoneName = dto.MilestoneName,
                Description = dto.Description,
                SequenceNumber = dto.SequenceNumber > 0 ? dto.SequenceNumber : nextSequence,
                PaymentPercentage = dto.PaymentPercentage,
                PaymentAmount = contract.ContractValue * (dto.PaymentPercentage / 100),
                PlannedDate = dto.PlannedDate,
                Notes = dto.Notes,
                TenantId = contract.TenantId
            };

            await _milestoneRepository.CreateAsync(milestone);
            await _unitOfWork.SaveChangesAsync();

            return MapMilestoneToDto(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding milestone to contract {ContractId}", contractId);
            throw;
        }
    }

    public async Task<ContractMilestoneDto> UpdateMilestoneAsync(Guid milestoneId, UpdateContractMilestoneDto dto)
    {
        try
        {
            var milestone = await _milestoneRepository.GetByIdAsync(milestoneId)
                ?? throw new InvalidOperationException($"Milestone with ID {milestoneId} not found");

            if (!string.IsNullOrEmpty(dto.MilestoneName)) milestone.MilestoneName = dto.MilestoneName;
            if (dto.Description != null) milestone.Description = dto.Description;
            if (dto.SequenceNumber.HasValue) milestone.SequenceNumber = dto.SequenceNumber.Value;
            if (dto.PaymentPercentage.HasValue)
            {
                milestone.PaymentPercentage = dto.PaymentPercentage.Value;
                var contract = await _contractRepository.GetByIdAsync(milestone.ContractId);
                if (contract != null)
                {
                    milestone.PaymentAmount = contract.ContractValue * (dto.PaymentPercentage.Value / 100);
                }
            }
            if (dto.PlannedDate.HasValue) milestone.PlannedDate = dto.PlannedDate.Value;
            if (dto.Notes != null) milestone.Notes = dto.Notes;

            await _milestoneRepository.UpdateAsync(milestone);
            await _unitOfWork.SaveChangesAsync();

            return MapMilestoneToDto(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task<ContractMilestoneDto> UpdateMilestoneStatusAsync(Guid milestoneId, UpdateMilestoneStatusDto dto)
    {
        try
        {
            var milestone = await _milestoneRepository.GetByIdAsync(milestoneId)
                ?? throw new InvalidOperationException($"Milestone with ID {milestoneId} not found");

            milestone.Status = dto.Status;

            switch (dto.Status)
            {
                case "Completed":
                    milestone.CompletedAt = DateTime.UtcNow;
                    milestone.ActualDate = DateTime.UtcNow;
                    break;
                case "Invoiced":
                    milestone.InvoicedAt = DateTime.UtcNow;
                    milestone.InvoiceNumber = dto.InvoiceNumber;
                    break;
                case "Paid":
                    milestone.PaidAt = DateTime.UtcNow;
                    break;
            }

            if (!string.IsNullOrEmpty(dto.Notes)) milestone.Notes = dto.Notes;

            await _milestoneRepository.UpdateAsync(milestone);
            await _unitOfWork.SaveChangesAsync();

            return MapMilestoneToDto(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating milestone status {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task DeleteMilestoneAsync(Guid milestoneId)
    {
        try
        {
            var milestone = await _milestoneRepository.GetByIdAsync(milestoneId)
                ?? throw new InvalidOperationException($"Milestone with ID {milestoneId} not found");

            if (milestone.Status != "Pending")
            {
                throw new InvalidOperationException("Cannot delete a milestone that is already in progress or completed");
            }

            await _milestoneRepository.DeleteAsync(milestoneId);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task<IEnumerable<ContractMilestoneDto>> GetMilestonesByContractIdAsync(Guid contractId)
    {
        try
        {
            var milestones = await _milestoneRepository.GetByContractIdAsync(contractId);
            return milestones.Select(MapMilestoneToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving milestones for contract {ContractId}", contractId);
            throw;
        }
    }

    #endregion

    #region Amendments

    public async Task<ContractAmendmentDto> CreateAmendmentAsync(Guid contractId, CreateContractAmendmentDto dto)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractId)
                ?? throw new InvalidOperationException($"Contract with ID {contractId} not found");

            if (contract.Status != "Active")
            {
                throw new InvalidOperationException("Amendments can only be created for active contracts");
            }

            var existingAmendments = await _amendmentRepository.GetByContractIdAsync(contractId);
            var nextSequence = existingAmendments.Any() ? existingAmendments.Max(a => a.SequenceNumber) + 1 : 1;

            var amendment = new ContractAmendment
            {
                ContractId = contractId,
                AmendmentNumber = await _amendmentRepository.GenerateAmendmentNumberAsync(contractId),
                SequenceNumber = nextSequence,
                AmendmentType = dto.AmendmentType,
                Reason = dto.Reason,
                Description = dto.Description,
                Status = "PendingApproval",
                RequestedDate = DateTime.UtcNow,
                RequestedById = _currentUserProvider.UserId,
                Notes = dto.Notes,
                TenantId = contract.TenantId
            };

            // Set change-specific fields
            switch (dto.AmendmentType)
            {
                case "ValueChange":
                    amendment.PreviousValue = contract.ContractValue;
                    amendment.NewValue = dto.NewValue;
                    amendment.ValueChange = dto.NewValue.HasValue ? dto.NewValue.Value - contract.ContractValue : null;
                    break;
                case "TimelineExtension":
                    amendment.PreviousEndDate = contract.EndDate;
                    amendment.NewEndDate = dto.NewEndDate;
                    if (contract.EndDate.HasValue && dto.NewEndDate.HasValue)
                    {
                        amendment.DaysExtended = (int)(dto.NewEndDate.Value - contract.EndDate.Value).TotalDays;
                    }
                    break;
                case "ScopeChange":
                    amendment.ScopeChanges = dto.ScopeChanges;
                    break;
            }

            await _amendmentRepository.CreateAsync(amendment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created amendment {AmendmentNumber} for contract {ContractNumber}", amendment.AmendmentNumber, contract.ContractNumber);

            return MapAmendmentToDto(amendment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating amendment for contract {ContractId}", contractId);
            throw;
        }
    }

    public async Task<ContractAmendmentDto> ProcessAmendmentAsync(Guid amendmentId, ProcessAmendmentDto dto)
    {
        try
        {
            var amendment = await _amendmentRepository.GetByIdAsync(amendmentId)
                ?? throw new InvalidOperationException($"Amendment with ID {amendmentId} not found");

            if (amendment.Status != "PendingApproval")
            {
                throw new InvalidOperationException($"Amendment is already {amendment.Status}");
            }

            amendment.ApprovedById = _currentUserProvider.UserId;
            amendment.ApprovedDate = DateTime.UtcNow;
            amendment.ApprovalNotes = dto.Notes;

            if (dto.Approved)
            {
                amendment.Status = "Approved";

                // Apply the amendment to the contract
                var contract = await _contractRepository.GetByIdAsync(amendment.ContractId)
                    ?? throw new InvalidOperationException("Contract not found");

                switch (amendment.AmendmentType)
                {
                    case "ValueChange":
                        if (amendment.NewValue.HasValue)
                        {
                            contract.ContractValue = amendment.NewValue.Value;
                            // Recalculate milestone amounts
                            var milestones = await _milestoneRepository.GetByContractIdAsync(contract.Id);
                            foreach (var milestone in milestones)
                            {
                                milestone.PaymentAmount = contract.ContractValue * (milestone.PaymentPercentage / 100);
                                await _milestoneRepository.UpdateAsync(milestone);
                            }
                        }
                        break;
                    case "TimelineExtension":
                        if (amendment.NewEndDate.HasValue)
                        {
                            contract.EndDate = amendment.NewEndDate.Value;
                            if (contract.StartDate.HasValue)
                            {
                                contract.DurationDays = (int)(amendment.NewEndDate.Value - contract.StartDate.Value).TotalDays;
                            }
                        }
                        break;
                    case "ScopeChange":
                        if (!string.IsNullOrEmpty(amendment.ScopeChanges))
                        {
                            contract.ScopeOfWork = (contract.ScopeOfWork ?? "") + "\n\n[Amendment " + amendment.AmendmentNumber + "]: " + amendment.ScopeChanges;
                        }
                        break;
                }

                await _contractRepository.UpdateAsync(contract);
                _logger.LogInformation("Approved and applied amendment {AmendmentNumber}", amendment.AmendmentNumber);
            }
            else
            {
                amendment.Status = "Rejected";
                _logger.LogInformation("Rejected amendment {AmendmentNumber}", amendment.AmendmentNumber);
            }

            await _amendmentRepository.UpdateAsync(amendment);
            await _unitOfWork.SaveChangesAsync();

            var updatedAmendment = await _amendmentRepository.GetByIdAsync(amendmentId);
            return MapAmendmentToDto(updatedAmendment!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing amendment {AmendmentId}", amendmentId);
            throw;
        }
    }

    public async Task DeleteAmendmentAsync(Guid amendmentId)
    {
        try
        {
            var amendment = await _amendmentRepository.GetByIdAsync(amendmentId)
                ?? throw new InvalidOperationException($"Amendment with ID {amendmentId} not found");

            if (amendment.Status != "Draft" && amendment.Status != "PendingApproval")
            {
                throw new InvalidOperationException("Cannot delete an approved or rejected amendment");
            }

            await _amendmentRepository.DeleteAsync(amendmentId);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting amendment {AmendmentId}", amendmentId);
            throw;
        }
    }

    public async Task<IEnumerable<ContractAmendmentDto>> GetAmendmentsByContractIdAsync(Guid contractId)
    {
        try
        {
            var amendments = await _amendmentRepository.GetByContractIdAsync(contractId);
            return amendments.Select(MapAmendmentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving amendments for contract {ContractId}", contractId);
            throw;
        }
    }

    public async Task<IEnumerable<ContractAmendmentDto>> GetPendingAmendmentsAsync()
    {
        try
        {
            var amendments = await _amendmentRepository.GetPendingApprovalAsync();
            return amendments.Select(MapAmendmentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending amendments");
            throw;
        }
    }

    #endregion

    #region Documents

    public async Task<ContractDocumentDto> UploadDocumentAsync(Guid contractId, string documentType, string fileName, string filePath, string? contentType, long? fileSize, string? description)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractId)
                ?? throw new InvalidOperationException($"Contract with ID {contractId} not found");

            var document = new ContractDocument
            {
                ContractId = contractId,
                DocumentType = documentType,
                FileName = fileName,
                FilePath = filePath,
                ContentType = contentType,
                FileSize = fileSize,
                Description = description,
                UploadedById = _currentUserProvider.UserId,
                TenantId = contract.TenantId
            };

            await _documentRepository.CreateAsync(document);
            await _unitOfWork.SaveChangesAsync();

            return MapDocumentToDto(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to contract {ContractId}", contractId);
            throw;
        }
    }

    public async Task DeleteDocumentAsync(Guid documentId)
    {
        try
        {
            await _documentRepository.DeleteAsync(documentId);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<ContractDocumentDto>> GetDocumentsByContractIdAsync(Guid contractId)
    {
        try
        {
            var documents = await _documentRepository.GetByContractIdAsync(contractId);
            return documents.Select(MapDocumentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for contract {ContractId}", contractId);
            throw;
        }
    }

    #endregion

    #region Mapping Methods

    private ContractDto MapToDto(Contract contract)
    {
        var paidMilestones = contract.Milestones.Where(m => !m.IsDeleted && m.Status == "Paid").ToList();
        var completedMilestones = contract.Milestones.Where(m => !m.IsDeleted && (m.Status == "Completed" || m.Status == "Invoiced" || m.Status == "Paid")).ToList();

        return new ContractDto
        {
            Id = contract.Id,
            ContractNumber = contract.ContractNumber,
            ContractTitle = contract.ContractTitle,
            ContractType = contract.ContractType,
            Status = contract.Status,
            TenderAwardId = contract.TenderAwardId,
            TenderId = contract.TenderId,
            TenderNumber = contract.Tender?.TenderNumber ?? "",
            TenderTitle = contract.Tender?.Title ?? "",
            BusinessPartnerId = contract.BusinessPartnerId,
            BusinessPartnerName = contract.BusinessPartner?.PartnerName ?? "",
            TenderBidId = contract.TenderBidId,
            ContractValue = contract.ContractValue,
            Currency = contract.Currency,
            PaymentTerms = contract.PaymentTerms,
            RetentionPercentage = contract.RetentionPercentage,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            DurationDays = contract.DurationDays,
            WarrantyPeriodDays = contract.WarrantyPeriodDays,
            ScopeOfWork = contract.ScopeOfWork,
            Deliverables = contract.Deliverables,
            SpecialConditions = contract.SpecialConditions,
            PenaltyClause = contract.PenaltyClause,
            SignedDate = contract.SignedDate,
            SignedByName = contract.SignedByName ?? contract.SignedBy?.UserName,
            ContractorSignatoryName = contract.ContractorSignatoryName,
            ContractorSignedDate = contract.ContractorSignedDate,
            ContractDocumentPath = contract.ContractDocumentPath,
            Notes = contract.Notes,
            CreatedAt = contract.CreatedAt,
            CreatedByName = contract.CreatedBy?.UserName,
            ActivatedAt = contract.ActivatedAt,
            CompletedAt = contract.CompletedAt,
            TerminatedAt = contract.TerminatedAt,
            TerminationReason = contract.TerminationReason,
            Milestones = contract.Milestones.Where(m => !m.IsDeleted).Select(MapMilestoneToDto).OrderBy(m => m.SequenceNumber).ToList(),
            Amendments = contract.Amendments.Where(a => !a.IsDeleted).Select(MapAmendmentToDto).OrderBy(a => a.SequenceNumber).ToList(),
            Documents = contract.Documents.Where(d => !d.IsDeleted).Select(MapDocumentToDto).ToList(),
            TotalPaidAmount = paidMilestones.Sum(m => m.PaymentAmount),
            RemainingAmount = contract.ContractValue - paidMilestones.Sum(m => m.PaymentAmount),
            CompletedMilestones = completedMilestones.Count,
            TotalMilestones = contract.Milestones.Count(m => !m.IsDeleted)
        };
    }

    private ContractListDto MapToListDto(Contract contract)
    {
        return new ContractListDto
        {
            Id = contract.Id,
            ContractNumber = contract.ContractNumber,
            ContractTitle = contract.ContractTitle,
            ContractType = contract.ContractType,
            Status = contract.Status,
            TenderNumber = contract.Tender?.TenderNumber ?? "",
            BusinessPartnerName = contract.BusinessPartner?.PartnerName ?? "",
            ContractValue = contract.ContractValue,
            Currency = contract.Currency,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            CreatedAt = contract.CreatedAt
        };
    }

    private ContractMilestoneDto MapMilestoneToDto(ContractMilestone milestone)
    {
        return new ContractMilestoneDto
        {
            Id = milestone.Id,
            ContractId = milestone.ContractId,
            MilestoneName = milestone.MilestoneName,
            Description = milestone.Description,
            SequenceNumber = milestone.SequenceNumber,
            PaymentPercentage = milestone.PaymentPercentage,
            PaymentAmount = milestone.PaymentAmount,
            PlannedDate = milestone.PlannedDate,
            ActualDate = milestone.ActualDate,
            Status = milestone.Status,
            CompletedAt = milestone.CompletedAt,
            InvoicedAt = milestone.InvoicedAt,
            PaidAt = milestone.PaidAt,
            InvoiceNumber = milestone.InvoiceNumber,
            Notes = milestone.Notes
        };
    }

    private ContractAmendmentDto MapAmendmentToDto(ContractAmendment amendment)
    {
        return new ContractAmendmentDto
        {
            Id = amendment.Id,
            ContractId = amendment.ContractId,
            AmendmentNumber = amendment.AmendmentNumber,
            SequenceNumber = amendment.SequenceNumber,
            AmendmentType = amendment.AmendmentType,
            Reason = amendment.Reason,
            Description = amendment.Description,
            PreviousValue = amendment.PreviousValue,
            NewValue = amendment.NewValue,
            ValueChange = amendment.ValueChange,
            PreviousEndDate = amendment.PreviousEndDate,
            NewEndDate = amendment.NewEndDate,
            DaysExtended = amendment.DaysExtended,
            ScopeChanges = amendment.ScopeChanges,
            Status = amendment.Status,
            RequestedDate = amendment.RequestedDate,
            RequestedByName = amendment.RequestedBy?.UserName,
            ApprovedDate = amendment.ApprovedDate,
            ApprovedByName = amendment.ApprovedBy?.UserName,
            ApprovalNotes = amendment.ApprovalNotes,
            DocumentPath = amendment.DocumentPath,
            Notes = amendment.Notes
        };
    }

    private ContractDocumentDto MapDocumentToDto(ContractDocument document)
    {
        return new ContractDocumentDto
        {
            Id = document.Id,
            ContractId = document.ContractId,
            DocumentType = document.DocumentType,
            FileName = document.FileName,
            FilePath = document.FilePath,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            Description = document.Description,
            UploadedByName = document.UploadedBy?.UserName,
            CreatedAt = document.CreatedAt
        };
    }

    #endregion
}

