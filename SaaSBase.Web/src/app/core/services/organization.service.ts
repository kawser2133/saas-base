import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface Organization {
  id: string;
  name: string;
  description?: string;
  website?: string;
  email?: string;
  phone?: string;
  address?: string;
  city?: string;
  state?: string;
  country?: string;
  postalCode?: string;
  logoUrl?: string;
  primaryColor?: string;
  secondaryColor?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Location {
  id: string;
  organizationId: string;
  name: string;
  description?: string;
  address?: string;
  city?: string;
  state?: string;
  country?: string;
  postalCode?: string;
  phone?: string;
  email?: string;
  managerName?: string;
  locationCode?: string;
  locationType?: string;
  isWarehouse?: boolean;
  isRetail?: boolean;
  isOffice?: boolean;
  timezone?: string;
  currency?: string;
  language?: string;
  latitude?: number;
  longitude?: number;
  parentLocationId?: string;
  level?: number;
  isActive: boolean;
  isDefault?: boolean;
  createdAt: string;
  updatedAt: string;
  createdAtUtc?: string;
  modifiedAtUtc?: string;
  createdBy?: string;
  modifiedBy?: string;
  createdById?: string;
  createdByName?: string;
  modifiedById?: string;
  modifiedByName?: string;
}

export interface BusinessSetting {
  id: string;
  organizationId: string;
  settingKey: string;
  settingValue: string;
  settingType?: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  createdAtUtc?: string;
  modifiedAtUtc?: string;
  createdBy?: string;
  modifiedBy?: string;
  createdById?: string;
  createdByName?: string;
  modifiedById?: string;
  modifiedByName?: string;
}

export interface Currency {
  id: string;
  organizationId: string;
  code: string;
  name: string;
  symbol: string;
  exchangeRate: number;
  decimalPlaces?: number;
  isDefault: boolean;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
  createdAtUtc?: string;
  modifiedAtUtc?: string;
  createdBy?: string;
  modifiedBy?: string;
  createdById?: string;
  createdByName?: string;
  modifiedById?: string;
  modifiedByName?: string;
}

export interface TaxRate {
  id: string;
  organizationId: string;
  name: string;
  rate: number;
  description?: string;
  taxType?: string;
  isActive: boolean;
  isDefault?: boolean;
  effectiveFrom?: string;
  effectiveTo?: string;
  createdAt?: string;
  updatedAt?: string;
  createdAtUtc?: string;
  modifiedAtUtc?: string;
  createdBy?: string;
  modifiedBy?: string;
  createdById?: string;
  createdByName?: string;
  modifiedById?: string;
  modifiedByName?: string;
}

export interface NotificationTemplate {
  id: string;
  organizationId: string;
  name: string;
  subject: string;
  body: string;
  templateType: string;
  description?: string;
  variables?: string;
  category?: string;
  isActive: boolean;
  isSystemTemplate?: boolean;
  createdAt?: string;
  updatedAt?: string;
  createdAtUtc?: string;
  modifiedAtUtc?: string;
  createdBy?: string;
  modifiedBy?: string;
  createdById?: string;
  createdByName?: string;
  modifiedById?: string;
  modifiedByName?: string;
}

export interface IntegrationSetting {
  id: string;
  organizationId: string;
  name?: string;
  description?: string;
  integrationType: string;
  provider?: string;
  settings?: any;
  configuration?: string;
  isActive: boolean;
  isEnabled?: boolean;
  lastSyncAt?: string;
  lastSyncStatus?: string;
  errorMessage?: string;
  createdAt?: string;
  updatedAt?: string;
  createdAtUtc?: string;
  modifiedAtUtc?: string;
  createdBy?: string;
  modifiedBy?: string;
  createdById?: string;
  createdByName?: string;
  modifiedById?: string;
  modifiedByName?: string;
}

// Import/Export interfaces (matching roles.service.ts pattern)
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

@Injectable({
  providedIn: 'root'
})
export class OrganizationService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;

  constructor(private http: HttpClient) { }

  // Organization CRUD
  getOrganizations(params?: { search?: string; isActive?: boolean; page?: number; pageSize?: number }): Observable<any> {
    const options: any = {};
    if (params) {
      options.params = {} as any;
      if (params.search) (options.params as any).search = params.search;
      if (typeof params.isActive === 'boolean') (options.params as any).isActive = params.isActive;
      if (params.page) (options.params as any).page = params.page;
      if (params.pageSize) (options.params as any).pageSize = params.pageSize;
    }
    return this.http.get(`${this.api}/organizations`, options);
  }

  getOrganization(id: string): Observable<Organization> {
    return this.http.get<Organization>(`${this.api}/organizations/${id}`).pipe(
      map(org => ({
        ...org,
        logoUrl: org.logoUrl ? `${environment.apiBaseUrl}/media/${org.logoUrl}` : org.logoUrl
      }))
    );
  }

  createOrganization(payload: Partial<Organization>): Observable<Organization> {
    return this.http.post<Organization>(`${this.api}/organizations`, payload);
  }

  updateOrganization(id: string, payload: Partial<Organization>): Observable<Organization> {
    return this.http.put<Organization>(`${this.api}/organizations/${id}`, payload).pipe(
      map(org => ({
        ...org,
        logoUrl: org.logoUrl ? `${environment.apiBaseUrl}/media/${org.logoUrl}` : org.logoUrl
      }))
    );
  }

  deleteOrganization(id: string): Observable<any> {
    return this.http.delete(`${this.api}/organizations/${id}`);
  }

  uploadLogo(id: string, file: File): Observable<any> {
    const formData = new FormData();
    formData.append('logo', file);
    
    return this.http.post<any>(`${this.api}/organizations/${id}/logo`, formData);
  }

  removeLogo(id: string): Observable<any> {
    return this.http.delete<any>(`${this.api}/organizations/${id}/logo`);
  }

  // Location Management
  getLocations(params?: { 
    search?: string; 
    isActive?: boolean; 
    country?: string;
    city?: string;
    createdFrom?: Date;
    createdTo?: Date;
    page?: number; 
    pageSize?: number;
    sortField?: string;
    sortDirection?: string;
    organizationId?: string;
  }): Observable<any> {
    const options: any = { params: {} };
    if (params) {
      if (params.search) options.params.search = params.search;
      if (typeof params.isActive === 'boolean') options.params.isActive = params.isActive;
      if (params.country) options.params.country = params.country;
      if (params.city) options.params.city = params.city;
      if (params.createdFrom) options.params.createdFrom = params.createdFrom.toISOString();
      if (params.createdTo) options.params.createdTo = params.createdTo.toISOString();
      if (params.page) options.params.page = params.page;
      if (params.pageSize) options.params.pageSize = params.pageSize;
      if (params.sortField) options.params.sortField = params.sortField;
      if (params.sortDirection) options.params.sortDirection = params.sortDirection;
      if (params.organizationId) options.params.organizationId = params.organizationId;
    }
    return this.http.get(`${this.api}/organizations/locations`, options);
  }

  getLocation(locationId: string): Observable<Location> {
    return this.http.get<Location>(`${this.api}/organizations/locations/${locationId}`);
  }

  createLocation(payload: Partial<Location>): Observable<Location> {
    return this.http.post<Location>(`${this.api}/organizations/locations`, payload);
  }

  updateLocation(locationId: string, payload: Partial<Location>): Observable<Location> {
    return this.http.put<Location>(`${this.api}/organizations/locations/${locationId}`, payload);
  }

  deleteLocation(locationId: string): Observable<any> {
    return this.http.delete(`${this.api}/organizations/locations/${locationId}`);
  }

  getLocationHierarchy(): Observable<any> {
    return this.http.get(`${this.api}/organizations/locations/hierarchy`);
  }

  // Location Import/Export/History (matching roles.service.ts pattern)
  getLocationTemplate(): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/locations/import/template`, { responseType: 'blob' });
  }

  startLocationExportAsync(params: {
    format: 1 | 2 | 3 | 4; // 1=Excel, 2=CSV, 3=PDF, 4=JSON
    search?: string;
    isActive?: boolean;
    country?: string;
    city?: string;
    createdFrom?: string;
    createdTo?: string;
    selectedIds?: string[];
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/locations/export/async`, params);
  }

  getLocationExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.api}/organizations/locations/export/jobs/${jobId}`);
  }

  downloadLocationExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/locations/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  startLocationImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/locations/import/async`, formData);
  }

  getLocationImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.api}/organizations/locations/import/jobs/${jobId}`);
  }

  getLocationImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/locations/import/error-report/${errorReportId}`, { responseType: 'blob' });
  }

  getLocationHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<ImportExportHistoryResponse>(`${this.api}/organizations/locations/history`, { params });
  }

  getLocationCountries(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/organizations/locations/filter-options/countries`);
  }

  getLocationCities(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/organizations/locations/filter-options/cities`);
  }

  cloneMultipleLocations(ids: string[]): Observable<{ items: Location[], message: string }> {
    return this.http.post<{ items: Location[], message: string }>(`${this.api}/organizations/locations/bulk-clone`, ids);
  }

  // Business Settings
  getBusinessSettings(params?: { 
    search?: string; 
    isActive?: boolean; 
    createdFrom?: Date;
    createdTo?: Date;
    page?: number; 
    pageSize?: number;
    sortField?: string;
    sortDirection?: string;
    organizationId?: string;
  }): Observable<any> {
    const options: any = { params: {} };
    if (params) {
      if (params.search) options.params.search = params.search;
      if (typeof params.isActive === 'boolean') options.params.isActive = params.isActive;
      if (params.createdFrom) options.params.createdFrom = params.createdFrom.toISOString();
      if (params.createdTo) options.params.createdTo = params.createdTo.toISOString();
      if (params.page) options.params.page = params.page;
      if (params.pageSize) options.params.pageSize = params.pageSize;
      if (params.sortField) options.params.sortField = params.sortField;
      if (params.sortDirection) options.params.sortDirection = params.sortDirection;
      if (params.organizationId) options.params.organizationId = params.organizationId;
    }
    return this.http.get(`${this.api}/organizations/business-settings`, options);
  }

  getBusinessSetting(settingId: string): Observable<BusinessSetting> {
    return this.http.get<BusinessSetting>(`${this.api}/organizations/business-settings/${settingId}`);
  }

  createBusinessSetting(payload: Partial<BusinessSetting>): Observable<BusinessSetting> {
    return this.http.post<BusinessSetting>(`${this.api}/organizations/business-settings`, payload);
  }

  updateBusinessSetting(settingId: string, payload: Partial<BusinessSetting>): Observable<BusinessSetting> {
    return this.http.put<BusinessSetting>(`${this.api}/organizations/business-settings/${settingId}`, payload);
  }

  deleteBusinessSetting(settingId: string): Observable<any> {
    return this.http.delete(`${this.api}/organizations/business-settings/${settingId}`);
  }

  // Business Settings Import/Export/History (matching roles.service.ts pattern)
  getBusinessSettingTemplate(): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/business-settings/import/template`, { responseType: 'blob' });
  }

  startBusinessSettingExportAsync(params: {
    format: 1 | 2 | 3 | 4; // 1=Excel, 2=CSV, 3=PDF, 4=JSON
    search?: string;
    isActive?: boolean;
    createdFrom?: string;
    createdTo?: string;
    selectedIds?: string[];
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/business-settings/export/async`, params);
  }

  getBusinessSettingExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.api}/organizations/business-settings/export/jobs/${jobId}`);
  }

  downloadBusinessSettingExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/business-settings/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  startBusinessSettingImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/business-settings/import/async`, formData);
  }

  getBusinessSettingImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.api}/organizations/business-settings/import/jobs/${jobId}`);
  }

  getBusinessSettingImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/business-settings/import/error-report/${errorReportId}`, { responseType: 'blob' });
  }

  getBusinessSettingHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<ImportExportHistoryResponse>(`${this.api}/organizations/business-settings/history`, { params });
  }

  cloneMultipleBusinessSettings(ids: string[]): Observable<{ items: BusinessSetting[], message: string }> {
    return this.http.post<{ items: BusinessSetting[], message: string }>(`${this.api}/organizations/business-settings/bulk-clone`, ids);
  }

  // Currency Management
  getCurrencies(params?: { 
    search?: string; 
    isActive?: boolean; 
    code?: string;
    createdFrom?: Date;
    createdTo?: Date;
    page?: number; 
    pageSize?: number;
    sortField?: string;
    sortDirection?: string;
    organizationId?: string;
  }): Observable<any> {
    const options: any = { params: {} };
    if (params) {
      if (params.search) options.params.search = params.search;
      if (typeof params.isActive === 'boolean') options.params.isActive = params.isActive;
      if (params.code) options.params.code = params.code;
      if (params.createdFrom) options.params.createdFrom = params.createdFrom.toISOString();
      if (params.createdTo) options.params.createdTo = params.createdTo.toISOString();
      if (params.page) options.params.page = params.page;
      if (params.pageSize) options.params.pageSize = params.pageSize;
      if (params.sortField) options.params.sortField = params.sortField;
      if (params.sortDirection) options.params.sortDirection = params.sortDirection;
      if (params.organizationId) options.params.organizationId = params.organizationId;
    }
    return this.http.get(`${this.api}/organizations/currencies`, options);
  }

  getCurrency(currencyId: string): Observable<Currency> {
    return this.http.get<Currency>(`${this.api}/organizations/currencies/${currencyId}`);
  }

  createCurrency(payload: Partial<Currency>): Observable<Currency> {
    return this.http.post<Currency>(`${this.api}/organizations/currencies`, payload);
  }

  updateCurrency(currencyId: string, payload: Partial<Currency>): Observable<Currency> {
    return this.http.put<Currency>(`${this.api}/organizations/currencies/${currencyId}`, payload);
  }

  deleteCurrency(currencyId: string): Observable<any> {
    return this.http.delete(`${this.api}/organizations/currencies/${currencyId}`);
  }

  // Deprecated: Use isDefault field in updateCurrency instead
  // setBaseCurrency(currencyId: string): Observable<any> {
  //   return this.http.put(`${this.api}/organizations/currencies/${currencyId}/set-base`, {});
  // }

  // Currency Import/Export/History (matching roles.service.ts pattern)
  getCurrencyTemplate(): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/currencies/import/template`, { responseType: 'blob' });
  }

  startCurrencyExportAsync(params: {
    format: 1 | 2 | 3 | 4; // 1=Excel, 2=CSV, 3=PDF, 4=JSON
    search?: string;
    isActive?: boolean;
    code?: string;
    createdFrom?: string;
    createdTo?: string;
    selectedIds?: string[];
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/currencies/export/async`, params);
  }

  getCurrencyExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.api}/organizations/currencies/export/jobs/${jobId}`);
  }

  downloadCurrencyExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/currencies/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  startCurrencyImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/currencies/import/async`, formData);
  }

  getCurrencyImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.api}/organizations/currencies/import/jobs/${jobId}`);
  }

  getCurrencyImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/currencies/import/error-report/${errorReportId}`, { responseType: 'blob' });
  }

  getCurrencyHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<ImportExportHistoryResponse>(`${this.api}/organizations/currencies/history`, { params });
  }

  getCurrencyCodes(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/organizations/currencies/filter-options/codes`);
  }

  cloneMultipleCurrencies(ids: string[]): Observable<{ items: Currency[], message: string }> {
    return this.http.post<{ items: Currency[], message: string }>(`${this.api}/organizations/currencies/bulk-clone`, ids);
  }

  // Tax Rate Management
  getTaxRates(params?: { 
    search?: string; 
    isActive?: boolean; 
    taxType?: string;
    createdFrom?: Date;
    createdTo?: Date;
    page?: number; 
    pageSize?: number;
    sortField?: string;
    sortDirection?: string;
    organizationId?: string;
  }): Observable<any> {
    const options: any = { params: {} };
    if (params) {
      if (params.search) options.params.search = params.search;
      if (typeof params.isActive === 'boolean') options.params.isActive = params.isActive;
      if (params.taxType) options.params.taxType = params.taxType;
      if (params.createdFrom) options.params.createdFrom = params.createdFrom.toISOString();
      if (params.createdTo) options.params.createdTo = params.createdTo.toISOString();
      if (params.page) options.params.page = params.page;
      if (params.pageSize) options.params.pageSize = params.pageSize;
      if (params.sortField) options.params.sortField = params.sortField;
      if (params.sortDirection) options.params.sortDirection = params.sortDirection;
      if (params.organizationId) options.params.organizationId = params.organizationId;
    }
    return this.http.get(`${this.api}/organizations/tax-rates`, options);
  }

  getTaxRate(taxRateId: string): Observable<TaxRate> {
    return this.http.get<TaxRate>(`${this.api}/organizations/tax-rates/${taxRateId}`);
  }

  createTaxRate(payload: Partial<TaxRate>): Observable<TaxRate> {
    return this.http.post<TaxRate>(`${this.api}/organizations/tax-rates`, payload);
  }

  updateTaxRate(taxRateId: string, payload: Partial<TaxRate>): Observable<TaxRate> {
    return this.http.put<TaxRate>(`${this.api}/organizations/tax-rates/${taxRateId}`, payload);
  }

  deleteTaxRate(taxRateId: string): Observable<any> {
    return this.http.delete(`${this.api}/organizations/tax-rates/${taxRateId}`);
  }

  // Tax Rate Import/Export/History (matching roles.service.ts pattern)
  getTaxRateTemplate(): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/tax-rates/import/template`, { responseType: 'blob' });
  }

  startTaxRateExportAsync(params: {
    format: 1 | 2 | 3 | 4; // 1=Excel, 2=CSV, 3=PDF, 4=JSON
    search?: string;
    isActive?: boolean;
    taxType?: string;
    createdFrom?: string;
    createdTo?: string;
    selectedIds?: string[];
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/tax-rates/export/async`, params);
  }

  getTaxRateExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.api}/organizations/tax-rates/export/jobs/${jobId}`);
  }

  downloadTaxRateExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/tax-rates/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  startTaxRateImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/tax-rates/import/async`, formData);
  }

  getTaxRateImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.api}/organizations/tax-rates/import/jobs/${jobId}`);
  }

  getTaxRateImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/tax-rates/import/error-report/${errorReportId}`, { responseType: 'blob' });
  }

  getTaxRateHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<ImportExportHistoryResponse>(`${this.api}/organizations/tax-rates/history`, { params });
  }

  getTaxTypes(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/organizations/tax-rates/filter-options/tax-types`);
  }

  cloneMultipleTaxRates(ids: string[]): Observable<{ items: TaxRate[], message: string }> {
    return this.http.post<{ items: TaxRate[], message: string }>(`${this.api}/organizations/tax-rates/bulk-clone`, ids);
  }

  // Notification Templates
  getNotificationTemplates(params?: { 
    search?: string; 
    isActive?: boolean; 
    category?: string;
    templateType?: string;
    createdFrom?: Date;
    createdTo?: Date;
    page?: number; 
    pageSize?: number;
    sortField?: string;
    sortDirection?: string;
    organizationId?: string;
  }): Observable<any> {
    const options: any = { params: {} };
    if (params) {
      if (params.search) options.params.search = params.search;
      if (typeof params.isActive === 'boolean') options.params.isActive = params.isActive;
      if (params.category) options.params.category = params.category;
      if (params.templateType) options.params.templateType = params.templateType;
      if (params.createdFrom) options.params.createdFrom = params.createdFrom.toISOString();
      if (params.createdTo) options.params.createdTo = params.createdTo.toISOString();
      if (params.page) options.params.page = params.page;
      if (params.pageSize) options.params.pageSize = params.pageSize;
      if (params.sortField) options.params.sortField = params.sortField;
      if (params.sortDirection) options.params.sortDirection = params.sortDirection;
      if (params.organizationId) options.params.organizationId = params.organizationId;
    }
    return this.http.get(`${this.api}/organizations/notification-templates`, options);
  }

  getNotificationTemplate(templateId: string): Observable<NotificationTemplate> {
    return this.http.get<NotificationTemplate>(`${this.api}/organizations/notification-templates/${templateId}`);
  }

  createNotificationTemplate(payload: Partial<NotificationTemplate>): Observable<NotificationTemplate> {
    return this.http.post<NotificationTemplate>(`${this.api}/organizations/notification-templates`, payload);
  }

  updateNotificationTemplate(templateId: string, payload: Partial<NotificationTemplate>): Observable<NotificationTemplate> {
    return this.http.put<NotificationTemplate>(`${this.api}/organizations/notification-templates/${templateId}`, payload);
  }

  deleteNotificationTemplate(templateId: string): Observable<any> {
    return this.http.delete(`${this.api}/organizations/notification-templates/${templateId}`);
  }

  // Notification Template Import/Export/History (matching roles.service.ts pattern)
  getNotificationTemplateTemplate(): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/notification-templates/import/template`, { responseType: 'blob' });
  }

  startNotificationTemplateExportAsync(params: {
    format: 1 | 2 | 3 | 4; // 1=Excel, 2=CSV, 3=PDF, 4=JSON
    search?: string;
    isActive?: boolean;
    category?: string;
    templateType?: string;
    createdFrom?: string;
    createdTo?: string;
    selectedIds?: string[];
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/notification-templates/export/async`, params);
  }

  getNotificationTemplateExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.api}/organizations/notification-templates/export/jobs/${jobId}`);
  }

  downloadNotificationTemplateExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/notification-templates/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  startNotificationTemplateImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/notification-templates/import/async`, formData);
  }

  getNotificationTemplateImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.api}/organizations/notification-templates/import/jobs/${jobId}`);
  }

  getNotificationTemplateImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/notification-templates/import/error-report/${errorReportId}`, { responseType: 'blob' });
  }

  getNotificationTemplateHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<ImportExportHistoryResponse>(`${this.api}/organizations/notification-templates/history`, { params });
  }

  getNotificationTemplateCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/organizations/notification-templates/filter-options/categories`);
  }

  cloneMultipleNotificationTemplates(ids: string[]): Observable<{ items: NotificationTemplate[], message: string }> {
    return this.http.post<{ items: NotificationTemplate[], message: string }>(`${this.api}/organizations/notification-templates/bulk-clone`, ids);
  }

  // Integration Settings
  getIntegrationSettings(params?: { 
    search?: string; 
    isActive?: boolean; 
    provider?: string;
    integrationType?: string;
    createdFrom?: Date;
    createdTo?: Date;
    page?: number; 
    pageSize?: number;
    sortField?: string;
    sortDirection?: string;
    organizationId?: string;
  }): Observable<any> {
    const options: any = { params: {} };
    if (params) {
      if (params.search) options.params.search = params.search;
      if (typeof params.isActive === 'boolean') options.params.isActive = params.isActive;
      if (params.provider) options.params.provider = params.provider;
      if (params.integrationType) options.params.integrationType = params.integrationType;
      if (params.createdFrom) options.params.createdFrom = params.createdFrom.toISOString();
      if (params.createdTo) options.params.createdTo = params.createdTo.toISOString();
      if (params.page) options.params.page = params.page;
      if (params.pageSize) options.params.pageSize = params.pageSize;
      if (params.sortField) options.params.sortField = params.sortField;
      if (params.sortDirection) options.params.sortDirection = params.sortDirection;
      if (params.organizationId) options.params.organizationId = params.organizationId;
    }
    return this.http.get(`${this.api}/organizations/integration-settings`, options);
  }

  getIntegrationSetting(settingId: string): Observable<IntegrationSetting> {
    return this.http.get<IntegrationSetting>(`${this.api}/organizations/integration-settings/${settingId}`);
  }

  createIntegrationSetting(payload: Partial<IntegrationSetting>): Observable<IntegrationSetting> {
    return this.http.post<IntegrationSetting>(`${this.api}/organizations/integration-settings`, payload);
  }

  updateIntegrationSetting(settingId: string, payload: Partial<IntegrationSetting>): Observable<IntegrationSetting> {
    return this.http.put<IntegrationSetting>(`${this.api}/organizations/integration-settings/${settingId}`, payload);
  }

  deleteIntegrationSetting(settingId: string): Observable<any> {
    return this.http.delete(`${this.api}/organizations/integration-settings/${settingId}`);
  }

  // Integration Setting Import/Export/History (matching roles.service.ts pattern)
  getIntegrationSettingTemplate(): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/integration-settings/import/template`, { responseType: 'blob' });
  }

  startIntegrationSettingExportAsync(params: {
    format: 1 | 2 | 3 | 4; // 1=Excel, 2=CSV, 3=PDF, 4=JSON
    search?: string;
    isActive?: boolean;
    provider?: string;
    createdFrom?: string;
    createdTo?: string;
    selectedIds?: string[];
  }): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/integration-settings/export/async`, params);
  }

  getIntegrationSettingExportJobStatus(jobId: string): Observable<ExportJobStatus> {
    return this.http.get<ExportJobStatus>(`${this.api}/organizations/integration-settings/export/jobs/${jobId}`);
  }

  downloadIntegrationSettingExport(jobId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/integration-settings/export/jobs/${jobId}/download`, { responseType: 'blob' });
  }

  startIntegrationSettingImportAsync(formData: FormData): Observable<{ jobId: string }> {
    return this.http.post<{ jobId: string }>(`${this.api}/organizations/integration-settings/import/async`, formData);
  }

  getIntegrationSettingImportJobStatus(jobId: string): Observable<ImportJobStatus> {
    return this.http.get<ImportJobStatus>(`${this.api}/organizations/integration-settings/import/jobs/${jobId}`);
  }

  getIntegrationSettingImportErrorReport(errorReportId: string): Observable<Blob> {
    return this.http.get(`${this.api}/organizations/integration-settings/import/error-report/${errorReportId}`, { responseType: 'blob' });
  }

  getIntegrationSettingHistory(type?: 'import' | 'export', page: number = 1, pageSize: number = 10): Observable<ImportExportHistoryResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (type) {
      params = params.set('type', type);
    }
    return this.http.get<ImportExportHistoryResponse>(`${this.api}/organizations/integration-settings/history`, { params });
  }

  getIntegrationProviders(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/organizations/integration-settings/filter-options/providers`);
  }

  cloneMultipleIntegrationSettings(ids: string[]): Observable<{ items: IntegrationSetting[], message: string }> {
    return this.http.post<{ items: IntegrationSetting[], message: string }>(`${this.api}/organizations/integration-settings/bulk-clone`, ids);
  }

  // Organization Analytics
  getOrganizationAnalytics(organizationId: string): Observable<any> {
    return this.http.get(`${this.api}/organizations/${organizationId}/analytics`);
  }

  getOrganizationDashboard(organizationId: string): Observable<any> {
    return this.http.get(`${this.api}/organizations/${organizationId}/dashboard`);
  }

  /**
   * Get location dropdown options
   */
  getLocationDropdownOptions(isActive?: boolean): Observable<LocationDropdownOption[]> {
    let params = new HttpParams();
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    return this.http.get<LocationDropdownOption[]>(`${this.api}/locations/dropdown-options`, { params });
  }
}

export interface LocationDropdownOption {
  id: string;
  name: string;
  isActive: boolean;
}
