import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface Role {
  id: string;
  name: string;
  description?: string;
  roleType: string;
  parentRoleId?: string;
  level: number;
  isSystemRole: boolean;
  isActive: boolean;
  sortOrder: number;
  color?: string;
  icon?: string;
  createdAtUtc: string;
  lastModifiedAtUtc?: string;
  createdBy?: string;
  updatedBy?: string;
  updatedAtUtc?: string;
  permissionCount: number;
  permissionNames: string[];
  permissions?: Permission[];
  userCount: number;
  showDropdown?: boolean;
  dropdownUp?: boolean;
  organizationId?: string; // Organization ID
  organizationName?: string; // Organization Name (for System Admin view)
}

export interface CreateRoleRequest {
  name: string;
  description?: string;
  roleType: string;
  parentRoleId?: string;
  sortOrder: number;
  color?: string;
  icon?: string;
  isActive: boolean;
}

export interface UpdateRoleRequest {
  name: string;
  description?: string;
  roleType: string;
  parentRoleId?: string;
  isActive: boolean;
  sortOrder: number;
  color?: string;
  icon?: string;
}

export interface RoleStatistics {
  total: number;
  active: number;
  inactive: number;
  systemRoles: number;
  businessRoles: number;
}

export interface PaginatedRolesResponse {
  items: Role[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
  currentPage?: number;
}

export interface PaginatedPermissionsResponse {
  items: Permission[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
  currentPage?: number;
}

export interface ImportResult {
  successCount: number;
  skippedCount: number;
  errorCount: number;
  errors?: string[];
  errorReportId?: string;
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

// Unified import/export history
export interface ImportExportHistory {
  id: string;
  jobId: string; // Job ID for downloading exports
  entityType: string; // "User", "Role", etc.
  operationType: string; // "Import" or "Export"
  fileName: string;
  format: string; // "Excel", "CSV", "PDF", "JSON"
  totalRows: number;
  successCount: number;
  updatedCount: number;
  skippedCount: number;
  errorCount: number;
  status: string; // "Pending", "Processing", "Completed", "Failed"
  progress: number; // 0-100
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

export interface Permission {
  id: string;
  code: string;
  name: string;
  description?: string;
  module: string;
  action: string;
  resource: string;
  isSystemPermission: boolean;
  isActive: boolean;
  sortOrder: number;
  category?: string;
  createdAtUtc: string;
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  isEmailVerified: boolean;
  department?: string;
  jobTitle?: string;
  avatarUrl?: string;
  createdAtUtc: string;
}

export interface RoleHierarchy {
  id: string;
  name: string;
  description?: string;
  roleType: string;
  parentRoleId?: string;
  level: number;
  isActive: boolean;
  sortOrder: number;
  color?: string;
  icon?: string;
  children: RoleHierarchy[];
  userCount: number;
  permissionCount: number;
}

export interface DropdownOptions {
  roleTypes: string[];
  parentRoles: Role[];
}

@Injectable({
  providedIn: 'root'
})
export class RolesService {
  private readonly apiUrl = `${environment.apiBaseUrl}/api/${environment.apiVersion}/roles`;

  constructor(private http: HttpClient) {}

  /**
   * Get paginated roles with server-side filtering, sorting, and pagination
   */
  getData(params: {
    page: number;
    pageSize: number;
    search?: string;
    sortField?: string;
    sortDirection?: 'asc' | 'desc';
    roleType?: string;
    isActive?: boolean;
    parentRoleId?: string;
    organizationId?: string;
    createdFrom?: string;
    createdTo?: string;
  }): Observable<PaginatedRolesResponse> {
    let httpParams = new HttpParams()
      .set('page', params.page.toString())
      .set('pageSize', params.pageSize.toString())
      .set('sortField', params.sortField || 'createdAtUtc')
      .set('sortDirection', params.sortDirection || 'desc');

    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.roleType && params.roleType !== 'all') {
      httpParams = httpParams.set('roleType', params.roleType);
    }
    if (params.isActive !== undefined) {
      httpParams = httpParams.set('isActive', params.isActive.toString());
    }
    if (params.parentRoleId && params.parentRoleId !== 'all' && params.parentRoleId !== '') {
      httpParams = httpParams.set('parentRoleId', params.parentRoleId);
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

    return this.http.get<PaginatedRolesResponse>(this.apiUrl, { params: httpParams });
  }

  /**
   * Get role by ID
   */
  getRoleById(id: string): Observable<Role> {
    return this.http.get<Role>(`${this.apiUrl}/${id}`);
  }

  /**
   * Create new role
   */
  create(role: CreateRoleRequest): Observable<Role> {
    return this.http.post<Role>(this.apiUrl, role);
  }

