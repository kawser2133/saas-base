import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface MfaSettingsDto {
  isMfaEnabled: boolean;
  enabledMethods: string[];
  defaultMethod?: string;
  totpSetupQrCode?: string;
  phoneNumberMasked?: string;
  emailMasked?: string;
}

export interface UserMfaSettingsDto {
  id: string;
  userId: string;
  userEmail: string;
  mfaType: string;
  isActive: boolean;
  isDefault: boolean;
  lastUsedAt?: string;
  createdAtUtc: string;
  organizationId?: string;
  organizationName?: string;
}

export interface PagedResult<T> {
  page: number;
  pageSize: number;
  totalCount: number;
  items: T[];
}

@Injectable({ providedIn: 'root' })
export class MfaService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}/mfa`;

  constructor(private http: HttpClient) {}

  getSettings(userId: string): Observable<MfaSettingsDto> {
    return this.http.get<MfaSettingsDto>(`${this.api}/settings/${userId}`);
  }

  setup(userId: string, mfaType: string): Observable<any> {
    return this.http.post(`${this.api}/setup`, { userId, mfaType });
  }

  enable(userId: string, mfaType: string, code: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.api}/enable`, { userId, mfaType, code });
  }

  disable(userId: string, mfaType: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.api}/disable`, { userId, mfaType });
  }

  setDefault(userId: string, mfaType: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.api}/set-default`, { userId, mfaType });
  }

  sendCode(userId: string, mfaType: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.api}/send-code`, { userId, mfaType });
  }

  verify(userId: string, mfaType: string, code: string): Observable<{ isValid: boolean }> {
    return this.http.post<{ isValid: boolean }>(`${this.api}/verify`, { userId, mfaType, code });
  }

  generateBackupCodes(userId: string): Observable<{ codes: string[] }> {
    return this.http.post<{ codes: string[] }>(`${this.api}/backup-codes/generate`, { userId });
  }

  verifyBackupCode(userId: string, code: string): Observable<{ isValid: boolean }> {
    return this.http.post<{ isValid: boolean }>(`${this.api}/backup-codes/verify`, { userId, code });
  }

  getOrganizationMfaSettings(page = 1, pageSize = 10, search?: string, sortField?: string, sortDirection?: 'asc'|'desc', organizationId?: string): Observable<PagedResult<UserMfaSettingsDto>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (sortField) params = params.set('sortField', sortField);
    if (sortDirection) params = params.set('sortDirection', sortDirection);
    if (organizationId) params = params.set('organizationId', organizationId);
    return this.http.get<PagedResult<UserMfaSettingsDto>>(`${this.api}/organization`, { params });
  }

  startExportAsync(params: { format: number; search?: string; sortField?: string; sortDirection?: string; selectedIds?: string[] }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/export/async`, params);
  }

  getExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.api}/export/jobs/${jobId}`);
  }

  downloadExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.api}/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  getHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (type) {
      params = params.set('type', type);
    }

    return this.http.get<any>(`${this.api}/history`, { params });
  }
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
