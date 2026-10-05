/**
 * What a travel policy's badge may truthfully say.
 *
 * ⚠ The register and the policy page used to badge every approved, non-current policy
 * "Superseded" — but withdrawing a policy and superseding it both just clear `isCurrentVersion`, so
 * a withdrawn policy read as replaced by a newer one when nothing replaced it. And "In force" was
 * shown for a current version whose dates had not started or had ended, although the guard applies
 * a policy only to trips inside `effectiveFrom`–`effectiveTo` (`GetApplicablePoliciesAsync`). This
 * says only what the record can support (travel final closure, lane 0).
 */
export interface TravelPolicyStateInput {
  approvedById?: string | null;
  isCurrentVersion: boolean;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export type TravelPolicyBadgeVariant = 'default' | 'secondary' | 'outline';

export function travelPolicyState(
  p: TravelPolicyStateInput,
  today: string = new Date().toISOString().slice(0, 10),
): { label: string; variant: TravelPolicyBadgeVariant } {
  if (!p.approvedById) return { label: 'Draft — not enforcing', variant: 'outline' };
  // Withdrawn and superseded are the same flag; the record cannot say which happened.
  if (!p.isCurrentVersion) return { label: 'Not in force', variant: 'secondary' };
  const from = p.effectiveFrom.slice(0, 10);
  if (from > today) {
    return { label: `Approved — in force from ${new Date(from).toLocaleDateString()}`, variant: 'outline' };
  }
  if (p.effectiveTo && p.effectiveTo.slice(0, 10) < today) return { label: 'Expired', variant: 'secondary' };
  return { label: 'In force', variant: 'default' };
}
