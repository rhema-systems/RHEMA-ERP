import type { ProcurementMasterDataResourceType } from '@/types/procurement-master-data-change';

export const supplierMasterResourceTypes = [
  'SupplierProfile',
  'SupplierBankDetails',
  'SupplierTaxDetails',
  'SupplierOwnershipDetails',
  'SupplierCategoryAssignments',
  'SupplierComplianceStatus',
] as const satisfies readonly ProcurementMasterDataResourceType[];

export type SupplierMasterResourceType =
  (typeof supplierMasterResourceTypes)[number];

export interface BeneficialOwnerDraft {
  name: string;
  ownershipPercent: string;
  nationality: string;
  registrationNumber: string;
  politicallyExposed: boolean;
}

export interface SupplierMasterChangeDraft {
  resourceType: SupplierMasterResourceType;
  partnerName: string;
  legalName: string;
  primaryEmail: string;
  primaryPhone: string;
  physicalAddress: string;
  bankName: string;
  bankAccountNumber: string;
  bankAccountName: string;
  bankBranch: string;
  bankSwiftCode: string;
  bankIBAN: string;
  taxIdentificationNumber: string;
  vatNumber: string;
  isTaxExempt: boolean;
  taxExemptionNumber: string;
  taxExemptionExpiry: string;
  owners: BeneficialOwnerDraft[];
  ownershipVerifiedAtUtc: string;
  categoryIds: string[];
  registrationStatus: string;
  approvalStatus: string;
  isActive: boolean;
  isBlacklisted: boolean;
  blacklistReason: string;
  blacklistDate: string;
  blacklistExpiryDate: string;
  riskLevel: string;
  complianceStatus: string;
  complianceReviewDateUtc: string;
  complianceValidUntilUtc: string;
  complianceNotes: string;
}

export interface SupplierComplianceSource {
  status?: string;
  approvalStatus?: string;
  riskLevel?: string;
  isActive: boolean;
  isBlacklisted: boolean;
  blacklistReason?: string;
  blacklistDate?: string;
  blacklistExpiryDate?: string;
  complianceStatus?: string;
  complianceReviewDateUtc?: string;
  complianceValidUntilUtc?: string;
  complianceNotes?: string;
}

const dateInputValue = (value?: string) => value?.slice(0, 10) ?? '';

export const hydrateSupplierComplianceDraft = (
  draft: SupplierMasterChangeDraft,
  supplier: SupplierComplianceSource
): SupplierMasterChangeDraft => ({
  ...draft,
  registrationStatus: supplier.status ?? '',
  approvalStatus: supplier.approvalStatus ?? '',
  isActive: supplier.isActive,
  isBlacklisted: supplier.isBlacklisted,
  blacklistReason: supplier.blacklistReason ?? '',
  blacklistDate: dateInputValue(supplier.blacklistDate),
  blacklistExpiryDate: dateInputValue(supplier.blacklistExpiryDate),
  riskLevel: supplier.riskLevel ?? '',
  complianceStatus: supplier.complianceStatus ?? '',
  complianceReviewDateUtc: dateInputValue(supplier.complianceReviewDateUtc),
  complianceValidUntilUtc: dateInputValue(supplier.complianceValidUntilUtc),
  complianceNotes: supplier.complianceNotes ?? '',
});

const optional = (value: string) => (value.trim() ? value.trim() : undefined);

const includesField = (
  changedFields: ReadonlySet<keyof SupplierMasterChangeDraft> | undefined,
  field: keyof SupplierMasterChangeDraft
) => !changedFields || changedFields.has(field);

const optionalField = (
  draft: SupplierMasterChangeDraft,
  field: keyof SupplierMasterChangeDraft,
  changedFields?: ReadonlySet<keyof SupplierMasterChangeDraft>
) => {
  if (!includesField(changedFields, field)) return undefined;
  const value = optional(String(draft[field] ?? ''));
  return changedFields && value === undefined ? null : value;
};

