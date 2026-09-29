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
 *
 * @param options.probationIsDerived
 *   True when the selected POSITION states a probation period, in which case the term is not this
 *   form's to send. ⚠ The server refuses a `probationPeriodDays` that contradicts the position, and
 *   the edit form loads whatever the employee currently holds — so a position change with the old
 *   term still in the payload would be refused on a screen the user had not touched. Sending null
 *   asks the server to derive it, which is the whole rule.
 * @param options.importMode
 *   True on the import door, the only path that may supply a confirmation date.
 */
export function employeeFormToRequest(
  values: EmployeeFormValues,
  options: { probationIsDerived?: boolean; importMode?: boolean } = {},
): CreateEmployeeRequest {
  return {
    employeeNumber: values.employeeNumber?.trim() || undefined,
    firstName: values.firstName,
    middleName: s(values.middleName),
    lastName: values.lastName,
    title: s(values.title),
    gender: (values.gender || null) as Gender | null,
    // ⚠ These four were collected by the form and never sent — lane 3a proved the API, and the
    // mapper was the layer nothing asserted (demo feedback round 2, lane A-1). Only meaningful
    // beside a gender of Other, but sent regardless: the server keeps whatever it is given.
    genderDescription: values.gender === 'Other' ? s(values.genderDescription) : null,
    hometown: s(values.hometown),
    hasDisability: values.hasDisability,
    // Round 3, lane P2: the catalogue row travels only with the tick; the server refuses one without it.
    disabilityTypeId: values.hasDisability ? s(values.disabilityTypeId) : null,
    disabilityDescription: values.hasDisability ? s(values.disabilityDescription) : null,
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
    contractTypeId: s(values.contractTypeId),
    probationPeriodDays: options.probationIsDerived ? null : values.probationPeriodDays,
    // Refused by the server on every other path — a hire has not passed a probation that has not
    // started, and an ordinary edit is not how a confirmation is recorded.
    confirmationDate: options.importMode ? s(values.confirmationDate) : null,
    positionId: values.positionId,
    organizationUnitId: values.organizationUnitId,
    locationId: values.locationId,
    managerId: s(values.managerId),
    staffStatus: values.staffStatus as StaffStatus,
    taxNumber: s(values.taxNumber),
    socialSecurityNumber: s(values.socialSecurityNumber),
    tinNumber: s(values.tinNumber),
    bloodType: (values.bloodType || null) as BloodType | null,
    // Membership only. ⚠ The salary and the five switches are deliberately NOT sent — not as
    // null, not at all. On the update DTO they are nullable and "absent" means "untouched"; this
    // mapper used to re-send all five on every edit, so an edit of somebody's phone number could
    // re-assert switches the Salary tab had since changed. They are the tab's now (Q-3).
    isOnPayroll: values.isOnPayroll,
    offPayrollReason: values.isOnPayroll ? null : ((values.offPayrollReason || null) as OffPayrollReason | null),
    offPayrollNote: values.isOnPayroll ? null : s(values.offPayrollNote),
    badgeNumber: s(values.badgeNumber),
    notes: s(values.notes),
    isExpatriate: values.isExpatriate,
    // Round 4, lane O. The choice always travels, so "Follow the position" can hand a by-hand
    // setting back to the post. ⚠ The three texts go as they stand, NOT through s(): the update reads
    // null as "leave it", so an emptied field must arrive as '' to be cleared.
    maintenanceAssignment: values.maintenanceAssignment,
    specialization: (values.specialization ?? '').trim(),
    certificationLevel: (values.certificationLevel ?? '').trim(),
    experienceLevel: values.experienceLevel ?? '',
  };
}
