export const SUPPLIER_ONBOARDING_PERMISSIONS = {
  manage: 'procurement.supplier.manage',
  review: 'procurement.supplier.review',
  verifyPayment: 'procurement.supplier.payment.verify',
} as const;

export const resolveSupplierOnboardingAccess = (
  hasPermission: (permission: string) => boolean
) => ({
  canManage: hasPermission(SUPPLIER_ONBOARDING_PERMISSIONS.manage),
  canReview: hasPermission(SUPPLIER_ONBOARDING_PERMISSIONS.review),
  canVerifyPayment: hasPermission(
    SUPPLIER_ONBOARDING_PERMISSIONS.verifyPayment
  ),
});
