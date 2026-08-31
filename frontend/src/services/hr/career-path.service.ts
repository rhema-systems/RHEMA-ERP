import { apiService } from '../api.service';

/**
 * One employee's career history.
 *
 * ⚠ **These rows are normally written by the machine, not by hand.** Implementing a movement closes
 * the open step and opens the next one, which is why the create/update/delete had no caller: the
 * only writer was the movements engine. The decision on 2026-08-28 was that they are nevertheless
 * *not* server-write-only and should be correctable — a history that is wrong and unfixable is worse
 * than one somebody can annotate.
 *
 * ⚠ **The update is four fields.** `endDate`, `isCurrent`, `achievements`, `keyProjects` — and
 * nothing else. The position, unit, salary and movement link are what the engine wrote, and are
 * corrected by correcting the movement rather than the history row. A form offering them would be
 * offering to make the history disagree with the movement that caused it. Proven against the DTO and
 * the running API by `hr-tierb-tail/probe-lane2-groupB.mjs`.
 */
export interface CareerPathStep {
  id: string;
  employeeId: string;
  positionId: string;
  positionTitle?: string | null;
  organizationUnitId: string;
  organizationUnitName?: string | null;
  locationName?: string | null;
  startDate: string;
  endDate?: string | null;
  isCurrent: boolean;
  movementId?: string | null;
  movementNumber?: string | null;
  salary: number;
  salaryGradeName?: string | null;
  achievements?: string | null;
  keyProjects?: string | null;
}

export interface CreateCareerPathStep {
  employeeId: string;
  positionId: string;
  organizationUnitId: string;
  organizationLevelId?: string | null;
  locationId?: string | null;
  startDate: string;
  endDate?: string | null;
  isCurrent: boolean;
  movementId?: string | null;
  salary: number;
  salaryGradeId?: string | null;
  achievements?: string | null;
  keyProjects?: string | null;
}

/** The four fields the server will take. See the class remark. */
export interface UpdateCareerPathStep {
  id: string;
  endDate?: string | null;
  isCurrent: boolean;
  achievements?: string | null;
  keyProjects?: string | null;
}

class CareerPathService {
  private readonly baseUrl = '/employee-career-paths';

  forEmployee(employeeId: string) {
    return apiService.get<CareerPathStep[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  create(payload: CreateCareerPathStep) {
    return apiService.post<CareerPathStep>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateCareerPathStep) {
    return apiService.put<CareerPathStep>(`${this.baseUrl}/${id}`, payload);
  }

  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const careerPathService = new CareerPathService();
