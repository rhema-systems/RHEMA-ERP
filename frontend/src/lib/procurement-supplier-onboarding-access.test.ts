import { describe, expect, it } from 'vitest';

import {
  resolveSupplierOnboardingAccess,
  SUPPLIER_ONBOARDING_PERMISSIONS,
} from './procurement-supplier-onboarding-access';

describe('supplier onboarding access', () => {
  it('allows an independent payment verifier without granting supplier review', () => {
    const granted = new Set<string>([
      SUPPLIER_ONBOARDING_PERMISSIONS.verifyPayment,
    ]);

    const access = resolveSupplierOnboardingAccess((permission) =>
      granted.has(permission)
    );

    expect(access.canVerifyPayment).toBe(true);
    expect(access.canReview).toBe(false);
    expect(access.canManage).toBe(false);
  });
});
