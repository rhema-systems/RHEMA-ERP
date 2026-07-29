import type {
  SaveSupplierEvidencePack,
  SaveSupplierEvidenceRequirement,
  SupplierEvidencePackStatus,
  SupplierRegistrationCategory,
} from '@/types/procurement-supplier-evidence-pack';

export const supplierCategoryLabel: Record<
  SupplierRegistrationCategory,
  string
> = {
  Goods: 'Goods',
  Works: 'Works',
  Services: 'Services',
};

export const supplierEvidencePackStatusLabel: Record<
  SupplierEvidencePackStatus,
  string
> = {
  Draft: 'Draft',
  PendingApproval: 'Pending approval',
  Published: 'Published',
  Retired: 'Retired',
};

export const emptySupplierEvidenceRequirement =
  (): SaveSupplierEvidenceRequirement => ({
    requirementCode: '',
    name: '',
    kind: 'Document',
    documentType: '',
    isMandatory: true,
    allowedClassifications: [],
    validityMode: 'CurrentOnSubmission',
    approvalStepOrder: 1,
    approvalStepName: '',
    maxFileSizeBytes: 10 * 1024 * 1024,
    allowedMimeTypes: ['application/pdf'],
  });

export const validateSupplierEvidencePack = (
  value: SaveSupplierEvidencePack
): string | null => {
  if (!value.packCode.trim() || !value.name.trim())
    return 'Pack code and name are required.';
  if (!value.sourceConfigurationProfileId || !value.workflowDefinitionId)
    return 'Published configuration-profile and workflow lineage are required.';
  if (!value.effectiveFromUtc) return 'Effective-from date is required.';
  if (!value.requirements.length)
    return 'At least one evidence requirement is required.';
  const codes = new Set<string>();
  for (const requirement of value.requirements) {
    const code = requirement.requirementCode.trim().toUpperCase();
    if (!code || !requirement.name.trim())
      return 'Every requirement needs a code and name.';
    if (codes.has(code)) return `Requirement code ${code} is duplicated.`;
    codes.add(code);
    if (
      !requirement.approvalStepName.trim() ||
      requirement.approvalStepOrder < 1
    )
      return `Requirement ${code} needs an exact workflow step name and order.`;
    if (
      requirement.kind !== 'Classification' &&
      !requirement.documentType?.trim()
    )
      return `Requirement ${code} needs a document type.`;
    if (
      requirement.kind !== 'Document' &&
      (!requirement.classificationScheme?.trim() ||
        !requirement.allowedClassifications.length)
    )
      return `Requirement ${code} needs a classification scheme and allowed values.`;
    if (
      requirement.validityMode === 'MinimumRemainingDays' &&
      (!requirement.minimumRemainingDays ||
        requirement.minimumRemainingDays < 1)
    )
      return `Requirement ${code} needs positive remaining-validity days.`;
  }
  if (
    value.category === 'Works' &&
    !value.requirements.some(
      (item) => item.isMandatory && item.kind !== 'Document'
    )
  )
    return 'Works needs a mandatory classification requirement.';
  return null;
};
