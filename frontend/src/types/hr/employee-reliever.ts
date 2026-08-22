/**
 * Pre-defined relievers: who covers for an employee, and in what order.
 *
 * ⚠ The roster is a **default source, never an override**. A leave request carries its own reliever
 * ids, and those stay authoritative for that request — the roster only decides what the form starts
 * with. That is the whole of Decision 3 in the areas 19-23 plan.
 */

// Mirrors EmployeeRelieverDto.
export interface EmployeeReliever {
  id: string;
  employeeId: string;
  /** Whose cover this is. Added in slice 7 — the DTO used to name only the reliever. */
  employeeName: string;
  relieverEmployeeId: string;
  relieverName: string;
  relieverPositionName?: string | null;
  relieverOrganizationUnitName?: string | null;
  /** 1 = primary, 2 = backup, and so on. Ordinal, so it starts at 1. */
  priority: number;
  isActive: boolean;
}

// Mirrors CreateEmployeeRelieverDto.
export interface CreateEmployeeRelieverRequest {
  employeeId: string;
  relieverEmployeeId: string;
  priority: number;
  isActive: boolean;
}

// Mirrors UpdateEmployeeRelieverDto — note it carries NO employeeId. A roster row cannot be moved
// to a different employee, and sending one would be sending a key the endpoint does not model.
export interface UpdateEmployeeRelieverRequest {
  relieverEmployeeId: string;
  priority: number;
  isActive: boolean;
}
