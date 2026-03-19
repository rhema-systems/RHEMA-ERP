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
        /// Levies like NHIL, GETFL, COVID
        /// </summary>
        Levy = 2,

        /// <summary>
        /// Withholding taxes (WHT)
        /// </summary>
        Withholding = 3
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
        /// Tax on base + all previous taxes in the group
        /// Example: VAT = (Base + NHIL + GETFL + COVID) * 15%
        /// </summary>
        Cumulative = 2,

        /// <summary>
        /// Tax on base + specific previous taxes (defined in AppliesOnTaxCodes)
        /// Example: VAT = (Base + NHIL + GETFL) * 15% (excludes COVID)
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
