import { describe, expect, it } from 'vitest';
import {
  buildSupplierMasterPatch,
  type SupplierMasterChangeDraft,
  supplierMasterResourceTypes,
} from './procurement-supplier-master-change';

const draft = (): SupplierMasterChangeDraft => ({
  resourceType: 'SupplierOwnershipDetails',
  partnerName: '',
  legalName: '',
  primaryEmail: '',
  primaryPhone: '',
  physicalAddress: '',
  bankName: '',
  bankAccountNumber: '',
  bankAccountName: '',
  bankBranch: '',
  bankSwiftCode: '',
  bankIBAN: '',
  taxIdentificationNumber: '',
  vatNumber: '',
  isTaxExempt: false,
  taxExemptionNumber: '',
  taxExemptionExpiry: '',
  owners: [
    {
      name: 'Ada Holdings',
      ownershipPercent: '100',
      nationality: 'GH',
      registrationNumber: 'REG-1',
      politicallyExposed: false,
    },
  ],
  ownershipVerifiedAtUtc: '2026-07-27',
  categoryIds: [],
  registrationStatus: 'Approved',
  approvalStatus: 'Approved',
  isActive: true,
  isBlacklisted: false,
  blacklistReason: '',
  blacklistDate: '',
  blacklistExpiryDate: '',
  riskLevel: 'Low',
  complianceStatus: 'Compliant',
  complianceReviewDateUtc: '2026-07-27',
  complianceValidUntilUtc: '2027-07-27',
  complianceNotes: '',
});

describe('supplier master change builder', () => {
  it('exposes exactly the six supplier-controlled families', () =>
    expect(supplierMasterResourceTypes).toHaveLength(6));

  it('builds normalized beneficial ownership without raw patch editing', () => {
    const patch = buildSupplierMasterPatch(draft());
    expect(JSON.parse(patch.BeneficialOwnershipJson as string)).toEqual([
      {
        name: 'Ada Holdings',
        ownershipPercent: 100,
        nationality: 'GH',
        registrationNumber: 'REG-1',
        politicallyExposed: false,
      },
    ]);
  });

  it('rejects incomplete ownership totals', () => {
    const value = draft();
    value.owners[0].ownershipPercent = '60';
    expect(() => buildSupplierMasterPatch(value)).toThrow('total 100');
  });

  it('deduplicates category assignments', () => {
    const value = draft();
    value.resourceType = 'SupplierCategoryAssignments';
    value.categoryIds = ['b', 'a', 'b'];
    expect(buildSupplierMasterPatch(value)).toEqual({ CategoryIds: ['a', 'b'] });
  });
});
