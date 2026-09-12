import type { EmployeeFormValues } from './EmployeeForm';
import type {
  CreateEmployeeRequest,
  Gender,
  MaritalStatus,
  StaffStatus,
  EmploymentType,
  BloodType,
  OffPayrollReason,
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
    // ⚠ Still sent, and still meaningful — but only for a country with no division scheme. When
    // geoAreaId is set the server overwrites both from the tree, deliberately, so the structured
    // link and the printed text can never disagree.
    city: s(values.city),
    state: s(values.state),
    postalCode: s(values.postalCode),
    digitalAddress: s(values.digitalAddress),
    countryId: s(values.countryId),
    geoAreaId: s(values.geoAreaId),
    // ⚠ A null geoAreaId means "not supplied" to the update DTO, so emptying the picker has to say
    // so explicitly or the save would succeed and change nothing. Only meaningful on update; the
    // create DTO ignores it.
    clearGeoArea: !values.geoAreaId,
    emailAddress: s(values.emailAddress),
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
    // Off payroll: no salary and every switch off, whatever the hidden fields still hold —
    // the service refuses an off-payroll record that carries either.
    isOnPayroll: values.isOnPayroll,
    offPayrollReason: values.isOnPayroll ? null : ((values.offPayrollReason || null) as OffPayrollReason | null),
    offPayrollNote: values.isOnPayroll ? null : s(values.offPayrollNote),
    salary: values.isOnPayroll && values.salary && values.salary.trim() ? Number(values.salary) : null,
    payTax: values.isOnPayroll && values.payTax,
    ssFund: values.isOnPayroll && values.ssFund,
    grossUp: values.isOnPayroll && values.grossUp,
    tier2Only: values.isOnPayroll && values.tier2Only,
    overtime: values.isOnPayroll && values.overtime,
    badgeNumber: s(values.badgeNumber),
    notes: s(values.notes),
    isExpatriate: values.isExpatriate,
  };
}
