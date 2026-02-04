import { Component, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { SessionsService, UserSessionDto } from '../../../core/services/sessions.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { NotificationService } from '../../../shared/services/notification.service';
import { AuthorizationService } from '../../../core/services/authorization.service';
import { UserContextService } from '../../../core/services/user-context.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import { AuthService } from '../../../core/services/auth.service';
import { HttpClient } from '@angular/common/http';
import { clearAuthStorage, decodeJwt, getAccessToken } from '../../../core/auth/auth.utils';
import { environment } from '../../../../environments/environment';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';

@Component({
  selector: 'app-sessions',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NotificationContainerComponent, BreadcrumbComponent, HasPermissionDirective],
  templateUrl: './sessions.component.html',
  styleUrls: ['./sessions.component.scss']
})
export class SessionsComponent implements OnInit {
  // Utility references
  Math = Math;

  items: UserSessionDto[] = [];
  page = 1;
  pageSize = 10;
  totalCount = 0;
  isLoading = false;
  canViewAll = false;
  searchQuery = '';
  sortField: string | null = null;
  sortDirection: 'asc' | 'desc' = 'desc';
  exportMenuOpen = false;
  selectedItems = new Set<string>();
  
  // Organization filtering for System Admin
  organizations: Array<{ id: string; name: string }> = [];
  isSystemAdmin = false;
  showExportDialog = false;
  isExporting = false;
  exportJobId: string | null = null;
  importJobId: string | null = null;
  exportProgress = 0;
  exportStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed' | null = null;
  exportFormat: string | null = null;
  isMobile = false;
  showHistoryDialog = false;
  history: any[] = [];
  historyPage = 1;
  historyPageSize = 10;
  historyTotalCount = 0;
  isLoadingHistory = false;
  historyType: 'import' | 'export' | undefined = undefined;

  // Common dropdown pattern for organization filter
  dropdownStates: { [key: string]: any } = {};
  filterFormData: any = { organizationId: '' };
  filters: any = { organizationId: '' };

  // Permission flags
  canDelete = false;
  canRead = false;

  constructor(
    private sessionsService: SessionsService,
    private notifications: NotificationService,
    private authz: AuthorizationService,
    private userContextService: UserContextService,
    private commonUtility: CommonUtilityService,
    private router: Router,
    private authService: AuthService,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    this.isMobile = this.commonUtility.isMobile();
    
    // Check if user is System Admin
    this.isSystemAdmin = this.userContextService.isSystemAdmin();
    
    // Load permission flags
    this.canRead = this.authz.hasPermission('Sessions.Read');
    this.canDelete = this.authz.hasPermission('Sessions.Delete');
    
    // Load organizations if System Admin
    if (this.isSystemAdmin) {
      this.loadOrganizations();
      this.initializeDropdownStates();
    }
    
    this.loadData();
  }
  
  // Load organizations for System Admin
  loadOrganizations(): void {
    if (!this.isSystemAdmin) return;
    
    const api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;
    this.http.get<Array<{ id: string; name: string }>>(`${api}/system/organizations`).subscribe({
      next: (orgs) => {
        this.organizations = orgs || [];
        // Initialize dropdown state after organizations are loaded
        if (this.isSystemAdmin) {
          this.initializeDropdownState('filter-organizationId');
        }
      },
      error: (error) => {
        console.error('Error loading organizations:', error);
        this.organizations = [];
      }
    });
  }

  initializeDropdownStates(): void {
    if (this.isSystemAdmin) {
      this.initializeDropdownState('filter-organizationId');
    }
  }

  initializeDropdownState(fieldName: string): void {
    if (!this.dropdownStates[fieldName]) {
      const options = this.getOptionsForField(fieldName);
      this.dropdownStates[fieldName] = this.commonUtility.initializeDropdownState(fieldName, options);
    }
  }

  getOptionsForField(fieldName: string): any[] {
    switch (fieldName) {
      case 'filter-organizationId': return this.organizations;
      default: return [];
    }
  }

