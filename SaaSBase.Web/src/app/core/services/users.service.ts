import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface User {
  id: string;
  email: string;
  fullName: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  isActive: boolean;
  isEmailVerified: boolean;
  isPhoneVerified: boolean;
  lastLoginAt?: string;
  avatarUrl?: string;
  timeZone?: string;
  language?: string;
  isMfaEnabled: boolean;
  jobTitle?: string;
  department?: string;
  location?: string;
  employeeId?: string;
  dateOfBirth?: string;
  address?: string;
  city?: string;
  state?: string;
  country?: string;
  postalCode?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyContactRelation?: string;
  createdAtUtc: string;
  createdBy?: string;
  modifiedAtUtc?: string;
  modifiedBy?: string;
  roleId?: string; // Keep for backward compatibility
  roleName?: string; // Keep for backward compatibility
  roleIds?: string[]; // Array of role IDs
  roleNames?: string[]; // Array of role names
  roleDetails?: Array<{ id: string; name: string; icon?: string; color?: string }>; // Role details with icon and color
  showDropdown?: boolean;
  dropdownUp?: boolean;
  lockedUntil?: string; // Account lock expiration date
  organizationId?: string; // Organization ID
  organizationName?: string; // Organization Name (for System Admin view)
}

export interface CreateUserRequest {
  email: string;
  fullName?: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  isActive?: boolean;
  department?: string;
  jobTitle?: string;
  location?: string;
  employeeId?: string;
  roleId: string; // Keep for backward compatibility, but roleIds will be used
  roleIds?: string[]; // Array of role IDs
}

export interface UpdateUserRequest {
  fullName: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  isActive?: boolean;
  department?: string;
  jobTitle?: string;
  location?: string;
  employeeId?: string;
  roleId: string; // Keep for backward compatibility
  roleIds?: string[]; // Array of role IDs - preferred for multi-role support
}

export interface UserStatistics {
  total: number;
  active: number;
  inactive: number;
  emailVerifiedUsers: number;
  emailUnverifiedUsers: number;
  recentlyCreatedUsers: number;
}

export interface PaginatedUsersResponse {
  items: User[];
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

export interface RoleOption {
  id: string;
  name: string;
  description?: string;
}

export interface DropdownOptions {
  locations: string[];
  departments: string[];
  positions: string[];
  roles: RoleOption[];
}

@Injectable({
  providedIn: 'root'
})
export class UsersService {
  private readonly apiUrl = `${environment.apiBaseUrl}/api/${environment.apiVersion}/users`;

  constructor(private http: HttpClient) {}

  /**
   * Get paginated users with server-side filtering, sorting, and pagination
   */
  getData(params: {
    page: number;
    pageSize: number;
    search?: string;
    sortField?: string;
    sortDirection?: 'asc' | 'desc';
    department?: string;
    jobTitle?: string;
    location?: string;
    isActive?: boolean;
    isEmailVerified?: boolean;
    roleId?: string;
    organizationId?: string;
    createdFrom?: string;
    createdTo?: string;
  }): Observable<PaginatedUsersResponse> {
    let httpParams = new HttpParams()
      .set('page', params.page.toString())
      .set('pageSize', params.pageSize.toString())
      .set('sortField', params.sortField || 'createdAtUtc')
      .set('sortDirection', params.sortDirection || 'desc');

    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params.department && params.department !== 'all') {
      httpParams = httpParams.set('department', params.department);
    }
    if (params.jobTitle && params.jobTitle !== 'all') {
      httpParams = httpParams.set('jobTitle', params.jobTitle);
    }
    if (params.location && params.location !== 'all') {
      httpParams = httpParams.set('location', params.location);
    }
    if (params.isActive !== undefined) {
      httpParams = httpParams.set('isActive', params.isActive.toString());
    }
    if (params.isEmailVerified !== undefined) {
      httpParams = httpParams.set('isEmailVerified', params.isEmailVerified.toString());
    }
    if (params.roleId && params.roleId !== 'all' && params.roleId !== '') {
      httpParams = httpParams.set('roleId', params.roleId);
    }
    if (params.organizationId && params.organizationId !== 'all' && params.organizationId !== '') {
      httpParams = httpParams.set('organizationId', params.organizationId);
    }
    if (params.createdFrom) {
      httpParams = httpParams.set('createdFrom', params.createdFrom);
    }
    if (params.createdTo) {
      httpParams = httpParams.set('createdTo', params.createdTo);
    }

    return this.http.get<PaginatedUsersResponse>(this.apiUrl, { params: httpParams });
  }

  /**
   * Get user by ID
   */
  getUserById(id: string): Observable<User> {
    return this.http.get<User>(`${this.apiUrl}/${id}`);
  }

  /**
   * Create new user
   */
  create(user: CreateUserRequest): Observable<User> {
    return this.http.post<User>(this.apiUrl, user);
  }

  /**
   * Update existing user
   */
  update(id: string, user: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${this.apiUrl}/${id}`, user);
  }

  /**
   * Delete single user
   */
  delete(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }

  /**
   * Delete multiple users
   */
  deleteMultiple(ids: string[]): Observable<any> {
    return this.http.post(`${this.apiUrl}/bulk-delete`, { ids });
  }

  /**
   * Clone multiple users
   */
  cloneMultiple(ids: string[]): Observable<{ message: string; clonedUsers: any[] }> {
    return this.http.post<{ message: string; clonedUsers: any[] }>(`${this.apiUrl}/bulk-clone`, { ids });
  }

  /**
   * Set user active status
   */
  setActive(id: string, isActive: boolean): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}/active?isActive=${isActive}`, null);
  }

  /**
   * Get user statistics
   */
  getStatistics(): Observable<UserStatistics> {
    return this.http.get<UserStatistics>(`${this.apiUrl}/statistics`);
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
   * Import users from file
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
    department?: string;
    jobTitle?: string;
    location?: string;
    isActive?: boolean;
    isEmailVerified?: boolean;
    roleId?: string;
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
   * Generate password reset link
   */
  generatePasswordReset(id: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/password-reset`, null);
  }

  /**
   * Send email verification
   */
  sendEmailVerification(id: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/email-verification`, null);
  }

  /**
   * Resend invitation
   */
  resendInvitation(id: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/resend-invitation`, null);
  }

  /**
   * Unlock user account
   */
  unlockUser(id: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${id}/unlock`, null);
  }

  /**
   * Get dropdown options for filters and forms
   */
  getDropdownOptions(): Observable<DropdownOptions> {
    return this.http.get<DropdownOptions>(`${this.apiUrl}/dropdown-options`);
  }

  /**
   * Get location options
   */
  getLocationOptions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/dropdown-options/locations`);
  }

  /**
   * Get department options
   */
  getDepartmentOptions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/dropdown-options/departments`);
  }

  /**
   * Get position/job title options
   */
  getPositionOptions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/dropdown-options/positions`);
  }

}
