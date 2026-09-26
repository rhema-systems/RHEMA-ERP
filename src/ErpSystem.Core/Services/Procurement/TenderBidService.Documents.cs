using System.Data;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public partial class TenderBidService
{
    public async Task ValidateBidDocumentChangeAsync(Guid bidId, Guid? tenderItemId = null)
    {
        var bid = await RequireEditableDocumentBidAsync(bidId);
        await ResolveDocumentBidItemAsync(bid, tenderItemId);
    }

    private async Task<TenderBid> RequireEditableDocumentBidAsync(Guid bidId)
    {
        if (!_currentUserProvider.IsAuthenticated || _currentUserProvider.UserId == Guid.Empty ||
            _currentUserProvider.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Sign in to manage bid documents.");

        // A fresh SQL read under the mutation's serializable transaction also protects against
        // another request submitting the bid between validation and document persistence.
        var bid = await _unitOfWork.Repository<TenderBid>()
            .GetQueryable(value => value.Id == bidId && value.TenantId == _currentUserProvider.TenantId && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync()
            ?? throw new TenderBidInitiationValidationException("BID_DOCUMENT_BID_NOT_FOUND", "The bid was not found.");

        if (_currentUserProvider.IsExternalUser)
        {
            var partner = await GetCurrentBusinessPartnerAsync();
            if (partner == null || partner.TenantId != bid.TenantId || partner.Id != bid.BusinessPartnerId)
                throw new UnauthorizedAccessException("Suppliers can manage documents only for their own bids.");
        }
        else if (!_currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.SuperAdmin) &&
                 !_currentUserProvider.HasRegisteredProcurementPermission("procurement.sourcing.manage"))
        {
            throw new UnauthorizedAccessException("Tender management permission is required to manage bid documents.");
        }

        if (bid.Status != "Draft")
            throw new TenderBidInitiationValidationException("BID_DOCUMENT_LOCKED", "Documents can be changed only while the bid is a draft.");

        var tender = await _unitOfWork.Repository<Tender>()
            .GetQueryable(value => value.Id == bid.TenderId && value.TenantId == bid.TenantId && !value.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync()
            ?? throw new TenderBidInitiationValidationException("BID_DOCUMENT_TENDER_NOT_FOUND", "The tender was not found.");
        if (tender.Status != "Published" || !tender.SubmissionDeadline.HasValue ||
            DateTime.UtcNow >= tender.SubmissionDeadline.Value)
            throw new TenderBidInitiationValidationException("BID_DOCUMENT_CUTOFF", "Bid documents cannot be changed after the tender closes or its submission deadline is reached.");
        return bid;
    }

    private async Task<TenderBidItem?> ResolveDocumentBidItemAsync(TenderBid bid, Guid? tenderItemId)
    {
        if (!tenderItemId.HasValue) return null;
        var item = await _unitOfWork.Repository<TenderBidItem>()
            .GetQueryable(value => value.TenantId == bid.TenantId && value.TenderBidId == bid.Id &&
                                   value.TenderItemId == tenderItemId.Value && !value.IsDeleted)
            .Include(value => value.TenderItem).ThenInclude(value => value.Lot)
            .Include(value => value.BidLot)
            .AsNoTracking().SingleOrDefaultAsync();
        if (item?.TenderItem == null || item.TenderItem.IsDeleted || item.TenderItem.TenantId != bid.TenantId ||
            item.TenderItem.TenderId != bid.TenderId || item.BidLot == null || item.BidLot.IsDeleted ||
            item.BidLot.TenantId != bid.TenantId || item.BidLot.TenderBidId != bid.Id ||
            item.TenderItem.LotId != item.BidLot.LotId || item.TenderItem.Lot == null ||
            item.TenderItem.Lot.IsDeleted || item.TenderItem.Lot.Status == "Cancelled")
            throw new TenderBidInitiationValidationException("BID_DOCUMENT_ITEM_INVALID", "Select an item from a selected lot in your saved bid draft.");
        return item;
    }

    private async Task<T> WithBidDocumentTransactionAsync<T>(Func<Task<T>> operation)
    {
        if (_unitOfWork.HasActiveTransaction) return await operation();
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var result = await operation();
                await _unitOfWork.CommitAsync();
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync();
                else _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });
    }

    private async Task EnsureNoAttachedItemDocumentsAsync(Guid bidId, IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.ToHashSet();
        if (ids.Count == 0) return;
        var documents = await _bidDocumentRepository.GetByBidIdAsync(bidId);
        if ((documents ?? []).Any(document => !document.IsDeleted && document.TenderBidItemId.HasValue &&
                                            ids.Contains(document.TenderBidItemId.Value)))
            throw new TenderBidInitiationValidationException("BID_ITEM_HAS_DOCUMENTS", "Remove the item's supporting documents before removing the item or its lot from the bid.");
    }
}
