import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Menu {
  id: string;
  label: string;
  route: string;
  icon: string;
  section?: string;
}

export interface Permission {
  id: string;
  code: string;
  name: string;
  description?: string;
  module: string;
  action: string;
  resource: string;
  isSystemPermission: boolean;
  isSystemAdminOnly: boolean;
  isActive: boolean;
  sortOrder: number;
  category?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  createdBy?: string;
  updatedBy?: string;
  menuId: string; // ✅ Menu Foreign Key (Required)
  menu?: Menu; // ✅ Menu Navigation (Optional)
  showDropdown?: boolean;
  dropdownUp?: boolean;
}

export interface CreatePermissionRequest {
  code: string;
  name: string;
  description?: string;
  module: string;
  action: string;
  resource: string;
  sortOrder?: number;
  category?: string;
  menuId: string; // ✅ Menu Foreign Key (Required)
  isSystemAdminOnly?: boolean; // System Admin only flag
}

export interface UpdatePermissionRequest {
  code: string;
  name: string;
  description?: string;
  module: string;
  action: string;
  resource: string;
  isActive: boolean;
  sortOrder: number;
  category?: string;
  menuId: string; // ✅ Menu Foreign Key (Required)
  isSystemAdminOnly?: boolean; // System Admin only flag
}

export interface PermissionStatistics {
  total: number;
  active: number;
  inactive: number;
}

export interface PaginatedPermissionsResponse {
  items: Permission[];
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

export interface DropdownOptions {
  modules: string[];
  actions: string[];
  categories: string[];
}

@Injectable({
  providedIn: 'root'
})
export class PermissionsService {
  private readonly apiUrl = `${environment.apiBaseUrl}/api/${environment.apiVersion}/permissions`;

  constructor(private http: HttpClient) {}

  /**
   * Get paginated permissions with server-side filtering, sorting, and pagination
   */
  getData(params: {
    page: number;
    pageSize: number;
    search?: string;
    sortField?: string;
    sortDirection?: 'asc' | 'desc';
    category?: string;
    module?: string;
    action?: string;
    isActive?: boolean;
    createdFrom?: string;
    createdTo?: string;
  }): Observable<PaginatedPermissionsResponse> {
    let httpParams = new HttpParams()
      .set('page', params.page.toString())
      .set('pageSize', params.pageSize.toString())
      .set('sortField', params.sortField || 'createdAtUtc')
      .set('sortDirection', params.sortDirection || 'asc');

    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.category && params.category !== 'all') {
      httpParams = httpParams.set('category', params.category);
    }
    if (params.module && params.module !== 'all') {
      httpParams = httpParams.set('module', params.module);
    }
    if (params.action && params.action !== 'all') {
      httpParams = httpParams.set('action', params.action);
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

    return this.http.get<PaginatedPermissionsResponse>(this.apiUrl, { params: httpParams });
  }

  /**
   * Get permission by ID
   */
  getPermissionById(id: string): Observable<Permission> {
    return this.http.get<Permission>(`${this.apiUrl}/${id}`);
  }

  /**
   * Create new permission
   */
  create(permission: CreatePermissionRequest): Observable<Permission> {
    return this.http.post<Permission>(this.apiUrl, permission);
  }

  /**
   * Update existing permission
   */
  update(id: string, permission: UpdatePermissionRequest): Observable<Permission> {
    return this.http.put<Permission>(`${this.apiUrl}/${id}`, permission);
  }

  /**
   * Delete single permission
   */
  delete(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  /**
   * Delete multiple permissions
   */
  deleteMultiple(ids: string[]): Observable<any> {
    return this.http.post(`${this.apiUrl}/bulk-delete`, { ids });
  }

  cloneMultiple(ids: string[]): Observable<{ message: string; clonedPermissions: Permission[] }> {
    return this.http.post<{ message: string; clonedPermissions: Permission[] }>(`${this.apiUrl}/bulk-clone`, { ids });
  }

  /**
   * Get permission statistics
   */
  getStatistics(): Observable<PermissionStatistics> {
    return this.http.get<PermissionStatistics>(`${this.apiUrl}/statistics`);
  }

  /**
   * Get import template
   */
  getTemplate(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/template`, {
      responseType: 'blob'
    });
  }

  /**
   * Start async import job
   */
  startImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.apiUrl}/import/async`, formData);
  }

  /**
   * Get async import job status
   */
  getImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.apiUrl}/import/jobs/${jobId}`);
  }

  /**
   * Download import error report
   */
  getImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/import/error-report/${errorReportId}`, {
      responseType: 'blob'
    });
  }

  /**
   * Get unified import/export history
   */
  getHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (type) {
      params = params.set('type', type);
    }

    return this.http.get<ImportExportHistoryResponse>(`${this.apiUrl}/history`, { params });
  }

  /**
   * Start async export job (NEW - non-blocking)
   */
  startExportAsync(params: {
    format: 1 | 2 | 3 | 4; // 1=Excel, 2=CSV, 3=PDF, 4=JSON
    search?: string;
    category?: string;
    module?: string;
    action?: string;
    isActive?: boolean;
    selectedIds?: string[];
    createdFrom?: string;
    createdTo?: string;
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.apiUrl}/export/async`, params);
  }

  /**
   * Get async export job status
   */
  getExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.apiUrl}/export/jobs/${jobId}`);
  }

  /**
   * Download async export file
   */
  downloadExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export/jobs/${jobId}/download`, {
      responseType: 'blob'
    });
  }

  /**
   * Get dropdown options for filters and forms
   */
  getDropdownOptions(): Observable<DropdownOptions> {
    return this.http.get<DropdownOptions>(`${this.apiUrl}/dropdown-options`);
  }

  /**
   * Get unique modules
   */
  getModules(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/modules`);
  }

  /**
   * Get unique actions
   */
  getActions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/actions`);
  }

  /**
   * Get unique categories
   */
  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/categories`);
  }

  /**
   * Get menu dropdown options for permissions
   */
  getMenuDropdownOptions(): Observable<Menu[]> {
    const menusApiUrl = `${environment.apiBaseUrl}/api/${environment.apiVersion}/menus/dropdown`;
    return this.http.get<Menu[]>(menusApiUrl);
  }
}
