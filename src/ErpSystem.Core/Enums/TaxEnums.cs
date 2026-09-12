using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Enums
{
    /// <summary>
    /// Tax applicability - which transactions the tax applies to
    /// </summary>
    public enum TaxApplicability
    {
        /// <summary>
        /// Applies to sales/AR transactions only
        /// </summary>
        Sales = 1,

        /// <summary>
        /// Applies to purchases/AP transactions only
        /// </summary>
        Purchases = 2,

        /// <summary>
        /// Applies to both sales and purchases
        /// </summary>
        Both = 3
    }

    /// <summary>
    /// Tax category classification
    /// </summary>
    public enum TaxCategory
    {
        /// <summary>
        /// Standard taxes like VAT, GST
        /// </summary>
        Standard = 1,

        /// <summary>
        /// Levies like NHIL and GETFund.
        /// </summary>
        Levy = 2,

        /// <summary>
        /// Withholding taxes (WHT)
        /// </summary>
        Withholding = 3,

        /// <summary>
        /// Exempt supplies that do not create output or input tax postings.
        /// </summary>
        Exempt = 4,

        /// <summary>
        /// Zero-rated supplies reported as taxable at a zero percent rate.
        /// </summary>
        ZeroRated = 5,

        /// <summary>
        /// Non-taxable or out-of-scope supplies.
        /// </summary>
        OutOfScope = 6,

        /// <summary>
        /// VAT withholding or withholding VAT clearing treatment.
        /// </summary>
        VatWithholding = 7,

        /// <summary>
        /// Reverse-charge or import VAT placeholder categories.
        /// </summary>
        ReverseCharge = 8
    }

    /// <summary>
    /// Canonical line-level tax treatment for AP and AR invoice lines.
    /// </summary>
    public enum TaxTreatment
    {
        /// <summary>Draft only: Finance has not reviewed the tax treatment yet.</summary>
        PendingReview = 5,
        /// <summary>
        /// Standard-rated taxable supply.
        /// </summary>
        Standard = 1,

        /// <summary>
        /// Exempt supply. No input/output tax posting is created.
        /// </summary>
        Exempt = 2,

        /// <summary>
        /// Zero-rated taxable supply. Reportable as taxable at zero percent.
        /// </summary>
        ZeroRated = 3,

        /// <summary>
        /// Non-taxable or out-of-scope supply.
        /// </summary>
        OutOfScope = 4,

        /// <summary>
        /// Alias for out-of-scope/non-taxable supply.
        /// </summary>
        NonTaxable = OutOfScope
    }

    /// <summary>
    /// Compound basis - how tax is calculated in relation to other taxes
    /// </summary>
    public enum CompoundBasis
    {
        /// <summary>
        /// Tax calculated on base amount only
        /// Example: NHIL = Base * 2.5%
        /// </summary>
        BaseOnly = 1,

        /// <summary>
        /// Tax on base + all previous taxes in the group.
        /// </summary>
        Cumulative = 2,

        /// <summary>
        /// Tax on base + specific previous taxes (defined in AppliesOnTaxCodes)
        /// </summary>
        Specific = 3
    }

    /// <summary>
    /// Transaction type for tax determination
    /// </summary>
    public enum TaxTransactionType
    {
        /// <summary>
        /// Sale of goods
        /// </summary>
        SaleOfGoods = 1,

        /// <summary>
        /// Sale of services
        /// </summary>
        SaleOfServices = 2,

        /// <summary>
        /// Purchase of goods
        /// </summary>
        PurchaseOfGoods = 3,

        /// <summary>
        /// Purchase of services
        /// </summary>
        PurchaseOfServices = 4,

        /// <summary>
        /// Construction/works
        /// </summary>
        Works = 5,

        /// <summary>
        /// Rental income
        /// </summary>
        Rent = 6,

        /// <summary>
        /// Dividend payment
        /// </summary>
        Dividend = 7,

        /// <summary>
        /// Interest payment
        /// </summary>
        Interest = 8
    }
}
