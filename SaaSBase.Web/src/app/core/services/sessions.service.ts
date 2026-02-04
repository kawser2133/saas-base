import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface UserSessionDto {
  id: string;
  userId: string;
  userEmail?: string;
  sessionId: string;
  deviceId?: string;
  deviceName?: string;
  deviceType?: string;
  browserName?: string;
  browserVersion?: string;
  operatingSystem?: string;
  ipAddress?: string;
  userAgent?: string;
  location?: string;
  lastActivityAt: string;
  expiresAt: string;
  isActive: boolean;
  notes?: string;
  createdAtUtc: string;
  refreshToken?: string;
  organizationId?: string; // Organization ID
  organizationName?: string; // Organization Name (for System Admin view)
}

export interface PagedResult<T> {
  page: number;
  pageSize: number;
  totalCount: number;
  items: T[];
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
  startedAt?: string;
  completedAt?: string;
  expiresAt?: string;
}

@Injectable({ providedIn: 'root' })
export class SessionsService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;

  constructor(private http: HttpClient) {}

  getUserSessions(page = 1, pageSize = 10, search?: string, sortField?: string, sortDirection?: 'asc'|'desc'): Observable<PagedResult<UserSessionDto>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (sortField) params = params.set('sortField', sortField);
    if (sortDirection) params = params.set('sortDirection', sortDirection);
    return this.http.get<PagedResult<UserSessionDto>>(`${this.api}/sessions`, { params });
  }

  // Get current user's sessions only (for settings page)
  getMySessions(page = 1, pageSize = 10, search?: string, sortField?: string, sortDirection?: 'asc'|'desc'): Observable<PagedResult<UserSessionDto>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (sortField) params = params.set('sortField', sortField);
    if (sortDirection) params = params.set('sortDirection', sortDirection);
    return this.http.get<PagedResult<UserSessionDto>>(`${this.api}/sessions/my-sessions`, { params });
  }

  // Org-wide sessions (admin): omit userId
  getOrganizationSessions(page = 1, pageSize = 10, organizationId?: string, search?: string, sortField?: string, sortDirection?: 'asc'|'desc'): Observable<PagedResult<UserSessionDto>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (sortField) params = params.set('sortField', sortField);
    if (sortDirection) params = params.set('sortDirection', sortDirection);
    if (organizationId && organizationId !== 'all' && organizationId !== '') {
      params = params.set('organizationId', organizationId);
    }
    return this.http.get<PagedResult<UserSessionDto>>(`${this.api}/sessions`, { params });
  }

  exportSessions(search?: string, sortField?: string, sortDirection?: 'asc'|'desc') {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (sortField) params = params.set('sortField', sortField);
    if (sortDirection) params = params.set('sortDirection', sortDirection);
    // Legacy sync export removed; use async below
    return this.http.get(`${this.api}/sessions/export`, { params, responseType: 'blob' });
  }

  startExportAsync(params: { format: number; search?: string; sortField?: string; sortDirection?: string; selectedIds?: string[] }) {
    return this.http.post<{ jobId: string }>(`${this.api}/sessions/export/async`, params);
  }

  getExportJobStatus(jobId: string) {
    return this.http.get<ExportJobStatus>(`${this.api}/sessions/export/jobs/${jobId}`);
  }

  downloadExport(jobId: string) {
    return this.http.get(`${this.api}/sessions/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  getActiveSessions(userId: string): Observable<UserSessionDto[]> {
    const params = new HttpParams().set('userId', userId);
    return this.http.get<UserSessionDto[]>(`${this.api}/sessions/active`, { params });
  }

  revokeSession(sessionId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/sessions/${encodeURIComponent(sessionId)}`);
  }

  bulkRevokeSessions(sessionIds: string[]): Observable<{ message: string; revokedCount: number }> {
    return this.http.post<{ message: string; revokedCount: number }>(`${this.api}/sessions/bulk-revoke`, { sessionIds });
  }

  revokeAllUserSessions(userId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/sessions/user/${userId}`);
  }

  getHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (type) {
      params = params.set('type', type);
    }

    return this.http.get<any>(`${this.api}/sessions/history`, { params });
  }
}


