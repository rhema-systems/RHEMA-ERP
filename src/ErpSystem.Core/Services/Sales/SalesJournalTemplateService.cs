using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Core.Services.Sales
{
    public class SalesJournalTemplateService : ISalesJournalTemplateService
    {
        private readonly IGenericRepository<SalesJournalTemplate> _templateRepo;
        private readonly IGenericRepository<SalesJournalTemplateLine> _lineRepo;

        public SalesJournalTemplateService(
            IGenericRepository<SalesJournalTemplate> templateRepo,
            IGenericRepository<SalesJournalTemplateLine> lineRepo)
        {
            _templateRepo = templateRepo;
            _lineRepo = lineRepo;
        }

        public async Task<(IEnumerable<SalesJournalTemplateSummaryDto> Items, int TotalCount)> GetTemplatesAsync(int page, int pageSize, string? search = null, bool? isActive = null)
        {
            var query = _templateRepo.GetQueryable().Include(t => t.Lines).Where(t => !t.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.Name.Contains(search) || t.TransactionType.Contains(search));
            if (isActive.HasValue)
                query = query.Where(t => t.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(t => new SalesJournalTemplateSummaryDto
                {
                    Id = t.Id, Name = t.Name, Description = t.Description,
                    TransactionType = t.TransactionType, IsActive = t.IsActive,
                    LineCount = t.Lines.Count, CreatedAt = t.CreatedAt
                }).ToListAsync();

            return (items, totalCount);
        }

        public async Task<SalesJournalTemplateDetailDto?> GetTemplateByIdAsync(Guid id)
        {
            var t = await _templateRepo.GetQueryable().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (t == null) return null;
            return MapDetail(t);
        }

        public async Task<SalesJournalTemplateDetailDto> CreateTemplateAsync(CreateSalesJournalTemplateDto dto)
        {
            var template = new SalesJournalTemplate
            {
                Name = dto.Name, Description = dto.Description,
                TransactionType = dto.TransactionType,
                PostingBehavior = dto.PostingBehavior, AutoPost = dto.AutoPost
            };

            foreach (var line in dto.Lines)
            {
                template.Lines.Add(new SalesJournalTemplateLine
                {
                    SalesJournalTemplateId = template.Id,
                    Sequence = line.Sequence, AccountType = line.AccountType,
                    AccountCode = line.AccountCode, AccountName = line.AccountName,
                    EntryType = line.EntryType, AmountSource = line.AmountSource,
                    FixedAmount = line.FixedAmount, Percentage = line.Percentage,
                    Description = line.Description
                });
            }

            await _templateRepo.AddAsync(template);
            await _templateRepo.SaveChangesAsync();
            return (await GetTemplateByIdAsync(template.Id))!;
        }

        public async Task<SalesJournalTemplateDetailDto> UpdateTemplateAsync(Guid id, CreateSalesJournalTemplateDto dto)
        {
            var template = await _templateRepo.GetQueryable().Include(x => x.Lines).FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted)
                ?? throw new InvalidOperationException("Template not found");

            template.Name = dto.Name; template.Description = dto.Description;
            template.TransactionType = dto.TransactionType;
            template.PostingBehavior = dto.PostingBehavior; template.AutoPost = dto.AutoPost;
            template.UpdatedAt = DateTime.UtcNow;

            foreach (var oldLine in template.Lines.ToList())
                await _lineRepo.DeleteAsync(oldLine);

            foreach (var line in dto.Lines)
            {
                template.Lines.Add(new SalesJournalTemplateLine
                {
                    SalesJournalTemplateId = template.Id,
                    Sequence = line.Sequence, AccountType = line.AccountType,
                    AccountCode = line.AccountCode, AccountName = line.AccountName,
                    EntryType = line.EntryType, AmountSource = line.AmountSource,
                    FixedAmount = line.FixedAmount, Percentage = line.Percentage,
                    Description = line.Description
                });
            }

            await _templateRepo.UpdateAsync(template);
            await _templateRepo.SaveChangesAsync();
            return (await GetTemplateByIdAsync(id))!;
        }

        public async Task<bool> ActivateTemplateAsync(Guid id)
        {
            var t = await _templateRepo.GetQueryable().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (t == null) return false;
            t.IsActive = true; t.UpdatedAt = DateTime.UtcNow;
            await _templateRepo.UpdateAsync(t); await _templateRepo.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateTemplateAsync(Guid id)
        {
            var t = await _templateRepo.GetQueryable().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (t == null) return false;
            t.IsActive = false; t.UpdatedAt = DateTime.UtcNow;
            await _templateRepo.UpdateAsync(t); await _templateRepo.SaveChangesAsync();
            return true;
        }

        private SalesJournalTemplateDetailDto MapDetail(SalesJournalTemplate t)
        {
            return new SalesJournalTemplateDetailDto
            {
                Id = t.Id, Name = t.Name, Description = t.Description,
                TransactionType = t.TransactionType, PostingBehavior = t.PostingBehavior,
                AutoPost = t.AutoPost, IsActive = t.IsActive,
                LineCount = t.Lines.Count, CreatedAt = t.CreatedAt,
                Lines = t.Lines.OrderBy(l => l.Sequence).Select(l => new SalesJournalTemplateLineDto
                {
                    Id = l.Id, Sequence = l.Sequence, AccountType = l.AccountType,
                    AccountCode = l.AccountCode, AccountName = l.AccountName,
                    EntryType = l.EntryType, AmountSource = l.AmountSource,
                    FixedAmount = l.FixedAmount, Percentage = l.Percentage,
                    Description = l.Description
                }).ToList()
            };
        }
    }
}