  @HostListener('window:resize', ['$event'])
  onResize(): void {
    this.isMobile = this.commonUtility.isMobile();
  }

  onMainPageClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.form-group') &&
        !target.closest('.searchable-dropdown') &&
        !target.closest('.dropdown-toggle') &&
        !target.closest('.dropdown-menu') &&
        !target.closest('.action-dropdown')) {
      this.exportMenuOpen = false;
      this.closeAllSearchableDropdowns();
    }
  }

  loadData(): void {
    this.isLoading = true;
    // Backend handles permission - always call the same endpoint
    const orgId = this.isSystemAdmin && this.filterFormData.organizationId ? this.filterFormData.organizationId : undefined;
    this.sessionsService.getOrganizationSessions(this.page, this.pageSize, orgId, this.searchQuery, this.sortField || undefined, this.sortDirection).subscribe({
      next: res => {
        this.items = res.items?.map((item: any) => ({
          ...item,
          id: item.id || item.Id,
          organizationId: item.organizationId || item.OrganizationId,
          organizationName: item.organizationName || item.OrganizationName
        })) || [];
        this.totalCount = res.totalCount || 0;
        this.isLoading = false;
        // Clear selections when data changes
        this.selectedItems.clear();
      },
      error: () => {
        this.notifications.error('Error', 'Failed to load sessions');
        this.isLoading = false;
      }
    });
  }
  
  onOrganizationFilterChange(): void {
    this.page = 1;
    this.loadData();
  }

  onSearchChange(): void {
    this.page = 1;
    this.loadData();
  }

  onPageSizeChange(newSize: number): void {
    this.pageSize = newSize;
    this.page = 1;
    this.loadData();
  }

  onSort(field: string): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.loadData();
  }
  
  // Common dropdown pattern methods
  toggleSearchableDropdown(fieldName: string): void {
    this.initializeDropdownState(fieldName);
    if (this.dropdownStates[fieldName].isOpen) {
      this.dropdownStates[fieldName].isOpen = false;
      this.dropdownStates[fieldName].searchTerm = '';
    } else {
      // Close all other dropdowns first
      Object.keys(this.dropdownStates).forEach(key => {
        if (key !== fieldName) {
          this.dropdownStates[key].isOpen = false;
        }
      });
      this.dropdownStates[fieldName].isOpen = true;
      this.filterOptions(fieldName);
    }
  }

  filterOptions(fieldName: string): void {
    if (!this.dropdownStates[fieldName]) return;
    const searchTerm = this.dropdownStates[fieldName].searchTerm.toLowerCase();
    const options = this.getOptionsForField(fieldName);
    if (!searchTerm) {
      this.dropdownStates[fieldName].filteredOptions = options;
    } else {
      this.dropdownStates[fieldName].filteredOptions = options.filter((option: any) => {
        if (typeof option === 'object' && option.name) {
          return option.name.toLowerCase().includes(searchTerm);
        }
        return String(option).toLowerCase().includes(searchTerm);
      });
    }
  }

  onSearchInput(fieldName: string, event: Event): void {
    const target = event.target as HTMLInputElement;
    this.dropdownStates[fieldName].searchTerm = target.value;
    this.filterOptions(fieldName);
  }

  selectFilterOption(fieldName: string, option: string | any): void {
    if (fieldName === 'organizationId' && typeof option === 'object' && option.id) {
      option = option.id;
    }
    this.commonUtility.selectFilterOption(this.filterFormData, this.filters, fieldName, option as string);
    this.closeAllSearchableDropdowns();
    this.onOrganizationFilterChange();
  }

  clearDropdownValue(fieldName: string, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    if (fieldName.startsWith('filter-')) {
      const actualField = fieldName.replace('filter-', '');
      this.filterFormData[actualField] = '';
      this.filters[actualField] = '';
    } else {
      (this.filterFormData as any)[fieldName] = '';
    }
    this.closeAllSearchableDropdowns();
    this.onOrganizationFilterChange();
  }

  hasDropdownValue(fieldName: string): boolean {
    if (fieldName.startsWith('filter-')) {
      const actualField = fieldName.replace('filter-', '');
      return !!this.filterFormData[actualField];
    }
    return !!(this.filterFormData as any)[fieldName];
  }

  getFilterDisplayValue(fieldName: string): string {
    if (fieldName === 'organizationId' && this.filterFormData.organizationId) {
      const org = this.organizations.find(o => o.id === this.filterFormData.organizationId);
      return org ? org.name : '';
    }
    return this.commonUtility.getFilterDisplayValue(this.filterFormData, fieldName);
  }

  closeAllSearchableDropdowns(): void {
    Object.keys(this.dropdownStates).forEach((key: string) => {
      this.dropdownStates[key].isOpen = false;
    });
  }

  onExportFormat(fmt: 'excel' | 'csv' | 'pdf' | 'json'): void {
    const formatMap: { [key: string]: 1 | 2 | 3 | 4 } = {
      'excel': 1,
      'csv': 2,
      'pdf': 3,
      'json': 4
    };

    const params: any = {
      format: formatMap[fmt],
      search: this.searchQuery || undefined,
      sortField: this.sortField || undefined,
      sortDirection: this.sortDirection || undefined
    };

    // Add organizationId filter for System Admin
    if (this.isSystemAdmin && this.filterFormData.organizationId) {
      params.organizationId = this.filterFormData.organizationId;
    }

    // Add selected IDs if any are selected
    if (this.selectedItems.size > 0) {
      params.selectedIds = Array.from(this.selectedItems);
    }

    this.isExporting = true;
    this.exportProgress = 0;
    this.exportStatus = 'Pending';
    this.showExportDialog = true;

    this.sessionsService.startExportAsync(params).subscribe({
      next: (response) => {
        this.exportJobId = response.jobId;
        this.notifications.info('Export Started', 'Your export is processing. You can continue working.');
        this.pollExportJobStatus();
      },
      error: (error) => {
        console.error('Export start error:', error);
        this.notifications.error('Error', 'Failed to start export job.');
        this.isExporting = false;
        this.exportJobId = null;
        this.showExportDialog = false;
      }
    });
  }

  private pollExportJobStatus(): void {
    if (!this.exportJobId) return;

    const intervalId = setInterval(() => {
      if (!this.exportJobId) { clearInterval(intervalId); return; }

      this.sessionsService.getExportJobStatus(this.exportJobId).subscribe({
        next: (status) => {
          this.exportStatus = status.status;
          this.exportProgress = status.progressPercent;
          this.exportFormat = status.format;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isExporting = false;

            if (status.status === 'Completed') {
              if (status.totalRows > 0) {
                this.notifications.success('Export Completed', `Exported ${status.totalRows} records successfully!`);
                if (this.exportJobId) {
                  this.downloadCompletedExport(this.exportJobId, status.format);
                }
              } else {
                this.notifications.warning('Export Completed', 'No data available for export with current filters.');
              }
            } else {
              let errorMessage = status.message || 'Export failed.';
              if (status.totalRows === 0) {
                errorMessage = 'No data available for export with current filters.';
              }
              this.notifications.error('Export Failed', errorMessage);
            }

            this.exportJobId = null;
            this.exportFormat = null;
            this.showExportDialog = false;
          }
        },
        error: () => {
          clearInterval(intervalId);
          this.isExporting = false;
          this.exportJobId = null;
          this.exportFormat = null;
          this.showExportDialog = false;
          this.notifications.error('Error', 'Export status check failed.');
        }
      });
    }, 3000);
  }

  closeExportDialog(): void {
    this.showExportDialog = false;
    if (this.exportStatus !== 'Processing' && this.exportStatus !== 'Pending') {
      this.exportJobId = null;
      this.exportStatus = null;
      this.exportProgress = 0;
    }
  }

  private downloadCompletedExport(jobId: string, format: string): void {
    this.sessionsService.downloadExport(jobId).subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const extension = format.toLowerCase() === 'excel' ? 'xlsx' : format.toLowerCase();
        a.download = `sessions_export_${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.${extension}`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => this.notifications.error('Export', 'Download failed')
    });
  }

  toggleExportMenu(): void {
    this.exportMenuOpen = !this.exportMenuOpen;
  }

  private getCurrentUserId(): string | null {
    const token = getAccessToken();
    if (!token) return null;
    const decoded = decodeJwt(token);
    return decoded?.sub || decoded?.userId || decoded?.nameid || localStorage.getItem('userId');
  }

  private getCurrentSessionId(): string | null {
    const token = getAccessToken();
    if (!token) return null;
    const decoded = decodeJwt(token);
    return decoded?.sessionId || decoded?.sid || null;
  }

  private logout(): void {
    // Call logout API to mark session as inactive in backend
    this.authService.logout().subscribe({
      next: () => {
        clearAuthStorage();
        this.router.navigate(['/login'], { replaceUrl: true });
      },
      error: () => {
        // Even if API call fails, clear local storage and navigate to login
        clearAuthStorage();
        this.router.navigate(['/login'], { replaceUrl: true });
      }
    });
  }

  onRevoke(session: UserSessionDto): void {
    const currentUserId = this.getCurrentUserId();
    const currentSessionId = this.getCurrentSessionId();
    const isCurrentUser = currentUserId && session.userId === currentUserId;
    const isCurrentSession = currentSessionId && session.sessionId === currentSessionId;

    this.sessionsService.revokeSession(session.sessionId).subscribe({
      next: () => {
        this.notifications.success('Success', 'Session revoked');
        
        // If current user's current session is revoked, logout
        if (isCurrentUser && isCurrentSession) {
          this.notifications.warning('Session Revoked', 'Your session has been revoked. Please login again.');
          setTimeout(() => this.logout(), 1000);
        } else {
          this.loadData();
        }
      },
      error: () => this.notifications.error('Error', 'Failed to revoke session')
    });
  }

  onRevokeAllOther(session: UserSessionDto): void {
    // Use userId from the session row, not from localStorage
    if (!session.userId) {
      this.notifications.error('Error', 'User ID not found in session');
      return;
    }

    const currentUserId = this.getCurrentUserId();
    const isCurrentUser = currentUserId && session.userId === currentUserId;

    this.sessionsService.revokeAllUserSessions(session.userId).subscribe({
      next: () => {
        // If current user's sessions are revoked, logout
        if (isCurrentUser) {
          this.notifications.warning('Sessions Revoked', 'All your sessions have been revoked. Please login again.');
          setTimeout(() => this.logout(), 1000);
        } else {
          this.notifications.success('Success', 'All sessions revoked for user');
          this.loadData();
        }
      },
      error: () => this.notifications.error('Error', 'Failed to revoke all sessions')
    });
  }

  onPageChange(page: number): void {
    if (page < 1) return;
    this.page = page;
    this.loadData();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil((this.totalCount || 0) / (this.pageSize || 10)));
  }

  openHistoryDialog(): void {
    this.showHistoryDialog = true;
    this.loadHistory();
  }

  closeHistoryDialog(): void {
    this.showHistoryDialog = false;
  }

  loadHistory(): void {
    this.isLoadingHistory = true;
    this.sessionsService.getHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
      next: (response) => {
        this.history = response.items || [];
        this.historyTotalCount = response.totalCount || 0;
        this.isLoadingHistory = false;
      },
      error: () => {
        this.notifications.error('Error', 'Failed to load history');
        this.isLoadingHistory = false;
      }
    });
  }

  filterHistory(type: 'all' | 'import' | 'export'): void {
    this.historyType = type === 'all' ? undefined : type;
    this.historyPage = 1;
    this.loadHistory();
  }

  isCurrentJob(historyItem: any): boolean {
    return (historyItem.operationType === 'Import' && historyItem.id === this.importJobId) ||
           (historyItem.operationType === 'Export' && historyItem.id === this.exportJobId);
  }

  getStrategyDisplayName(strategy: string): string {
    const strategies: { [key: string]: string } = {
      'Skip': 'Skip Duplicates',
      'Update': 'Update Existing',
      'CreateNew': 'Create New'
    };
    return strategies[strategy] || strategy;
  }

  downloadErrorReportForHistory(historyId: string): void {
    const history = this.history.find(h => h.id === historyId);
    if (!history || !history.errorReportId) {
      this.notifications.warning('Warning', 'No error report available for this import');
      return;
    }

    // Note: Sessions service may not have getImportErrorReport method
    // This is a placeholder - adjust based on actual service method
    this.notifications.info('Info', 'Error report download not yet implemented for sessions');
  }

  goToHistoryPage(page: number): void {
    if (page < 1 || page > this.getHistoryTotalPages()) return;
    this.historyPage = page;
    this.loadHistory();
  }

  getHistoryTotalPages(): number {
    return Math.max(1, Math.ceil((this.historyTotalCount || 0) / this.historyPageSize));
  }

  downloadExportFromHistory(historyId: string): void {
    const historyItem = this.history.find(h => h.id === historyId);
    if (historyItem?.jobId) {
      this.sessionsService.downloadExport(historyItem.jobId).subscribe({
        next: (blob: Blob) => {
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = historyItem.fileName || 'sessions_export.xlsx';
          a.click();
          window.URL.revokeObjectURL(url);
        },
        error: () => this.notifications.error('Error', 'Download failed')
      });
    }
  }

  formatDateTime(date: string): string {
    if (!date) return '-';
    return new Date(date).toLocaleString();
  }

  formatFileSize(bytes: number): string {
    if (!bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  }

  getHistoryStatusClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'completed': return 'status-badge status-success';
      case 'processing': return 'status-badge status-info';
      case 'failed': return 'status-badge status-danger';
      case 'pending': return 'status-badge status-warning';
      default: return 'status-badge';
    }
  }

  // Checkbox selection methods
  toggleItemSelection(sessionId: string): void {
    this.commonUtility.toggleItemSelection(sessionId, this.selectedItems);
  }

  isAllSelected(): boolean {
    if (this.items.length === 0) return false;
    // Check if all current page items are selected (using sessionId)
    return this.items.every(item => this.selectedItems.has(item.sessionId));
  }

  masterToggle(): void {
    if (this.isAllSelected()) {
      // Deselect all current page items
      this.items.forEach(item => this.selectedItems.delete(item.sessionId));
    } else {
      // Select all current page items
      this.items.forEach(item => this.selectedItems.add(item.sessionId));
    }
  }

  // Bulk revoke selected sessions
  bulkRevokeSessions(): void {
    if (this.selectedItems.size === 0) {
      this.notifications.warning('Warning', 'No sessions selected');
      return;
    }

    const sessionIds = Array.from(this.selectedItems);
    const currentSessionId = this.getCurrentSessionId();
    const isCurrentSessionSelected = currentSessionId && sessionIds.includes(currentSessionId);

    this.sessionsService.bulkRevokeSessions(sessionIds).subscribe({
      next: (response) => {
        this.selectedItems.clear();
        if (isCurrentSessionSelected) {
          this.notifications.warning('Session Revoked', 'Your session has been revoked. Please login again.');
          setTimeout(() => this.logout(), 1000);
        } else {
          this.notifications.success('Success', response.message || `${response.revokedCount} session${response.revokedCount > 1 ? 's' : ''} revoked successfully`);
          this.loadData();
        }
      },
      error: (error) => {
        this.selectedItems.clear();
        console.error('Bulk revoke error:', error);
        this.notifications.error('Error', error.error?.message || 'Failed to revoke sessions');
        this.loadData();
      }
    });
  }
}


