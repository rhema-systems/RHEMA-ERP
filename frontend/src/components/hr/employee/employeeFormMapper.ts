import type { EmployeeFormValues } from './EmployeeForm';
import type {
  CreateEmployeeRequest,
  Gender,
  MaritalStatus,
  StaffStatus,
  EmploymentType,
  BloodType,
} from '@/types/hr/employee';

// Optional string → null.
const s = (v?: string | null) => (v && v.trim() ? v.trim() : null);

/**
 * Maps the employee form values to the create/update request shape.
 * Empty strings become null; enum strings are narrowed; salary parses to a number.
 * Department/Section are intentionally not sent (deprecated; org unit is derived
 * from the selected position).
 */
export function employeeFormToRequest(values: EmployeeFormValues): CreateEmployeeRequest {
  return {
    employeeNumber: values.employeeNumber?.trim() || undefined,
    firstName: values.firstName,
    middleName: s(values.middleName),
    lastName: values.lastName,
    title: s(values.title),
    gender: (values.gender || null) as Gender | null,
    dateOfBirth: s(values.dateOfBirth),
    maritalStatus: (values.maritalStatus || null) as MaritalStatus | null,
    religion: s(values.religion),
    isFullTime: values.isFullTime,
    dateEmployed: s(values.dateEmployed),
    address: s(values.address),
    city: s(values.city),
    state: s(values.state),
    postalCode: s(values.postalCode),
    digitalAddress: s(values.digitalAddress),
    emailAddress: values.emailAddress,
    telephoneNumber: s(values.telephoneNumber),
    mobileNumber: s(values.mobileNumber),
    employmentType: values.employmentType as EmploymentType,
    probationPeriodDays: values.probationPeriodDays,
    positionId: values.positionId,
    organizationUnitId: values.organizationUnitId,
    locationId: values.locationId,
    managerId: s(values.managerId),
    staffStatus: values.staffStatus as StaffStatus,
    taxNumber: s(values.taxNumber),
    socialSecurityNumber: s(values.socialSecurityNumber),
    tinNumber: s(values.tinNumber),
    bloodType: (values.bloodType || null) as BloodType | null,
    salary: values.salary && values.salary.trim() ? Number(values.salary) : null,
    payTax: values.payTax,
    ssFund: values.ssFund,
    grossUp: values.grossUp,
    tier2Only: values.tier2Only,
    overtime: values.overtime,
    badgeNumber: s(values.badgeNumber),
    notes: s(values.notes),
    isExpatriate: values.isExpatriate,
  };
}
