import { describe, expect, it, vi } from 'vitest';

import {
  getQuantitySurveyWorkspaceAccess,
  QUANTITY_SURVEY_WORKSPACE_PERMISSIONS,
} from './quantity-survey-workspace-access';

describe('getQuantitySurveyWorkspaceAccess', () => {
  it('maps every workspace mutation to its dedicated permission', () => {
    const granted = new Set<string>([
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.read,
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.boq,
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.measurements,
      QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.certificates,
    ]);
    const hasPermission = vi.fn((permission: string) =>
      granted.has(permission)
    );

    expect(getQuantitySurveyWorkspaceAccess(hasPermission)).toEqual({
      canRead: true,
      canManageBoq: true,
      canManageEstimates: false,
      canManageMeasurements: true,
      canManageValuations: false,
      canManageCertificates: true,
      canManageVariations: false,
    });
    expect(hasPermission).toHaveBeenCalledWith(
      'quantity-survey.certificates.manage'
    );
    expect(hasPermission).toHaveBeenCalledWith(
      'quantity-survey.valuations.manage'
    );
  });

  it('does not grant mutation access from read permission alone', () => {
    const access = getQuantitySurveyWorkspaceAccess(
      (permission) => permission === QUANTITY_SURVEY_WORKSPACE_PERMISSIONS.read
    );

    expect(access.canRead).toBe(true);
    expect(access.canManageBoq).toBe(false);
    expect(access.canManageEstimates).toBe(false);
    expect(access.canManageMeasurements).toBe(false);
    expect(access.canManageValuations).toBe(false);
    expect(access.canManageCertificates).toBe(false);
    expect(access.canManageVariations).toBe(false);
  });
});
