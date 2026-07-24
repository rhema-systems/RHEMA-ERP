import type {
  ProcurementSpecificationTemplateListItem,
  ProcurementSpecificationTemplateStatus,
  SaveProcurementSpecificationTemplate,
} from '@/types/procurement-specification-template';

export const procurementSpecificationTemplateActions = (
  item: Pick<ProcurementSpecificationTemplateListItem, 'status'>
) => ({
  canEdit: item.status === 'Draft',
  canSubmit: item.status === 'Draft',
  canDelete: item.status === 'Draft',
  canPublish: item.status === 'PendingApproval',
  canReject: item.status === 'PendingApproval',
  canClone: item.status === 'Published' || item.status === 'Retired',
  canRetire: item.status === 'Published',
});

export const validateProcurementSpecificationTemplate = (
  value: SaveProcurementSpecificationTemplate
) => {
  if (!value.templateCode.trim()) return 'Template code is required.';
  if (!value.name.trim()) return 'Template name is required.';
  if (!value.effectiveFromUtc) return 'Effective-from date is required.';
  if (
    value.effectiveToUtc &&
    new Date(value.effectiveToUtc) < new Date(value.effectiveFromUtc)
  )
    return 'Effective-to date cannot precede effective-from date.';
  const sections: Array<[string, string]> = [
    ['Purpose', value.purpose],
    [
      'Functional and performance requirements',
      value.functionalAndPerformanceRequirements,
    ],
    [
      'Process and materials requirements',
      value.processAndMaterialsRequirements,
    ],
    [
      'Dimensions and marking requirements',
      value.dimensionsAndMarkingRequirements,
    ],
    [
      'Testing and inspection requirements',
      value.testingAndInspectionRequirements,
    ],
    ['Applicable standards', value.applicableStandards],
    ['Deliverables', value.deliverables],
    ['Acceptance criteria', value.acceptanceCriteria],
  ];
  const missing = sections.find(([, content]) => !content.trim());
  return missing ? `${missing[0]} is required before submission.` : undefined;
};

export const procurementSpecificationTemplateStatusTone = (
  status: ProcurementSpecificationTemplateStatus
) => {
  if (status === 'Published') return 'border-emerald-300 text-emerald-700';
  if (status === 'PendingApproval') return 'border-blue-300 text-blue-700';
  if (status === 'Retired') return 'border-slate-300 text-slate-600';
  return 'border-amber-300 text-amber-700';
};
