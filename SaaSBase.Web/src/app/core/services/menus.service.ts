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
  parentMenuId?: string;
  sortOrder: number;
  isActive: boolean;
  description?: string;
  badge?: string;
  badgeColor?: string;
  isSystemMenu: boolean;
  createdAtUtc: string;
  lastModifiedAtUtc?: string;
  createdBy?: string;
  showDropdown?: boolean;
  dropdownUp?: boolean;
}

export interface CreateMenuRequest {
  label: string;
  route: string;
  icon: string;
  section?: string;
  parentMenuId?: string;
  sortOrder: number;
  isActive: boolean;
  description?: string;
  badge?: string;
  badgeColor?: string;
  isSystemMenu?: boolean;
}

export interface UpdateMenuRequest {
  label: string;
  route: string;
  icon: string;
  section?: string;
  parentMenuId?: string;
  sortOrder: number;
  isActive: boolean;
  description?: string;
  badge?: string;
  badgeColor?: string;
}

export interface MenuDropdown {
  id: string;
  label: string;
  route: string;
  section?: string;
}

export interface MenuStatistics {
  total: number;
  active: number;
  inactive: number;
  systemMenus: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
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

@Injectable({
  providedIn: 'root'
})
export class MenusService {
  private readonly apiUrl = `${environment.apiBaseUrl}/api/${environment.apiVersion}/menus`;

  constructor(private http: HttpClient) {}

  getMenus(
    search?: string,
    section?: string,
    parentMenuId?: string,
    isActive?: boolean,
    createdFrom?: string,
    createdTo?: string,
    page: number = 1,
    pageSize: number = 20,
    sortField?: string,
    sortDirection: string = 'asc'
  ): Observable<PagedResult<Menu>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString())
      .set('sortDirection', sortDirection);

    if (search) params = params.set('search', search);
    if (section) params = params.set('section', section);
    // Only include parentMenuId if it's a valid non-empty string
    if (parentMenuId && parentMenuId.trim() !== '') params = params.set('parentMenuId', parentMenuId);
    if (isActive !== undefined) params = params.set('isActive', isActive.toString());
    if (createdFrom) params = params.set('createdFrom', createdFrom);
    if (createdTo) params = params.set('createdTo', createdTo);
    if (sortField) params = params.set('sortField', sortField);

    return this.http.get<PagedResult<Menu>>(this.apiUrl, { params });
  }

  getMenuById(id: string): Observable<Menu> {
    return this.http.get<Menu>(`${this.apiUrl}/${id}`);
  }

  create(menu: CreateMenuRequest): Observable<Menu> {
    return this.http.post<Menu>(this.apiUrl, menu);
  }

  update(id: string, menu: UpdateMenuRequest): Observable<Menu> {
    return this.http.put<Menu>(`${this.apiUrl}/${id}`, menu);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  bulkDelete(ids: string[]): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/bulk-delete`, { ids });
  }

  cloneMultiple(ids: string[]): Observable<{ message: string; clonedMenus: Menu[] }> {
    return this.http.post<{ message: string; clonedMenus: Menu[] }>(`${this.apiUrl}/bulk-clone`, { ids });
  }

  setActive(id: string, isActive: boolean): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}/active`, null, {
      params: { isActive: isActive.toString() }
    });
  }

  getStatistics(): Observable<MenuStatistics> {
    return this.http.get<MenuStatistics>(`${this.apiUrl}/statistics`);
  }

  getDropdownOptions(): Observable<MenuDropdown[]> {
    return this.http.get<MenuDropdown[]>(`${this.apiUrl}/dropdown`);
  }

  getSections(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/sections`);
  }

  getMenusBySection(section: string): Observable<Menu[]> {
    return this.http.get<Menu[]>(`${this.apiUrl}/by-section/${encodeURIComponent(section)}`);
  }

  getChildMenus(parentMenuId: string): Observable<Menu[]> {
    return this.http.get<Menu[]>(`${this.apiUrl}/by-parent/${parentMenuId}`);
  }

  getImportTemplate(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/template`, { responseType: 'blob' });
  }

  startImport(file: File): Observable<{ jobId: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ jobId: string }>(`${this.apiUrl}/import/async`, formData);
  }

  getImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.apiUrl}/import/jobs/${jobId}`);
  }

  getImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/import/error-report/${errorReportId}`, { responseType: 'blob' });
  }

  startExport(format: string | number, filters: any): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.apiUrl}/export/async`, {
      format: typeof format === 'number' ? format : parseInt(format.toString()),
      ...filters
    });
  }

  getExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.apiUrl}/export/jobs/${jobId}`);
  }

  downloadExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  getHistory(type?: string, page: number = 1, pageSize: number = 10): Observable<any> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (type) params = params.set('type', type);
    return this.http.get(`${this.apiUrl}/history`, { params });
  }
}

