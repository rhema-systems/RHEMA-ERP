/**
 * The tenant's contract-kind vocabulary — the list an appointment letter picks from.
 *
 * ⚠ **Not the same thing as `EmploymentType`.** That enum is the system's fixed set and code
 * branches on it: the probation rule is keyed to `Permanent`, and the staff-number register is
 * chosen by it. This is the organisation's own naming (at TDC: Permanent, Contract, Fixed Term,
 * National Service, Internship, Casual, Consultancy), and its `duration` is what gives a fixed-term
 * contract its end date.
 *
 * The table was seeded in 2026 and then reachable from nowhere at all — no DTO, no service, no
 * screen. Surfaced in round-2 lane D1.
 */
export interface ContractType {
  id: string;
  code?: string | null;
  name: string;
  description?: string | null;
  /** Months. Zero means open-ended — a permanent appointment has no scheduled end. */
  duration: number;
  isActive: boolean;
  /** How many contracts name this kind, so a retire decision can see its reach. */
  contractCount: number;
}

export interface CreateContractType {
  code?: string | null;
  name: string;
  description?: string | null;
  duration: number;
  isActive: boolean;
}

export type UpdateContractType = CreateContractType;

/** "12 months" / "Open-ended", for a picker label and a table cell. */
export function contractTypeDuration(duration: number): string {
  if (duration <= 0) return 'Open-ended';
  return duration === 1 ? '1 month' : `${duration} months`;
}
