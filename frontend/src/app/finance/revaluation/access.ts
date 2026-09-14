export const FINANCE_READ_PERMISSION = 'Finance.Read';
export const RUN_FX_REVALUATION_PERMISSION = 'Finance.FX.Revaluation.Run';

export type PermissionCheck = (permission: string) => boolean;

export function getFxRevaluationAccess(hasPermission: PermissionCheck) {
    const canRead = hasPermission(FINANCE_READ_PERMISSION);
    return {
        canRead,
        canRun: canRead && hasPermission(RUN_FX_REVALUATION_PERMISSION),
    };
}
