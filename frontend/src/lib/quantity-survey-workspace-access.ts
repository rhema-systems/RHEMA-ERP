export const QUANTITY_SURVEY_WORKSPACE_PERMISSIONS = {
  read: 'quantity-survey.workspace.read',
  boq: 'quantity-survey.boq.manage',
  estimates: 'quantity-survey.estimates.manage',
  measurements: 'quantity-survey.measurements.manage',
  valuations: 'quantity-survey.valuations.manage',
  certificates: 'quantity-survey.certificates.manage',
  variations: 'quantity-survey.variations.manage',
} as const;

type PermissionChecker = (permission: string) => boolean;

export function getQuantitySurveyWorkspaceAccess(
  hasPermission: PermissionChecker
) {
  return {
    canRead: hasPermission(QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.read),
    canManageBoq: hasPermission(QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.boq),
    canManageEstimates: hasPermission(
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.estimates
    ),
    canManageMeasurements: hasPermission(
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.measurements
    ),
    canManageValuations: hasPermission(
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.valuations
    ),
    canManageCertificates: hasPermission(
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.certificates
    ),
    canManageVariations: hasPermission(
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.variations
    ),
  };
}
