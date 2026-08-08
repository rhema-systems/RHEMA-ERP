/**
 * Scope rendering for the things in Performance that are targeted at part of the organisation:
 * appraisal templates, goal library items, cycle targets.
 *
 * All of them carry the same three optional ids, and all of them resolve most-specific-first —
 * a position beats a unit, which beats a level, and nothing at all means the row applies
 * everywhere. Rendering it in one place keeps the template list, the template detail page and
 * the coverage preview describing the same row the same way.
 */
export interface AppraisalScopeNames {
  positionTitle?: string | null;
  organizationUnitName?: string | null;
  organizationLevelName?: string | null;
}

export function scopeLabel(scope: AppraisalScopeNames): string {
  if (scope.positionTitle) return `Position: ${scope.positionTitle}`;
  if (scope.organizationUnitName) return `Unit: ${scope.organizationUnitName}`;
  if (scope.organizationLevelName) return `Level: ${scope.organizationLevelName}`;
  return 'Global';
}
