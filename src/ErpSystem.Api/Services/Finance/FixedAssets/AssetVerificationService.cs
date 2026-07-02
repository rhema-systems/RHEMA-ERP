using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class AssetVerificationService : IAssetVerificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _documentNumberingService;

    public AssetVerificationService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService)
    {
        _context = context;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
    }

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
    private string UserName => _currentUser.UserName ?? "system";
    private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    public async Task<AssetVerificationSessionDto?> GetSessionByIdAsync(Guid sessionId)
    {
        var session = await _context.AssetVerificationSessions
            .Include(s => s.VerifiedBy)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == sessionId);

        return session == null ? null : MapToDto(session);
    }

    public async Task<IEnumerable<AssetVerificationSessionDto>> GetAllSessionsAsync()
    {
        var sessions = await _context.AssetVerificationSessions
            .Include(s => s.Items)
            .Where(s => s.TenantId == TenantId)
            .OrderByDescending(s => s.ScheduledDate)
            .ToListAsync();

        return sessions.Select(MapToDto).ToList();
    }

    public async Task<AssetVerificationSessionDto> CreateSessionAsync(CreateAssetVerificationSessionDto dto)
    {
        var session = new AssetVerificationSession
        {
            TenantId = TenantId,
            SessionName = dto.SessionName,
            ScheduledDate = dto.ScheduledDate,
            Description = dto.Description,
            Status = VerificationSessionStatus.Draft,
            ReferenceNumber = await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.AssetVerification,
                TenantId,
                dto.ScheduledDate,
                nameof(AssetVerificationSession)),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.AssetVerificationSessions.Add(session);

        // Add items
        List<FixedAsset> assetsToVerify;
        if (dto.AssetIds != null && dto.AssetIds.Any())
        {
            assetsToVerify = await _context.FixedAssets
                .Where(a => a.TenantId == TenantId && dto.AssetIds.Contains(a.Id))
                .ToListAsync();
        }
        else
        {
            assetsToVerify = await _context.FixedAssets
                .Where(a => a.TenantId == TenantId && (a.Status == FixedAssetStatus.Active || a.Status == FixedAssetStatus.FullyDepreciated))
                .ToListAsync();
        }

        foreach (var asset in assetsToVerify)
        {
            _context.AssetVerificationItems.Add(new AssetVerificationItem
            {
                TenantId = TenantId,
                Session = session,
                FixedAssetId = asset.Id,
                IsVerified = false,
                Condition = AssetCondition.Good,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            });
        }

        await _context.SaveChangesAsync();
        return MapToDto(session);
    }

    public async Task<AssetVerificationSessionDto> StartSessionAsync(Guid sessionId)
    {
        var session = await _context.AssetVerificationSessions
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        if (session.Status != VerificationSessionStatus.Draft)
        {
            throw new InvalidOperationException("Only draft sessions can be started.");
        }

        session.Status = VerificationSessionStatus.InProgress;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = UserName;

        await _context.SaveChangesAsync();
        return MapToDto(session);
    }

    public async Task<AssetVerificationSessionDto> CompleteSessionAsync(Guid sessionId)
    {
        var session = await _context.AssetVerificationSessions
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        session.Status = VerificationSessionStatus.Completed;
        session.CompletionDate = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = UserName;

        await _context.SaveChangesAsync();
        return MapToDto(session);
    }

    public async Task<AssetVerificationItemDto> VerifyAssetAsync(Guid sessionId, Guid assetId, VerifyAssetDto dto)
    {
        var item = await _context.AssetVerificationItems
            .Include(i => i.FixedAsset)
            .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.SessionId == sessionId && i.FixedAssetId == assetId)
            ?? throw new KeyNotFoundException("Asset not found in this session.");

        item.IsVerified = true;
        item.VerificationDate = DateTime.UtcNow;
        item.Condition = dto.Condition;
        item.CurrentLocation = dto.CurrentLocation;
        item.Notes = dto.Notes;
        item.ImageUrl = dto.ImageUrl;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = UserName;

        if (!string.IsNullOrWhiteSpace(dto.CurrentLocation) && item.FixedAsset != null)
        {
            item.FixedAsset.Location = dto.CurrentLocation.Trim();
            item.FixedAsset.UpdatedAt = DateTime.UtcNow;
            item.FixedAsset.UpdatedBy = UserName;
        }

        await _context.SaveChangesAsync();
        return MapToItemDto(item);
    }

    public async Task<IEnumerable<AssetVerificationItemDto>> GetSessionItemsAsync(Guid sessionId)
    {
        var items = await _context.AssetVerificationItems
            .Include(i => i.FixedAsset)
            .Where(i => i.TenantId == TenantId && i.SessionId == sessionId)
            .ToListAsync();

        return items.Select(MapToItemDto).ToList();
    }

    private static AssetVerificationSessionDto MapToDto(AssetVerificationSession s)
    {
        return new AssetVerificationSessionDto
        {
            Id = s.Id,
            SessionName = s.SessionName,
            ScheduledDate = s.ScheduledDate,
            CompletionDate = s.CompletionDate,
            Status = s.Status,
            Description = s.Description,
            ReferenceNumber = s.ReferenceNumber,
            VerifiedById = s.VerifiedById,
            VerifiedByName = s.VerifiedBy != null ? $"{s.VerifiedBy.FirstName} {s.VerifiedBy.LastName}" : null,
            TotalItems = s.Items?.Count ?? 0,
            VerifiedItems = s.Items?.Count(i => i.IsVerified) ?? 0
        };
    }

    private static AssetVerificationItemDto MapToItemDto(AssetVerificationItem i)
    {
        return new AssetVerificationItemDto
        {
            Id = i.Id,
            SessionId = i.SessionId,
            FixedAssetId = i.FixedAssetId,
            FixedAssetName = i.FixedAsset?.Name,
            AssetCode = i.FixedAsset?.AssetCode,
            IsVerified = i.IsVerified,
            VerificationDate = i.VerificationDate,
            Condition = i.Condition,
            CurrentLocation = i.CurrentLocation ?? i.FixedAsset?.Location,
            Notes = i.Notes,
            ImageUrl = i.ImageUrl
        };
    }
}
