namespace ErpSystem.Core.Enums
{
    public enum LineItemType
    {
        Product = 1,
        GLAccount = 2,
        Inventory = 3,

        // Asset-sale invoice lines are kept distinct from ordinary product/service revenue. The
        // AR posting path still owns tax and control-account entries, but credits the disposal
        // proceeds clearing account rather than a sales-revenue account.
        FixedAssetDisposal = 4,

        // A buyer/auctioneer deduction is a non-taxable contra line against the same clearing
        // account. Restricting negative AR lines to this explicit type prevents arbitrary credit
        // construction through normal invoice entry screens.
        FixedAssetDisposalAdjustment = 5
    }
}
