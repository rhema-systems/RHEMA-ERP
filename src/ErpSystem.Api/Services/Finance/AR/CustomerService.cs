using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services.Finance.AR
{
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ILogger<CustomerService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _logger = logger;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
        private string UserName => _currentUser.UserName ??  "system";

        public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .GetQueryable(c => c.TenantId == TenantId && c.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            return customer == null ? null : MapToDto(customer);
        }

        public async Task<CustomerDto?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .GetQueryable(c => c.TenantId == TenantId && c.CustomerCode == customerCode)
                .FirstOrDefaultAsync(cancellationToken);

            return customer == null ? null : MapToDto(customer);
        }

        public async Task<PagedResult<CustomerDto>> GetAllAsync(CustomerQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<Customer>()
                .GetQueryable(c => c.TenantId == TenantId);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                queryable = queryable.Where(c =>
                    c.CustomerName.Contains(query.SearchTerm) ||
                    c.CustomerCode.Contains(query.SearchTerm) ||
                    (c.Email != null && c.Email.Contains(query.SearchTerm)));
            }

            if (!string.IsNullOrWhiteSpace(query.CustomerType))
                queryable = queryable.Where(c => c.CustomerType == query.CustomerType);

            if (query.IsActive.HasValue)
                queryable = queryable.Where(c => c.IsActive == query.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(query.City))
                queryable = queryable.Where(c => c.City == query.City);

            if (!string.IsNullOrWhiteSpace(query.Country))
                queryable = queryable.Where(c => c.Country == query.Country);

            // Get total count
            var totalCount = await queryable.CountAsync(cancellationToken);

            // Apply sorting
            queryable = query.SortBy?.ToLower() switch
            {
                "name" => query.SortDescending
                    ? queryable.OrderByDescending(c => c.CustomerName)
                    : queryable.OrderBy(c => c.CustomerName),
                "code" => query.SortDescending
                    ? queryable.OrderByDescending(c => c.CustomerCode)
                    : queryable.OrderBy(c => c.CustomerCode),
                "balance" => query.SortDescending
                    ? queryable.OrderByDescending(c => c.OutstandingBalance)
                    : queryable.OrderBy(c => c.OutstandingBalance),
                _ => queryable.OrderBy(c => c.CustomerName)
            };

            // Apply pagination
            var customers = await queryable
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<CustomerDto>
            {
                Items = customers.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        public async Task<CustomerDto> CreateAsync(CustomerCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Generate customer code if not provided
            if (string.IsNullOrWhiteSpace(dto.CustomerCode))
            {
                dto.CustomerCode = await GenerateCustomerCodeAsync(cancellationToken);
            }

            // Check for duplicate code
            var existingCustomer = await _unitOfWork.Repository<Customer>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.CustomerCode == dto.CustomerCode);

            if (existingCustomer != null)
                throw new InvalidOperationException($"Customer with code '{dto.CustomerCode}' already exists.");

            var now = DateTime.UtcNow;
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CustomerCode = dto.CustomerCode,
                CustomerName = dto.CustomerName,
                CustomerType = dto.CustomerType,
                ContactPerson = dto.ContactPerson,
                Email = dto.Email,
                Phone = dto.Phone,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                PostalCode = dto.PostalCode,
                Country = dto.Country,
                TaxId = dto.TaxId,
                CreditLimit = dto.CreditLimit,
                OutstandingBalance = 0,
                PaymentTermsDays = dto.PaymentTermsDays,
                PriceGroup = dto.PriceGroup,
                IsActive = true,
                Notes = dto.Notes,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<Customer>().AddAsync(customer);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created customer {CustomerCode} - {CustomerName}", customer.CustomerCode, customer.CustomerName);

            return MapToDto(customer);
        }

        public async Task<CustomerDto> UpdateAsync(CustomerUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == dto.Id);

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{dto.Id}' not found.");

            var now = DateTime.UtcNow;
            customer.CustomerName = dto.CustomerName;
            customer.CustomerType = dto.CustomerType;
            customer.ContactPerson = dto.ContactPerson;
            customer.Email = dto.Email;
            customer.Phone = dto.Phone;
            customer.Address = dto.Address;
            customer.City = dto.City;
            customer.State = dto.State;
            customer.PostalCode = dto.PostalCode;
            customer.Country = dto.Country;
            customer.TaxId = dto.TaxId;
            customer.CreditLimit = dto.CreditLimit;
            customer.PaymentTermsDays = dto.PaymentTermsDays;
            customer.PriceGroup = dto.PriceGroup;
            customer.IsActive = dto.IsActive;
            customer.Notes = dto.Notes;
            customer.UpdatedAt = now;
            customer.UpdatedBy = UserName;

            await _unitOfWork.Repository<Customer>().UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated customer {CustomerId}", customer.Id);

            return MapToDto(customer);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id);

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{id}' not found.");

            // Validate no outstanding balance
            if (customer.OutstandingBalance != 0)
                throw new InvalidOperationException("Cannot delete customer with outstanding balance.");

            // Soft delete by marking as inactive
            customer.IsActive = false;
            customer.UpdatedAt = DateTime.UtcNow;
            customer.UpdatedBy = UserName;

            await _unitOfWork.Repository<Customer>().UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted (inactivated) customer {CustomerId}", id);
        }

        public async Task<CustomerBalanceDto> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == customerId);

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{customerId}' not found.");

            // Get all outstanding invoices
            var invoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.CustomerId == customerId && (i.TotalAmount - i.PaidAmount) > 0)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var balance = new CustomerBalanceDto
            {
                CustomerId = customerId,
                CustomerName = customer.CustomerName,
                TotalOutstanding = customer.OutstandingBalance,
                CreditLimit = customer.CreditLimit,
                AvailableCredit = customer.CreditLimit - customer.OutstandingBalance
            };

            // Calculate aging buckets
            foreach (var invoice in invoices)
            {
                var daysOverdue = invoice.DueDate.HasValue
                    ? (now - invoice.DueDate.Value).Days
                    : (now - invoice.InvoiceDate).Days;

                if (daysOverdue < 0 || !invoice.DueDate.HasValue)
                    balance.Current += invoice.BalanceAmount;
                else if (daysOverdue <= 30)
                    balance.Days1To30 += invoice.BalanceAmount;
                else if (daysOverdue <= 60)
                    balance.Days31To60 += invoice.BalanceAmount;
                else if (daysOverdue <= 90)
                    balance.Days61To90 += invoice.BalanceAmount;
                else
                    balance.Days90Plus += invoice.BalanceAmount;
            }

            return balance;
        }

        public async Task<CreditCheckResultDto> CheckCreditLimitAsync(Guid customerId, decimal additionalAmount, CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == customerId);

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{customerId}' not found.");

            var availableCredit = customer.CreditLimit - customer.OutstandingBalance;
            var isApproved = availableCredit >= additionalAmount;

            return new CreditCheckResultDto
            {
                IsApproved = isApproved,
                RequestedAmount = additionalAmount,
                CurrentOutstanding = customer.OutstandingBalance,
                CreditLimit = customer.CreditLimit,
                AvailableCredit = availableCredit,
                Message = isApproved
                    ? "Credit check passed"
                    : $"Insufficient credit. Available: {availableCredit:C}, Requested: {additionalAmount:C}"
            };
        }

        public async Task<List<ErpSystem.Core.DTOs.Finance.InvoiceDto>> GetCustomerInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var invoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.CustomerId == customerId)
                .Include(i => i.LineItems)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync(cancellationToken);

            // Map to simple InvoiceDto (would need full mapper implementation)
            return invoices.Select(i => new ErpSystem.Core.DTOs.Finance.InvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                CustomerId = i.CustomerId,
                CustomerName = i.CustomerName,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                BalanceAmount = i.BalanceAmount,
                Status = i.Status.ToString(),
                CurrencyCode = i.CurrencyCode,
                CreatedAt = i.CreatedAt
            }).ToList();
        }

        public async Task<List<ErpSystem.Core.DTOs.Finance.CustomerPaymentDto>> GetCustomerPaymentsAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var payments = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.CustomerId == customerId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync(cancellationToken);

            return payments.Select(p => new ErpSystem.Core.DTOs.Finance.CustomerPaymentDto
            {
                Id = p.Id,
                PaymentNumber = p.PaymentNumber,
                CustomerId = p.CustomerId,
                PaymentDate = p.PaymentDate,
                TotalAmount = p.TotalAmount,
                AllocatedAmount = p.AllocatedAmount,
                UnallocatedAmount = p.UnallocatedAmount,
                PaymentMethod = p.PaymentMethod,
                Status = p.Status,
                CurrencyCode = p.CurrencyCode,
                CreatedAt = p.CreatedAt
            }).ToList();
        }

        private async Task<string> GenerateCustomerCodeAsync(CancellationToken cancellationToken)
        {
            // Get the last customer code
            var lastCustomer = await _unitOfWork.Repository<Customer>()
                .GetQueryable(c => c.TenantId == TenantId && c.CustomerCode.StartsWith("CUST"))
                .OrderByDescending(c => c.CustomerCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastCustomer == null)
                return "CUST0001";

            // Extract number and increment
            var lastCode = lastCustomer.CustomerCode;
            if (lastCode.Length > 4 && int.TryParse(lastCode.Substring(4), out var lastNumber))
            {
                return $"CUST{(lastNumber + 1):0000}";
            }

            return "CUST0001";
        }

        private CustomerDto MapToDto(Customer customer)
        {
            return new CustomerDto
            {
                Id = customer.Id,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                CustomerType = customer.CustomerType,
                ContactPerson = customer.ContactPerson,
                Email = customer.Email,
                Phone = customer.Phone,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                PostalCode = customer.PostalCode,
                Country = customer.Country,
                TaxId = customer.TaxId,
                CreditLimit = customer.CreditLimit,
                OutstandingBalance = customer.OutstandingBalance,
                PaymentTermsDays = customer.PaymentTermsDays,
                PriceGroup = customer.PriceGroup,
                IsActive = customer.IsActive,
                LastOrderDate = customer.LastOrderDate,
                LastPaymentDate = customer.LastPaymentDate,
                Notes = customer.Notes,
                CreatedAt = customer.CreatedAt
            };
        }
    }
}
