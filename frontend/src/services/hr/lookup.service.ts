import { apiService } from '../api.service';
import type {
  Qualification,
  QualificationRequest,
  IdentificationType,
  CreateIdentificationTypeRequest,
  UpdateIdentificationTypeRequest,
  ReasonCode,
  ReasonCodeRequest,
  Department,
} from '@/types/hr/lookups';

/**
 * HR reference lookups. Each sits on its own controller with its own route — there is no
 * uniform `/hr` prefix in the ported module, so the base paths differ deliberately.
 */

/** api/hr/qualifications — the qualification catalogue behind employee qualifications. */
class QualificationService {
  private readonly baseUrl = '/hr/qualifications';

  getAll(): Promise<Qualification[]> {
    return apiService.get<Qualification[]>(this.baseUrl);
  }

  getActive(): Promise<Qualification[]> {
    return apiService.get<Qualification[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<Qualification> {
    return apiService.get<Qualification>(`${this.baseUrl}/${id}`);
  }

  create(data: QualificationRequest): Promise<Qualification> {
    return apiService.post<Qualification>(this.baseUrl, data);
  }

  /** Update reuses the create DTO on the backend. */
  update(id: string, data: QualificationRequest): Promise<Qualification> {
    return apiService.put<Qualification>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/hr/IdentificationTypes — ID document types behind employee identification cards. */
class IdentificationTypeService {
  private readonly baseUrl = '/hr/IdentificationTypes';

  getAll(): Promise<IdentificationType[]> {
    return apiService.get<IdentificationType[]>(this.baseUrl);
  }

  getActive(): Promise<IdentificationType[]> {
    return apiService.get<IdentificationType[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<IdentificationType> {
    return apiService.get<IdentificationType>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateIdentificationTypeRequest): Promise<IdentificationType> {
    return apiService.post<IdentificationType>(this.baseUrl, data);
  }

  update(id: string, data: UpdateIdentificationTypeRequest): Promise<IdentificationType> {
    return apiService.put<IdentificationType>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  activate(id: string): Promise<IdentificationType> {
    return apiService.put<IdentificationType>(`${this.baseUrl}/${id}/activate`);
  }

  deactivate(id: string): Promise<IdentificationType> {
    return apiService.put<IdentificationType>(`${this.baseUrl}/${id}/deactivate`);
  }
}

/** api/reason-codes — note: no `/hr` prefix on this one. */
class ReasonCodeService {
  private readonly baseUrl = '/reason-codes';

  getAll(): Promise<ReasonCode[]> {
    return apiService.get<ReasonCode[]>(this.baseUrl);
  }

  getById(id: string): Promise<ReasonCode> {
    return apiService.get<ReasonCode>(`${this.baseUrl}/${id}`);
  }

  create(data: ReasonCodeRequest): Promise<ReasonCode> {
    return apiService.post<ReasonCode>(this.baseUrl, data);
  }

  update(id: string, data: ReasonCodeRequest): Promise<ReasonCode> {
    return apiService.put<ReasonCode>(`${this.baseUrl}/${id}`, data);
  }

  deactivate(id: string): Promise<ReasonCode> {
    return apiService.patch<ReasonCode>(`${this.baseUrl}/${id}/deactivate`);
  }
}

/**
 * api/departments — READ ONLY. DepartmentsController exposes GET only (list, active,
 * by id); its create/update/delete service methods are deliberately not HTTP-wired, so
 * there is no department CRUD screen. Departments feed Employee.DepartmentId and other
 * modules; HR's own hierarchy is Organization Structure → Level → Unit.
 */
class DepartmentService {
  private readonly baseUrl = '/departments';

  getAll(): Promise<Department[]> {
    return apiService.get<Department[]>(this.baseUrl);
  }

  getActive(): Promise<Department[]> {
    return apiService.get<Department[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<Department> {
    return apiService.get<Department>(`${this.baseUrl}/${id}`);
  }
}

export const qualificationService = new QualificationService();
export const identificationTypeService = new IdentificationTypeService();
export const reasonCodeService = new ReasonCodeService();
export const departmentService = new DepartmentService();
