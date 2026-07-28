import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  Employee,
  EmployeeDetail,
  EmployeeSearchRequest,
  CreateEmployeeRequest,
  UpdateEmployeeRequest,
  TerminateEmployeeRequest,
} from '@/types/hr/employee';

/**
 * Employee access. Backend route: api/hr/Employees.
 * NOTE: the list endpoint is `POST /paged` with an EmployeeSearchDto body plus
 * page/pageSize query params (not a GET query-string). Deep sub-resources
 * (dependents, qualifications, bank details, etc.) are deferred.
 */
class EmployeeService {
  private readonly baseUrl = '/hr/Employees';

  searchPaged(
    search: EmployeeSearchRequest = {},
    page = 1,
    pageSize = 20,
  ): Promise<PagedResult<Employee>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    return apiService.post<PagedResult<Employee>>(`${this.baseUrl}/paged?${params.toString()}`, search);
  }

  getById(id: string): Promise<Employee> {
    return apiService.get<Employee>(`${this.baseUrl}/${id}`);
  }

  getDetails(id: string): Promise<EmployeeDetail> {
    return apiService.get<EmployeeDetail>(`${this.baseUrl}/${id}/details`);
  }

  create(data: CreateEmployeeRequest): Promise<EmployeeDetail> {
    return apiService.post<EmployeeDetail>(this.baseUrl, data);
  }

  update(id: string, data: UpdateEmployeeRequest): Promise<EmployeeDetail> {
    return apiService.put<EmployeeDetail>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  deactivate(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/deactivate`);
  }

  activate(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/activate`);
  }

  terminate(id: string, data: TerminateEmployeeRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/terminate`, data);
  }

  reinstate(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reinstate`, {});
  }
}

export const employeeService = new EmployeeService();
