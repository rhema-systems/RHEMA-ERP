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

const optional = (value: string) => (value.trim() ? value.trim() : undefined);

export const buildSupplierMasterPatch = (
  draft: SupplierMasterChangeDraft
): Record<string, unknown> => {
  switch (draft.resourceType) {
    case 'SupplierProfile':
      return compact({
        PartnerName: optional(draft.partnerName),
        LegalName: optional(draft.legalName),
        PrimaryEmail: optional(draft.primaryEmail),
        PrimaryPhone: optional(draft.primaryPhone),
        PhysicalAddress: optional(draft.physicalAddress),
      });
    case 'SupplierBankDetails':
      return compact({
        BankName: optional(draft.bankName),
        BankAccountNumber: optional(draft.bankAccountNumber),
        BankAccountName: optional(draft.bankAccountName),
        BankBranch: optional(draft.bankBranch),
        BankSwiftCode: optional(draft.bankSwiftCode),
        BankIBAN: optional(draft.bankIBAN),
      });
    case 'SupplierTaxDetails':
      return compact({
        TaxIdentificationNumber: optional(draft.taxIdentificationNumber),
        VATNumber: optional(draft.vatNumber),
        IsTaxExempt: draft.isTaxExempt,
        TaxExemptionNumber: optional(draft.taxExemptionNumber),
        TaxExemptionExpiry: optional(draft.taxExemptionExpiry),
      });
    case 'SupplierOwnershipDetails': {
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
        throw new Error('Every ownership percentage must be between 0 and 100.');
      if (Math.abs(total - 100) > 0.01)
        throw new Error('Beneficial ownership percentages must total 100.');
      return compact({
        BeneficialOwnershipJson: JSON.stringify(owners),
        OwnershipVerifiedAtUtc: optional(draft.ownershipVerifiedAtUtc),
      });
    }
    case 'SupplierCategoryAssignments':
      if (!draft.categoryIds.length)
        throw new Error('Select at least one supplier category.');
      return { CategoryIds: [...new Set(draft.categoryIds)].sort() };
    case 'SupplierComplianceStatus':
      return compact({
        RegistrationStatus: optional(draft.registrationStatus),
        ApprovalStatus: optional(draft.approvalStatus),
        IsActive: draft.isActive,
        IsBlacklisted: draft.isBlacklisted,
        BlacklistReason: optional(draft.blacklistReason),
        BlacklistDate: optional(draft.blacklistDate),
        BlacklistExpiryDate: optional(draft.blacklistExpiryDate),
        RiskLevel: optional(draft.riskLevel),
        ComplianceStatus: optional(draft.complianceStatus),
        ComplianceReviewDateUtc: optional(draft.complianceReviewDateUtc),
        ComplianceValidUntilUtc: optional(draft.complianceValidUntilUtc),
        ComplianceNotes: optional(draft.complianceNotes),
      });
  }
};

const compact = (value: Record<string, unknown>) =>
  Object.fromEntries(
    Object.entries(value).filter(([, item]) => item !== undefined)
  );
