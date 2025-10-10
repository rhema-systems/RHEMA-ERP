using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance
{
    public class Account : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public AccountType AccountType { get; set; }

        public decimal Balance { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid? ParentAccountId { get; set; }
        public Account? ParentAccount { get; set; }

        public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();
        public ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();

        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        public int Level { get; set; } = 1;
        public string FullPath { get; set; } = string.Empty;
    }

    public enum AccountType
    {
        Asset = 1,
        Liability = 2,
        Equity = 3,
        Income = 4,
        Expense = 5
    }
}