export const buildSupplierMasterPatch = (
  draft: SupplierMasterChangeDraft,
  changedFields?: ReadonlySet<keyof SupplierMasterChangeDraft>
): Record<string, unknown> => {
  switch (draft.resourceType) {
    case 'SupplierProfile':
      return compact({
        PartnerName: optionalField(draft, 'partnerName', changedFields),
        LegalName: optionalField(draft, 'legalName', changedFields),
        PrimaryEmail: optionalField(draft, 'primaryEmail', changedFields),
        PrimaryPhone: optionalField(draft, 'primaryPhone', changedFields),
        PhysicalAddress: optionalField(draft, 'physicalAddress', changedFields),
      });
    case 'SupplierBankDetails':
      return compact({
        BankName: optionalField(draft, 'bankName', changedFields),
        BankAccountNumber: optionalField(
          draft,
          'bankAccountNumber',
          changedFields
        ),
        BankAccountName: optionalField(draft, 'bankAccountName', changedFields),
        BankBranch: optionalField(draft, 'bankBranch', changedFields),
        BankSwiftCode: optionalField(draft, 'bankSwiftCode', changedFields),
        BankIBAN: optionalField(draft, 'bankIBAN', changedFields),
      });
    case 'SupplierTaxDetails':
      return compact({
        TaxIdentificationNumber: optionalField(
          draft,
          'taxIdentificationNumber',
          changedFields
        ),
        VATNumber: optionalField(draft, 'vatNumber', changedFields),
        IsTaxExempt: includesField(changedFields, 'isTaxExempt')
          ? draft.isTaxExempt
          : undefined,
        TaxExemptionNumber: optionalField(
          draft,
          'taxExemptionNumber',
          changedFields
        ),
        TaxExemptionExpiry: optionalField(
          draft,
          'taxExemptionExpiry',
          changedFields
        ),
      });
    case 'SupplierOwnershipDetails': {
      let beneficialOwnershipJson: string | undefined;
      if (includesField(changedFields, 'owners')) {
        const owners = draft.owners
          .filter((owner) => owner.name.trim())
          .map((owner) => ({
            name: owner.name.trim(),
            ownershipPercent: Number(owner.ownershipPercent),
            nationality: optional(owner.nationality),
            registrationNumber: optional(owner.registrationNumber),
            politicallyExposed: owner.politicallyExposed,
          }));
        const total = owners.reduce(
          (sum, owner) => sum + owner.ownershipPercent,
          0
        );
        if (!owners.length)
          throw new Error('Add at least one beneficial owner.');
        if (
          owners.some(
            (owner) =>
              !Number.isFinite(owner.ownershipPercent) ||
              owner.ownershipPercent <= 0 ||
              owner.ownershipPercent > 100
          )
        )
          throw new Error(
            'Every ownership percentage must be between 0 and 100.'
          );
        if (Math.abs(total - 100) > 0.01)
          throw new Error('Beneficial ownership percentages must total 100.');
        beneficialOwnershipJson = JSON.stringify(owners);
      }
      return compact({
        BeneficialOwnershipJson: beneficialOwnershipJson,
        OwnershipVerifiedAtUtc: optionalField(
          draft,
          'ownershipVerifiedAtUtc',
          changedFields
        ),
      });
    }
    case 'SupplierCategoryAssignments':
      if (!includesField(changedFields, 'categoryIds')) return {};
      if (!draft.categoryIds.length)
        throw new Error('Select at least one supplier category.');
      return { CategoryIds: [...new Set(draft.categoryIds)].sort() };
    case 'SupplierComplianceStatus':
      return compact({
        RegistrationStatus: optionalField(
          draft,
          'registrationStatus',
          changedFields
        ),
        ApprovalStatus: optionalField(draft, 'approvalStatus', changedFields),
        IsActive: includesField(changedFields, 'isActive')
          ? draft.isActive
          : undefined,
        IsBlacklisted: includesField(changedFields, 'isBlacklisted')
          ? draft.isBlacklisted
          : undefined,
        BlacklistReason: optionalField(draft, 'blacklistReason', changedFields),
        BlacklistDate: optionalField(draft, 'blacklistDate', changedFields),
        BlacklistExpiryDate: optionalField(
          draft,
          'blacklistExpiryDate',
          changedFields
        ),
        RiskLevel: optionalField(draft, 'riskLevel', changedFields),
        ComplianceStatus: optionalField(
          draft,
          'complianceStatus',
          changedFields
        ),
        ComplianceReviewDateUtc: optionalField(
          draft,
          'complianceReviewDateUtc',
          changedFields
        ),
        ComplianceValidUntilUtc: optionalField(
          draft,
          'complianceValidUntilUtc',
          changedFields
        ),
        ComplianceNotes: optionalField(draft, 'complianceNotes', changedFields),
      });
  }
};

const compact = (value: Record<string, unknown>) =>
  Object.fromEntries(
    Object.entries(value).filter(([, item]) => item !== undefined)
  );
