import { redirect } from 'next/navigation';

/**
 * `/hr/grievances` → `/hr/employee-relations`.
 *
 * The register moved in area 9c slice 10, and the old path is kept as a redirect rather than
 * deleted because it has been the desk's bookmark since area 9 slice 7.
 *
 * ⚠ **The screen it replaces had two defects, and neither was cosmetic.** It linked every row to
 * `/me/grievances/[id]` — the employee's own portal detail — so an HR officer browsing the register
 * was sent into the portal shell to do desk work. That happened to function, because the portal
 * detail carries the assign and respond mutations too, but the desk and the portal were sharing one
 * screen by accident rather than by design. And it read every case in the tenant and filtered the
 * array in the browser, which was 442 rows before anyone noticed.
 *
 * It was also the wrong NAME. The register holds mediations, welfare matters and union
 * consultations as well as grievances, and has done since slice 1.
 */
export default function GrievancesRegisterRedirect() {
  redirect('/hr/employee-relations');
}