  /**
   * Update existing role
   */
  update(id: string, role: UpdateRoleRequest): Observable<Role> {
    return this.http.put<Role>(`${this.apiUrl}/${id}`, role);
  }

  /**
   * Delete single role
   */
  delete(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  /**
   * Delete multiple roles
   */
  deleteMultiple(ids: string[]): Observable<any> {
    return this.http.post(`${this.apiUrl}/bulk-delete`, { ids });
  }

  cloneMultiple(ids: string[]): Observable<{ message: string; clonedRoles: Role[] }> {
    return this.http.post<{ message: string; clonedRoles: Role[] }>(`${this.apiUrl}/bulk-clone`, { ids });
  }

  /**
   * Set role active status
   */
  setActive(id: string, isActive: boolean): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}/active?isActive=${isActive}`, null);
  }

  /**
   * Get role statistics
   */
  getStatistics(): Observable<RoleStatistics> {
    return this.http.get<RoleStatistics>(`${this.apiUrl}/statistics`);
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
   * Import roles from file
   */
  importData(formData: FormData): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.apiUrl}/import`, formData);
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
    roleType?: string;
    isActive?: boolean;
    parentRoleId?: string;
    createdFrom?: string;
    createdTo?: string;
    selectedIds?: string[];
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
   * Get role hierarchy
   */
  getHierarchy(): Observable<RoleHierarchy[]> {
    return this.http.get<RoleHierarchy[]>(`${this.apiUrl}/hierarchy/tree`);
  }

  /**
   * Get permissions for a role
   */
  getRolePermissions(id: string): Observable<Permission[]> {
    return this.http.get<Permission[]>(`${this.apiUrl}/${id}/permissions`);
  }

  /**
   * Get all available permissions in the system
   */
  getAllPermissions(): Observable<Permission[]> {
    return this.http.get<any>(`${environment.apiBaseUrl}/api/${environment.apiVersion}/permissions`).pipe(
      map(response => {
        // Handle paginated response format
        if (response && Array.isArray(response.items)) {
          return response.items;
        } else if (Array.isArray(response)) {
          return response;
        } else {
          console.warn('Unexpected permissions response format:', response);
          return [];
        }
      })
    );
  }

  /**
   * Get unique modules for dropdown
   */
  getPermissionModules(): Observable<string[]> {
    return this.http.get<string[]>(`${environment.apiBaseUrl}/api/${environment.apiVersion}/permissions/modules`);
  }

  /**
   * Get unique actions for dropdown
   */
  getPermissionActions(): Observable<string[]> {
    return this.http.get<string[]>(`${environment.apiBaseUrl}/api/${environment.apiVersion}/permissions/actions`);
  }

  /**
   * Get paginated permissions with server-side filtering
   */
  getPermissions(params: {
    page: number;
    pageSize: number;
    search?: string;
    category?: string;
    module?: string;
    action?: string;
    isActive?: boolean;
    sortField?: string;
    sortDirection?: 'asc' | 'desc';
  }): Observable<PaginatedPermissionsResponse> {
    let httpParams = new HttpParams()
      .set('page', params.page.toString())
      .set('pageSize', params.pageSize.toString())
      .set('sortField', params.sortField || 'name')
      .set('sortDirection', params.sortDirection || 'asc');

    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.category) {
      httpParams = httpParams.set('category', params.category);
    }
    if (params.module) {
      httpParams = httpParams.set('module', params.module);
    }
    if (params.action) {
      httpParams = httpParams.set('action', params.action);
    }
    if (params.isActive !== undefined) {
      httpParams = httpParams.set('isActive', params.isActive.toString());
    }

    return this.http.get<PaginatedPermissionsResponse>(`${environment.apiBaseUrl}/api/${environment.apiVersion}/permissions`, { params: httpParams });
  }

  /**
   * Assign permission to role
   */
  assignPermission(roleId: string, permissionId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${roleId}/permissions/${permissionId}`, null);
  }

  /**
   * Remove permission from role
   */
  removePermission(roleId: string, permissionId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${roleId}/permissions/${permissionId}`);
  }

  /**
   * Get users assigned to role
   */
  getRoleUsers(id: string): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/${id}/users`);
  }

  /**
   * Remove role from user
   */
  removeRoleFromUser(userId: string, roleId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/unassign`, { userId, roleId });
  }

  /**
   * Assign role to user
   */
  assignRoleToUser(userId: string, roleId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/assign`, { userId, roleId });
  }

  /**
   * Get roles for a user
   */
  getUserRoles(userId: string): Observable<Role[]> {
    return this.http.get<Role[]>(`${this.apiUrl}/user/${userId}`);
  }

}
