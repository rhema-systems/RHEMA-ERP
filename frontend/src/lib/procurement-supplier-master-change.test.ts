import { describe, expect, it } from 'vitest';
import {
  buildSupplierMasterPatch,
  hydrateSupplierComplianceDraft,
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
    expect(buildSupplierMasterPatch(value)).toEqual({
      CategoryIds: ['a', 'b'],
    });
  });

  it('emits only explicitly changed compliance fields', () => {
    const value = draft();
    value.resourceType = 'SupplierComplianceStatus';

    expect(
      buildSupplierMasterPatch(
        value,
        new Set<keyof SupplierMasterChangeDraft>(['complianceValidUntilUtc'])
      )
    ).toEqual({ ComplianceValidUntilUtc: '2027-07-27' });
  });

  it('does not reactivate or unblacklist a supplier when compliance notes change', () => {
    const value = draft();
    value.resourceType = 'SupplierComplianceStatus';
    value.isActive = true;
    value.isBlacklisted = false;
    value.complianceNotes = 'Annual evidence refreshed.';

    const patch = buildSupplierMasterPatch(
      value,
      new Set<keyof SupplierMasterChangeDraft>(['complianceNotes'])
    );

    expect(patch).toEqual({ ComplianceNotes: 'Annual evidence refreshed.' });
    expect(patch).not.toHaveProperty('IsActive');
    expect(patch).not.toHaveProperty('IsBlacklisted');
    expect(patch).not.toHaveProperty('RegistrationStatus');
  });

  it('emits null when an operator explicitly clears a nullable field', () => {
    const value = draft();
    value.resourceType = 'SupplierComplianceStatus';
    value.blacklistExpiryDate = '';

    expect(
      buildSupplierMasterPatch(
        value,
        new Set<keyof SupplierMasterChangeDraft>(['blacklistExpiryDate'])
      )
    ).toEqual({ BlacklistExpiryDate: null });
  });

  it('hydrates compliance controls from the selected supplier without creating changes', () => {
    const value = hydrateSupplierComplianceDraft(draft(), {
      status: 'Suspended',
      approvalStatus: 'Approved',
      isActive: false,
      isBlacklisted: true,
      blacklistReason: 'Current restriction',
      blacklistDate: '2026-06-01T00:00:00Z',
      riskLevel: 'High',
      complianceStatus: 'NonCompliant',
      complianceReviewDateUtc: '2026-06-02T00:00:00Z',
      complianceValidUntilUtc: '2026-08-02T00:00:00Z',
      complianceNotes: 'Existing evidence gap.',
    });

    expect(value).toMatchObject({
      registrationStatus: 'Suspended',
      isActive: false,
      isBlacklisted: true,
      blacklistDate: '2026-06-01',
      riskLevel: 'High',
      complianceStatus: 'NonCompliant',
      complianceReviewDateUtc: '2026-06-02',
      complianceValidUntilUtc: '2026-08-02',
    });
    expect(
      buildSupplierMasterPatch(
        value,
        new Set<keyof SupplierMasterChangeDraft>()
      )
    ).toEqual({});
  });
});
