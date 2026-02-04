import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Department {
  id: string;
  name: string;
  description?: string;
  code?: string;
  managerId?: string;
  managerName?: string;
  isActive: boolean;
  sortOrder: number;
  createdAtUtc: string;
  lastModifiedAtUtc: string;
  createdBy?: string;
  showDropdown?: boolean;
  dropdownUp?: boolean;
  organizationId?: string; // Organization ID
  organizationName?: string; // Organization Name (for System Admin view)
}

export interface CreateDepartmentRequest {
  name: string;
  description?: string;
  code?: string;
  managerId?: string;
  managerName?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpdateDepartmentRequest {
  name: string;
  description?: string;
  code?: string;
  managerId?: string;
  managerName?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface DepartmentStatistics {
  total: number;
  active: number;
  inactive: number;
}

export interface PaginatedDepartmentsResponse {
  items: Department[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
  currentPage?: number;
}

export interface ImportJobStatus {
  jobId: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
  progressPercent: number;
  processedRows: number;
  totalRows: number;
  successCount: number;
  updatedCount: number;
  skippedCount: number;
  errorCount: number;
  startedAt: string;
  completedAt?: string;
  message?: string;
}

export interface ImportExportHistory {
  id: string;
  jobId: string;
  entityType: string;
  operationType: string;
  fileName: string;
  format: string;
  totalRows: number;
  successCount: number;
  updatedCount: number;
  skippedCount: number;
  errorCount: number;
  status: string;
  progress: number;
  duplicateHandlingStrategy?: string;
  errorReportId?: string;
  downloadUrl?: string;
  appliedFilters?: string;
  fileSizeBytes: number;
  importedBy: string;
  createdAtUtc: string;
  completedAtUtc?: string;
  errorMessage?: string;
}

export interface ImportExportHistoryResponse {
  items: ImportExportHistory[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ExportJobStatus {
  jobId: string;
  entityType: string;
  format: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
  progressPercent: number;
  totalRows: number;
  processedRows: number;
  message?: string;
  downloadUrl?: string;
  fileSizeBytes?: number;
  startedAt: string;
  completedAt?: string;
  expiresAt?: string;
}

export interface DepartmentDropdownOptions {
  levels: string[];
}

@Injectable({
  providedIn: 'root'
})
export class DepartmentsService {
  private readonly apiUrl = `${environment.apiBaseUrl}/api/${environment.apiVersion}/departments`;

  constructor(private http: HttpClient) {}

  getData(params: {
    page: number;
    pageSize: number;
    search?: string;
    sortField?: string;
    sortDirection?: 'asc' | 'desc';
    isActive?: boolean;
    createdFrom?: string;
    createdTo?: string;
    organizationId?: string;
  }): Observable<PaginatedDepartmentsResponse> {
    let httpParams = new HttpParams()
      .set('page', params.page.toString())
      .set('pageSize', params.pageSize.toString())
      .set('sortField', params.sortField || 'createdAtUtc')
      .set('sortDirection', params.sortDirection || 'desc');

    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.isActive !== undefined) {
      httpParams = httpParams.set('isActive', params.isActive.toString());
    }
    if (params.createdFrom) {
      httpParams = httpParams.set('createdFrom', params.createdFrom);
    }
    if (params.createdTo) {
      httpParams = httpParams.set('createdTo', params.createdTo);
    }
    if (params.organizationId && params.organizationId !== 'all' && params.organizationId !== '') {
      httpParams = httpParams.set('organizationId', params.organizationId);
    }

    return this.http.get<PaginatedDepartmentsResponse>(this.apiUrl, { params: httpParams });
  }

  getDepartmentById(id: string): Observable<Department> {
    return this.http.get<Department>(`${this.apiUrl}/${id}`);
  }

  create(department: CreateDepartmentRequest): Observable<Department> {
    return this.http.post<Department>(this.apiUrl, department);
  }

  update(id: string, department: UpdateDepartmentRequest): Observable<Department> {
    return this.http.put<Department>(`${this.apiUrl}/${id}`, department);
  }

  delete(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  deleteMultiple(ids: string[]): Observable<any> {
    return this.http.post(`${this.apiUrl}/bulk-delete`, { ids });
  }

  cloneMultiple(ids: string[]): Observable<{ message: string; clonedDepartments: Department[] }> {
    return this.http.post<{ message: string; clonedDepartments: Department[] }>(`${this.apiUrl}/bulk-clone`, { ids });
  }

  getStatistics(): Observable<DepartmentStatistics> {
    return this.http.get<DepartmentStatistics>(`${this.apiUrl}/statistics`);
  }

  getTemplate(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/template`, {
      responseType: 'blob'
    });
  }

  startImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.apiUrl}/import/async`, formData);
  }

  getImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.apiUrl}/import/jobs/${jobId}`);
  }

  getImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/import/error-report/${errorReportId}`, {
      responseType: 'blob'
    });
  }

  getHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (type) {
      params = params.set('type', type);
    }

    return this.http.get<ImportExportHistoryResponse>(`${this.apiUrl}/history`, { params });
  }

  startExportAsync(params: {
    format: 1 | 2 | 3 | 4;
    search?: string;
    isActive?: boolean;
    selectedIds?: string[];
    createdFrom?: string;
    createdTo?: string;
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.apiUrl}/export/async`, params);
  }

  getExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.apiUrl}/export/jobs/${jobId}`);
  }

  downloadExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export/jobs/${jobId}/download`, {
      responseType: 'blob'
    });
  }

  getDropdownOptions(): Observable<DepartmentDropdownOptions> {
    return this.http.get<DepartmentDropdownOptions>(`${this.apiUrl}/dropdown-options`);
  }

  setActive(id: string, isActive: boolean): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}/active`, null, {
      params: new HttpParams().set('isActive', isActive.toString())
    });
  }
}

