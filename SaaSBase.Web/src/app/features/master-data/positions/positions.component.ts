import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PositionsService, Position, CreatePositionRequest, UpdatePositionRequest, PositionStatistics, ImportJobStatus, ImportExportHistory, ExportJobStatus } from '../../../core/services/positions.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { AuthorizationService } from '../../../core/services/authorization.service';
import { UserContextService } from '../../../core/services/user-context.service';
import { HttpClient } from '@angular/common/http';
import { Subscription, timer } from 'rxjs';
import { environment } from '../../../../environments/environment';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

@Component({
  selector: 'app-positions',
  standalone: true,
  imports: [CommonModule, FormsModule, NotificationContainerComponent, BreadcrumbComponent, HasPermissionDirective],
  templateUrl: './positions.component.html',
  styleUrls: ['./positions.component.scss']
})
export class PositionsComponent implements OnInit, OnDestroy {
  // Utility references
  Math = Math;
  document = document;

  // Subscriptions
  private subscriptions = new Subscription();

  // Data properties
  items: Position[] = [];
  filteredItems: Position[] = [];
  paginatedData: Position[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: CreatePositionRequest & UpdatePositionRequest & { id?: string } = {
    name: '',
    description: '',
    code: '',
    level: '',
    departmentId: '',
    departmentName: '',
    isActive: true,
    sortOrder: 0
  };
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: Position | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '', organizationId: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', status: '', organizationId: '' };
  showFilters = false;
  searchQuery = '';
  
  // Organization filtering for System Admin
  organizations: Array<{ id: string; name: string }> = [];
  isSystemAdmin = false;

  // Pagination
  currentPage = 1;
  itemsPerPage = 10;
  totalItems = 0;
  totalPages = 1;
  itemsPerPageFormData: number = 10;

  // Sorting
  sortField = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Dropdowns
  dropdownStates: { [key: string]: any } = {};
  exportDropdownOpen = false;
  
  // Dropdown options from API
  levels: string[] = [];
  departments: Array<{id: string, name: string}> = [];

  // Import properties
  selectedFile: File | null = null;
  importErrors: string[] = [];
  isImporting = false;
  errorReportId: string | null = null;
  importJobId: string | null = null;
  importProgress: number = 0;
  importStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed' | null = null;

  // Unified history properties
  history: ImportExportHistory[] = [];
  historyTotalCount = 0;
  historyPage = 1;
  historyPageSize = 10;
  historyType: 'import' | 'export' | undefined = undefined;
  isLoadingHistory = false;

  // Export job tracking properties
  exportJobId: string | null = null;
  exportProgress: number = 0;
  exportStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed' | null = null;
  exportFormat: string | null = null;
  isExporting = false;
  showExportDialog = false;

  // Loading states
  isLoading = false;
  isSubmitting = false;
  isMobile = false;

  // Error handling
  errors: { [key: string]: string } = {};

  // Statistics
  statistics: PositionStatistics = {
    total: 0,
    active: 0,
    inactive: 0
  };

  // Available options
  statusOptions: ('active' | 'inactive')[] = ['active', 'inactive'];

  // Search timeout for debouncing
  private searchTimeout: any;

  // Permission flags
  canCreate = false;
  canUpdate = false;
  canDelete = false;
  canExport = false;
  canImport = false;

  constructor(
    private notificationService: NotificationService,
    private positionsService: PositionsService,
    private commonUtility: CommonUtilityService,
    private authorizationService: AuthorizationService,
    private userContextService: UserContextService,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeDropdownStates();
    
    // Check if user is System Admin
    this.isSystemAdmin = this.userContextService.isSystemAdmin();
    
    // Load organizations if System Admin
    if (this.isSystemAdmin) {
      this.loadOrganizations();
    }
    
    // Load critical data first
    this.loadPermissions();
    this.loadData();
    
    // Load non-critical data with small delays
    const statsTimer = timer(100).subscribe(() => {
      this.loadStatistics();
    });
    this.subscriptions.add(statsTimer);
    
    const dropdownTimer = timer(200).subscribe(() => {
      this.loadDropdownOptions();
    });
    this.subscriptions.add(dropdownTimer);
  }

  loadPermissions(): void {
    const userId = localStorage.getItem('userId');
    if (userId) {
      const sub = this.authorizationService.loadUserPermissionCodes(userId).subscribe({
        next: () => {
          this.canCreate = this.authorizationService.hasPermission('Positions.Create');
          this.canUpdate = this.authorizationService.hasPermission('Positions.Update');
          this.canDelete = this.authorizationService.hasPermission('Positions.Delete');
          this.canExport = this.authorizationService.hasPermission('Positions.Export');
          this.canImport = this.authorizationService.hasPermission('Positions.Import');
        },
        error: (error) => {
          console.error('Error loading permissions:', error);
        }
      });
      this.subscriptions.add(sub);
    }
  }

  @HostListener('window:resize', ['$event'])
  onResize(): void {
    this.checkScreenSize();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;

    if (target.closest('.header-buttons') || target.closest('.modal-overlay')) {
      return;
    }

    if (!target.closest('.searchable-dropdown')) {
      this.closeAllSearchableDropdowns();
    }

    if (!target.closest('.dropdown-toggle') && !target.closest('.dropdown-menu')) {
      this.closeAllDropdowns();
      this.exportDropdownOpen = false;
    }
  }

  private checkScreenSize(): void {
    this.isMobile = this.commonUtility.isMobile();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  initializeDropdownStates(): void {
    const fields = ['level', 'departmentId', 'isActive', 'filter-status', 'items-per-page'];
    if (this.isSystemAdmin) {
      fields.push('filter-organizationId');
    }
    fields.forEach(field => {
      this.initializeDropdownState(field);
    });
  }
  
  // Load organizations for System Admin
  loadOrganizations(): void {
    if (!this.isSystemAdmin) return;
    
    const api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;
    this.http.get<Array<{ id: string; name: string }>>(`${api}/system/organizations`).subscribe({
      next: (orgs) => {
        this.organizations = orgs || [];
      },
      error: (error) => {
        console.error('Error loading organizations:', error);
        this.organizations = [];
      }
    });
  }

  initializeDropdownState(fieldName: string): void {
    if (!this.dropdownStates[fieldName]) {
      const options = this.getOptionsForField(fieldName);
      this.dropdownStates[fieldName] = this.commonUtility.initializeDropdownState(fieldName, options);
    }
  }

  getOptionsForField(fieldName: string): any[] {
    switch (fieldName) {
      case 'level': return this.levels;
      case 'departmentId': 
        return this.departments.map(d => `${d.id}|${d.name}`);
      case 'isActive': return ['true', 'false'];
      case 'filter-status': return ['', 'true', 'false'];
      case 'filter-organizationId': return this.organizations;
      case 'items-per-page': return ['10', '25', '50', '100'];
      default: return [];
    }
  }

  // Data loading
  loadData(): void {
    this.isLoading = true;

    const params: any = {
      page: this.currentPage,
      pageSize: this.itemsPerPage,
      search: this.searchQuery,
      sortField: this.sortField,
      sortDirection: this.sortDirection,
      isActive: this.filters.isActive,
      createdFrom: this.filters.createdFrom || undefined,
      createdTo: this.filters.createdTo || undefined
    };

    // Add organizationId filter for System Admin
    if (this.isSystemAdmin && this.filters.organizationId) {
      params.organizationId = this.filters.organizationId;
    }

    const sub = this.positionsService.getData(params).subscribe({
      next: (response: any) => {
        this.items = response.items?.map((item: any) => ({
          ...item,
          id: item.id || item.Id,
          organizationId: item.organizationId || item.OrganizationId,
          organizationName: item.organizationName || item.OrganizationName,
          showDropdown: false,
          dropdownUp: false
        })) || [];
        this.totalItems = response.totalCount || 0;
        this.totalPages = response.totalPages || Math.ceil(this.totalItems / this.itemsPerPage);
        this.filteredItems = [...this.items];
        this.paginatedData = [...this.items];
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading positions:', error);
        this.notificationService.error('Error', 'Failed to load positions. Please try again.');
        this.isLoading = false;
      }
    });
    this.subscriptions.add(sub);
  }

  loadStatistics(): void {
    const sub = this.positionsService.getStatistics().subscribe({
      next: (stats: any) => {
        this.statistics = stats;
      },
      error: (error) => {
        console.error('Error loading statistics:', error);
      }
    });
    this.subscriptions.add(sub);
  }

  loadDropdownOptions(): void {
    const sub = this.positionsService.getDropdownOptions().subscribe({
      next: (options) => {
        this.levels = options.levels || [];
        this.departments = options.departments || [];
      },
      error: (error: any) => {
        console.error('Error loading dropdown options:', error);
      }
    });
    this.subscriptions.add(sub);
  }

  // Filter & Search
  applyFilters(): void {
    this.currentPage = 1;
    this.loadData();
  }

  onSearch(): void {
    clearTimeout(this.searchTimeout);
    this.searchTimeout = setTimeout(() => {
      this.applyFilters();
    }, 300);
  }

  onFilterChange(): void {
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.filters = { createdFrom: '', createdTo: '', organizationId: '' };
    this.filterFormData = { createdFrom: '', createdTo: '', status: '', organizationId: '' };
    this.applyFilters();
  }

  refreshData(): void {
    this.loadData();
    this.loadStatistics();
  }

  get activeFilterCount(): number {
    let count = 0;
    if (this.searchQuery) count++;
    if (this.filters.isActive !== undefined) count++;
    if (this.filters.organizationId) count++;
    if (this.filters.createdFrom) count++;
    if (this.filters.createdTo) count++;
    return count;
  }
  
  // Check if user can edit/delete this item (System Admin can only edit their own organization's data)
  canEditItem(item: Position): boolean {
    if (!this.canUpdate) return false;
    if (!this.isSystemAdmin) return true; // Regular users can edit their org's data
    // System Admin can only edit their own organization's data
    const currentOrgId = this.userContextService.getCurrentOrganizationId();
    return item.organizationId === currentOrgId;
  }

  canDeleteItem(item: Position): boolean {
    if (!this.canDelete) return false;
    if (!this.isSystemAdmin) return true; // Regular users can delete their org's data
    // System Admin can only delete their own organization's data
    const currentOrgId = this.userContextService.getCurrentOrganizationId();
    return item.organizationId === currentOrgId;
  }

  // Pagination & Sorting
  updatePagination(): void {
    this.totalPages = Math.ceil(this.totalItems / this.itemsPerPage);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.loadData();
  }

  onItemsPerPageChange(): void {
    this.currentPage = 1;
    this.loadData();
  }

  sortData(field: string): void {
    // Handle organizationName sorting
    if (field === 'organizationName') {
      field = 'organizationId'; // Sort by organizationId as proxy
    }
    
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.loadData();
  }

  // Dialog Management
  openAddDialog(): void {
    this.dialogMode = 'add';
    this.formData = {
      name: '',
      description: '',
      code: '',
      level: '',
      departmentId: '',
      departmentName: '',
      isActive: true,
      sortOrder: 0
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openEditDialog(item: Position): void {
    if (!this.canEditItem(item)) {
      if (!this.canUpdate) {
        this.notificationService.error('Access Denied', 'You do not have permission to update positions.');
      } else {
        this.notificationService.error('Access Denied', 'You can only edit positions from your own organization.');
      }
      return;
    }
    this.dialogMode = 'edit';
    this.selectedItem = item;
    this.formData = {
      id: item.id,
      name: item.name,
      description: item.description || '',
      code: item.code || '',
      level: item.level || '',
      departmentId: item.departmentId || '',
      departmentName: item.departmentName || '',
      isActive: item.isActive,
      sortOrder: item.sortOrder
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: Position): void {
    const sub = this.positionsService.getPositionById(item.id).subscribe({
      next: (positionDetail: Position) => {
        this.selectedItem = positionDetail;
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading position details:', error);
        this.selectedItem = item;
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    this.subscriptions.add(sub);
  }

  openDeleteDialog(items: Position[]): void {
    // Filter out items that cannot be deleted
    const deletableItems = items.filter(item => this.canDeleteItem(item));
    
    if (deletableItems.length === 0) {
      this.notificationService.error('Access Denied', 'You can only delete positions from your own organization.');
      return;
    }
    
    if (deletableItems.length < items.length) {
      this.notificationService.warning('Partial Selection', `Only ${deletableItems.length} of ${items.length} selected positions can be deleted. Positions from other organizations will be skipped.`);
    }
    
    this.selectedItems.clear();
    deletableItems.forEach(item => this.selectedItems.add(item.id));
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  openImportDialog(): void {
    this.showImportDialog = true;
    this.selectedFile = null;
    this.importErrors = [];
    this.disableBodyScroll();
  }

  closeDialogs(): void {
    this.showAddEditDialog = false;
    this.showViewDialog = false;
    this.showDeleteDialog = false;
    this.selectedItem = null;
    this.closeAllSearchableDropdowns();
    this.enableBodyScroll();
  }

  closeImportDialog(): void {
    this.showImportDialog = false;
    this.selectedFile = null;
    this.importErrors = [];
    this.enableBodyScroll();
  }

  private disableBodyScroll(): void {
    this.commonUtility.disableBodyScroll();
  }

  private enableBodyScroll(): void {
    this.commonUtility.enableBodyScroll();
  }

  // CRUD Operations
  saveItem(): void {
    this.validateForm();

    if (!this.isFormValid()) {
      this.markAllFieldsAsTouched();
      this.notificationService.error('Validation Error', 'Please fix the validation errors before saving');
      return;
    }

    this.isSubmitting = true;

    if (this.dialogMode === 'add') {
      const createRequest: CreatePositionRequest = {
        name: this.formData.name,
        description: this.formData.description,
        code: this.formData.code,
        level: this.formData.level,
        departmentId: this.formData.departmentId || undefined,
        departmentName: this.formData.departmentName,
        isActive: this.formData.isActive,
        sortOrder: this.formData.sortOrder
      };

      this.positionsService.create(createRequest).subscribe({
        next: (newPosition: Position) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Position created successfully!');
          this.isSubmitting = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error creating position:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to create position. Please try again.');
          this.isSubmitting = false;
        }
      });
    } else {
      const updateRequest: UpdatePositionRequest = {
        name: this.formData.name,
        description: this.formData.description,
        code: this.formData.code,
        level: this.formData.level,
        departmentId: this.formData.departmentId || undefined,
        departmentName: this.formData.departmentName,
        isActive: this.formData.isActive,
        sortOrder: this.formData.sortOrder
      };

      this.positionsService.update(this.formData.id!, updateRequest).subscribe({
        next: (updatedPosition: Position) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Position updated successfully!');
          this.isSubmitting = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error updating position:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to update position. Please try again.');
          this.isSubmitting = false;
        }
      });
    }
  }

  confirmDelete(): void {
    // Filter out items that cannot be deleted (System Admin can only delete their own org's positions)
    const allItems = this.items.filter(item => this.selectedItems.has(item.id));
    const deletableItems = allItems.filter(item => this.canDeleteItem(item));
    
    if (deletableItems.length === 0) {
      this.notificationService.error('Access Denied', 'You can only delete positions from your own organization.');
      this.closeDialogs();
      return;
    }
    
    if (deletableItems.length < allItems.length) {
      this.notificationService.warning('Partial Selection', `Only ${deletableItems.length} of ${allItems.length} selected positions can be deleted. Positions from other organizations will be skipped.`);
    }
    
    const itemsToDelete = deletableItems.map(item => item.id);

    if (itemsToDelete.length === 1) {
      this.positionsService.delete(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', 'Position deleted successfully!');
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting department:', error);
          const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete position. Please try again.';
          this.notificationService.error('Error', errorMessage);
        }
      });
    } else {
      const ids = itemsToDelete.map((id: string) => id);
      this.positionsService.deleteMultiple(ids).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', `${itemsToDelete.length} positions deleted successfully`);
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting departments:', error);
          const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete positions. Please try again.';
          this.notificationService.error('Error', errorMessage);
        }
      });
    }
  }

  clonePosition(position: Position): void {
    if (!this.canCreate) {
      this.notificationService.error('Access Denied', 'You do not have permission to clone positions.');
      return;
    }
    this.notificationService.info('Info', 'Cloning position...');
    this.positionsService.cloneMultiple([position.id]).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || 'Position cloned successfully');
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone position');
      }
    });
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notificationService.warning('Warning', 'Please select at least one position to clone');
      return;
    }

    if (!this.canCreate) {
      this.notificationService.error('Access Denied', 'You do not have permission to clone positions.');
      return;
    }

    this.notificationService.info('Info', 'Cloning positions...');
    this.positionsService.cloneMultiple(selectedIds).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || `${selectedIds.length} position(s) cloned successfully`);
        this.selectedItems.clear();
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone positions');
      }
    });
  }

  // Async export with progress tracking
  startAsyncExport(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
    const formatMap: { [key: string]: 1 | 2 | 3 | 4 } = {
      'excel': 1,
      'csv': 2,
      'pdf': 3,
      'json': 4
    };

    const params: any = {
      format: formatMap[format],
      search: this.searchQuery,
      isActive: this.filters.isActive,
      createdFrom: this.filters.createdFrom || undefined,
      createdTo: this.filters.createdTo || undefined
    };

    if (this.selectedItems.size > 0) {
      params.selectedIds = Array.from(this.selectedItems);
    }

    this.isExporting = true;
    this.exportProgress = 0;
    this.exportStatus = 'Pending';
    this.showExportDialog = true;

    this.positionsService.startExportAsync(params).subscribe({
      next: (response) => {
        this.exportJobId = response.jobId;
        this.notificationService.info('Export Started', 'Your export is processing. You can continue working.');
        this.pollExportJobStatus();
      },
      error: (error) => {
        console.error('Export start error:', error);
        this.notificationService.error('Error', 'Failed to start export job.');
        this.isExporting = false;
        this.showExportDialog = false;
      }
    });
  }

  private pollExportJobStatus(): void {
    if (!this.exportJobId) return;

    const intervalId = setInterval(() => {
      if (!this.exportJobId) { clearInterval(intervalId); return; }

      this.positionsService.getExportJobStatus(this.exportJobId).subscribe({
        next: (status: ExportJobStatus) => {
          this.exportStatus = status.status;
          this.exportProgress = status.progressPercent;
          this.exportFormat = status.format;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isExporting = false;

            if (status.status === 'Completed') {
              this.notificationService.success('Export Completed', `Exported ${status.totalRows} records successfully!`);
              if (this.exportJobId) {
                this.downloadCompletedExport(this.exportJobId, status.format);
              }
            } else {
              this.notificationService.error('Export Failed', status.message || 'Export failed.');
            }

            this.exportJobId = null;
            this.exportFormat = null;
            this.showExportDialog = false;

            if (this.showImportHistoryDialog) {
              this.loadHistory();
            }
          }
        },
        error: () => {
          clearInterval(intervalId);
          this.isExporting = false;
          this.exportJobId = null;
          this.exportFormat = null;
          this.showExportDialog = false;
          this.notificationService.error('Error', 'Export status check failed.');
        }
      });
    }, 3000);
  }

  private downloadCompletedExport(jobId: string, format: string): void {
    this.positionsService.downloadExport(jobId).subscribe({
      next: (blob: Blob) => {
        const extension = this.getFileExtension(format);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `positions_export_${new Date().toISOString().split('T')[0]}.${extension}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      },
      error: (error) => {
        console.error('Download error:', error);
        this.notificationService.error('Error', 'Failed to download export file.');
      }
    });
  }

  private getFileExtension(format: string): string {
    const formatLower = format.toLowerCase();
    switch (formatLower) {
      case 'excel': return 'xlsx';
      case 'csv': return 'csv';
      case 'pdf': return 'pdf';
      case 'json': return 'json';
      default: return 'xlsx';
    }
  }

  closeExportDialog(): void {
    this.showExportDialog = false;
  }

  // Import Functionality
  downloadTemplate(): void {
    this.positionsService.getTemplate().subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'positions_import_template.xlsx';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        this.notificationService.success('Success', 'Excel template downloaded successfully');
      },
      error: (error) => {
        console.error('Template download error:', error);
        this.notificationService.error('Error', 'Failed to download template');
      }
    });
  }

  onFileSelected(event: any): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;
    const file = input.files[0];
    this.validateAndSetFile(file);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    event.dataTransfer!.dropEffect = 'copy';
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.add('drag-over');
  }

  onDragEnter(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.add('drag-enter');
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.remove('drag-over', 'drag-enter');
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.remove('drag-over', 'drag-enter');
    const files = event.dataTransfer?.files;
    if (!files || files.length === 0) return;
    const file = files[0];
    this.validateAndSetFile(file);
  }

  private validateAndSetFile(file: File): void {
    const validTypes = ['application/vnd.ms-excel', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'text/csv'];
    const validExtensions = ['.csv', '.xlsx', '.xls'];
    const fileExtension = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

    if (!validTypes.includes(file.type) && !validExtensions.includes(fileExtension)) {
      this.notificationService.warning('Invalid File', 'Please upload a valid Excel (.xlsx, .xls) or CSV (.csv) file');
      return;
    }

    const maxSize = 5 * 1024 * 1024;
    if (file.size > maxSize) {
      this.notificationService.warning('File Too Large', `File size should not exceed 5MB. Your file is ${(file.size / (1024 * 1024)).toFixed(2)}MB`);
      return;
    }

    this.selectedFile = file;
    this.importErrors = [];
  }

  importData(): void {
    if (!this.selectedFile) {
      this.notificationService.warning('Warning', 'Please select a file to import');
      return;
    }

    this.isImporting = true;
    this.importErrors = [];
    this.errorReportId = null;
    this.importProgress = 0;
    this.importStatus = 'Pending';

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.positionsService.startImportAsync(formData).subscribe({
      next: (res) => {
        this.importJobId = res.jobId;
        this.notificationService.info('Import Started', 'Your import is processing in the background. Check Import History for progress.');
        this.closeImportDialog();
        this.pollImportJobStatus();
      },
      error: (error) => {
        this.isImporting = false;
        console.error('Async import start error:', error);
        this.notificationService.error('Error', 'Failed to start import job.');
      }
    });
  }

  private pollImportJobStatus(): void {
    if (!this.importJobId) return;

    const intervalId = setInterval(() => {
      if (!this.importJobId) { clearInterval(intervalId); return; }

      this.positionsService.getImportJobStatus(this.importJobId).subscribe({
        next: (status: ImportJobStatus) => {
          this.importStatus = status.status;
          this.importProgress = status.progressPercent ?? 0;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isImporting = false;
            this.importJobId = null;

            if (status.status === 'Completed') {
              this.notificationService.success('Import Completed', `Imported ${status.successCount} positions${status.skippedCount ? `, skipped ${status.skippedCount}` : ''}${status.errorCount ? `, errors ${status.errorCount}` : ''}.`);
            } else if (status.status === 'Failed') {
              let errorMessage = status.message || 'Import failed.';
              if (status.totalRows === 0) {
                errorMessage = 'File parsing failed. Please check that your file has the correct format and required headers. Download the template for reference.';
              }
              this.notificationService.error('Import Failed', errorMessage);

              if (status.errorCount > 0) {
                this.notificationService.info('Error Details', `Check Import History to download detailed error report (${status.errorCount} errors found).`);
              }
            }

            this.loadData();
            this.loadStatistics();
            if (this.showImportHistoryDialog) {
              this.loadHistory();
            }
          }
        },
        error: () => {
          clearInterval(intervalId);
          this.isImporting = false;
          this.importJobId = null;
          this.notificationService.error('Error', 'Import job status check failed.');
        }
      });
    }, 5000);
  }

  downloadErrorReport(): void {
    if (!this.errorReportId) {
      this.notificationService.warning('Warning', 'No error report available');
      return;
    }

    this.positionsService.getImportErrorReport(this.errorReportId).subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `import_errors_${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        this.notificationService.success('Success', 'Error report downloaded successfully');
      },
      error: (error) => {
        console.error('Error report download error:', error);
        this.notificationService.error('Error', 'Failed to download error report');
      }
    });
  }

  downloadErrorReportForHistory(historyId: string): void {
    const history = this.history.find(h => h.id === historyId);
    if (!history || !history.errorReportId) {
      this.notificationService.warning('Warning', 'No error report available for this import');
      return;
    }

    this.positionsService.getImportErrorReport(history.errorReportId).subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `import_errors_${history.fileName}_${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        this.notificationService.success('Success', 'Error report downloaded successfully');
      },
      error: (error) => {
        console.error('Error report download error:', error);
        this.notificationService.error('Error', 'Failed to download error report. The error report may have expired or been deleted.');
      }
    });
  }

  downloadExportFromHistory(historyId: string): void {
    const history = this.history.find(h => h.id === historyId);
    if (!history) {
      this.notificationService.warning('Warning', 'Export not found');
      return;
    }

    if (!history.jobId) {
      this.notificationService.warning('Warning', 'Job ID not found for this export');
      return;
    }

    this.positionsService.downloadExport(history.jobId).subscribe({
      next: (blob: Blob) => {
        const extension = this.getFileExtension(history.format);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = history.fileName || `positions_export_${new Date().toISOString().split('T')[0]}.${extension}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        this.notificationService.success('Success', 'Export downloaded successfully');
      },
      error: (error) => {
        console.error('Download error:', error);
        this.notificationService.error('Error', 'Failed to download export file. The file may have expired or been deleted.');
      }
    });
  }

  // Import History Methods
  openImportHistoryDialog(): void {
    this.showImportHistoryDialog = true;
    this.historyPage = 1;
    this.historyType = undefined;
    this.loadHistory();
    this.disableBodyScroll();
  }

  loadHistory(): void {
    this.isLoadingHistory = true;
    this.positionsService.getHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
      next: (response) => {
        this.history = response.items;
        this.historyTotalCount = response.totalCount;
        this.isLoadingHistory = false;
      },
      error: (error) => {
        console.error('Error loading history:', error);
        this.notificationService.error('Error', 'Failed to load history');
        this.isLoadingHistory = false;
      }
    });
  }

  filterHistory(type: 'all' | 'import' | 'export'): void {
    this.historyType = type === 'all' ? undefined : type;
    this.historyPage = 1;
    this.loadHistory();
  }

  closeImportHistoryDialog(): void {
    this.showImportHistoryDialog = false;
    this.enableBodyScroll();
  }

  getHistoryTotalPages(): number {
    return Math.ceil(this.historyTotalCount / this.historyPageSize);
  }

  goToHistoryPage(page: number): void {
    if (page >= 1 && page <= this.getHistoryTotalPages()) {
      this.historyPage = page;
      this.loadHistory();
    }
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    else if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    else return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  }

  getStrategyDisplayName(strategy: string): string {
    switch (strategy.toLowerCase()) {
      case 'skip': return 'Skip Duplicates';
      case 'update': return 'Update Existing';
      case 'createnew': return 'Create New';
      default: return strategy;
    }
  }

  triggerFileInput(): void {
    const fileInput = document.getElementById('fileInput') as HTMLInputElement;
    if (fileInput) {
      fileInput.click();
    }
  }

  removeSelectedFile(): void {
    this.selectedFile = null;
    this.importErrors = [];
  }

  toggleActiveStatus(position: Position): void {
    const newStatus = !position.isActive;
    this.positionsService.setActive(position.id, newStatus).subscribe({
      next: () => {
        this.notificationService.success('Success', `Position ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        console.error('Error toggling active status:', error);
        this.notificationService.error('Error', 'Failed to update position status.');
      }
    });
  }

  getHistoryStatusClass(status: string): string {
    const normalized = (status || '').toLowerCase();
    if (normalized.includes('success') || normalized === 'completed') return 'status-badge status-success';
    if (normalized.includes('fail') || normalized === 'error') return 'status-badge status-failed';
    if (normalized.includes('pending') || normalized.includes('processing') || normalized === 'inprogress') return 'status-badge status-warning';
    return 'status-badge';
  }

  isCurrentJob(history: ImportExportHistory): boolean {
    if (!this.isImporting || !this.importJobId) return false;
    const historyTime = new Date(history.createdAtUtc).getTime();
    const now = Date.now();
    const isVeryRecent = (now - historyTime) < 120000;
    const isStillProcessing = history.status === 'Processing' || history.status === 'Pending';
    return isVeryRecent && isStillProcessing;
  }

  getStatusClass(isActive: boolean): string {
    return isActive ? 'status-active' : 'status-inactive';
  }

  getStatusIcon(isActive: boolean): string {
    return isActive ? 'fa-check-circle' : 'fa-times-circle';
  }

  toggleItemSelection(id: string): void {
    this.commonUtility.toggleItemSelection(id, this.selectedItems);
  }

  isAllSelected(): boolean {
    return this.commonUtility.isAllSelected(this.paginatedData, this.selectedItems);
  }

  masterToggle(): void {
    this.commonUtility.masterToggle(this.paginatedData, this.selectedItems);
  }

  // Dropdown methods
  toggleDropdown(item: Position, event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }
    
    this.exportDropdownOpen = false;

    this.items.forEach((dataItem: Position) => {
      if (dataItem.id !== item.id) {
        dataItem.showDropdown = false;
      }
    });

    item.showDropdown = !item.showDropdown;
  }

  closeAllDropdowns(): void {
    this.items.forEach((item: Position) => {
      item.showDropdown = false;
    });
    this.commonUtility.closeAllDropdowns(this.dropdownStates);
  }

  toggleExportDropdown(): void {
    this.closeAllDropdowns();
    this.exportDropdownOpen = !this.exportDropdownOpen;
  }

  // Searchable dropdown methods
  toggleSearchableDropdown(fieldName: string): void {
    this.initializeDropdownState(fieldName);

    if (this.dropdownStates[fieldName].isOpen) {
      this.dropdownStates[fieldName].isOpen = false;
      this.dropdownStates[fieldName].searchTerm = '';
    } else {
      this.closeAllSearchableDropdowns();
      this.dropdownStates[fieldName].isOpen = true;
      this.dropdownStates[fieldName].searchTerm = '';
      this.filterOptions(fieldName);
    }
  }

  filterOptions(fieldName: string): void {
    this.initializeDropdownState(fieldName);
    const searchTerm = this.dropdownStates[fieldName].searchTerm;
    const allOptions = this.getOptionsForField(fieldName);
    this.dropdownStates[fieldName].filteredOptions = this.commonUtility.filterDropdownOptions(allOptions, searchTerm);
  }

  selectFilterOption(fieldName: string, option: string | any): void {
    // Handle organizationId filter which uses object with id property
    if (fieldName === 'organizationId' && typeof option === 'object' && option.id) {
      option = option.id;
    }
    
    if (fieldName === 'status') {
      if (option === '') {
        this.filterFormData.status = '';
        this.filters.isActive = undefined;
      } else {
        this.filterFormData.status = option;
        this.filters.isActive = option === 'true';
      }
    } else {
      this.commonUtility.selectFilterOption(this.filterFormData, this.filters, fieldName, option);
    }
    this.closeAllSearchableDropdowns();
    this.onFilterChange();
  }

  selectItemsPerPageOption(option: string): void {
    this.itemsPerPageFormData = parseInt(option);
    this.itemsPerPage = parseInt(option);
    this.closeAllSearchableDropdowns();
    this.onItemsPerPageChange();
  }

  onSearchInput(fieldName: string, event: Event): void {
    this.commonUtility.onDropdownSearchInput(this.dropdownStates, fieldName, event, (fieldName: string) => this.filterOptions(fieldName));
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
      this.dropdownStates[key].searchTerm = '';
    });
  }

  clearDropdownValue(fieldName: string, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }

    if (fieldName.startsWith('filter-')) {
      const actualField = fieldName.replace('filter-', '');
      if (actualField === 'status') {
        this.filterFormData.status = '';
        this.filters.isActive = undefined;
      } else if (actualField === 'organizationId') {
        this.filterFormData.organizationId = '';
        this.filters.organizationId = '';
      } else {
        this.filterFormData[actualField] = '';
        this.filters[actualField] = '';
      }
      this.onFilterChange();
    } else {
      (this.formData as any)[fieldName] = '';
      this.validateField(fieldName);
    }

    if (this.dropdownStates[fieldName]) {
      this.dropdownStates[fieldName].isOpen = false;
      this.dropdownStates[fieldName].searchTerm = '';
    }
  }

  hasDropdownValue(fieldName: string): boolean {
    if (fieldName.startsWith('filter-')) {
      const actualField = fieldName.replace('filter-', '');
      if (actualField === 'status') {
        return !!this.filterFormData.status;
      }
      if (actualField === 'organizationId') {
        return !!this.filterFormData.organizationId;
      }
      return !!this.filterFormData[actualField];
    } else {
      const value = (this.formData as any)[fieldName];
      return !!value;
    }
  }

  onFormFieldFocus(): void {
    this.commonUtility.onFormFieldFocus(this.dropdownStates);
  }

  onModalBodyClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (target.classList.contains('modal-body') ||
        target.classList.contains('form-section') ||
        target.classList.contains('form-sections') ||
        (!target.closest('.form-group') && !target.closest('.searchable-dropdown'))) {
      this.closeAllSearchableDropdowns();
    }
  }

  onMainPageClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.form-group') &&
        !target.closest('.searchable-dropdown') &&
        !target.closest('.dropdown-toggle') &&
        !target.closest('.dropdown-menu') &&
        !target.closest('.action-dropdown')) {
      this.closeAllSearchableDropdowns();
      this.closeAllDropdowns();
      this.exportDropdownOpen = false;
    }
  }

  getSelectedItems(): Position[] {
    return this.commonUtility.getSelectedItemsFromArray(this.items, this.selectedItems);
  }

  get deleteItemCount(): number {
    return this.selectedItems.size;
  }

  // Form validation
  private isFormValid(): boolean {
    return this.commonUtility.isFormValid(this.errors);
  }

  private validateForm(): void {
    this.errors = {};

    if (!this.formData.name || this.formData.name.trim() === '') {
      this.errors['name'] = 'Name is required';
    }
  }

  validateField(fieldName: string): void {
    switch (fieldName) {
      case 'name':
        if (!this.formData.name || this.formData.name.trim() === '') {
          this.errors['name'] = 'Name is required';
        } else {
          delete this.errors['name'];
        }
        break;
    }
  }

  private markAllFieldsAsTouched(): void {
    const fields = ['name'];
    fields.forEach((field: string) => {
      this.validateField(field);
    });
  }

  hasError(fieldName: string): boolean {
    return !!this.errors[fieldName];
  }

  getError(fieldName: string): string {
    return this.errors[fieldName] || '';
  }

  // Simple PDF Export using jsPDF
  exportPositionDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notificationService.warning('Warning', 'No position selected to export');
      return;
    }

    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const positionDetailsCards = document.querySelector('.modal-overlay .modal .entity-details-cards') as HTMLElement;

    if (!modalBody || !positionDetailsCards) {
      this.notificationService.error('Error', 'Unable to find content to export');
      return;
    }

    this.notificationService.info('Info', 'Generating PDF...');

    const originalModalBodyOverflow = modalBody.style.overflow;
    const originalModalBodyHeight = modalBody.style.height;
    const originalModalBodyMaxHeight = modalBody.style.maxHeight;
    const originalModalActionsDisplay = modalBody.querySelector('.modal-actions') ? (modalBody.querySelector('.modal-actions') as HTMLElement).style.display : '';

    const modalActions = modalBody.querySelector('.modal-actions') as HTMLElement;
    if (modalActions) {
      modalActions.style.display = 'none';
    }

    modalBody.style.overflow = 'visible';
    modalBody.style.height = 'auto';
    modalBody.style.maxHeight = 'none';

    setTimeout(() => {
      html2canvas(positionDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: true,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: positionDetailsCards.scrollWidth,
        windowHeight: positionDetailsCards.scrollHeight
      }).then(canvas => {
      modalBody.style.overflow = originalModalBodyOverflow;
      modalBody.style.height = originalModalBodyHeight;
      modalBody.style.maxHeight = originalModalBodyMaxHeight;
      if (modalActions) {
        modalActions.style.display = originalModalActionsDisplay;
      }

      const imgData = canvas.toDataURL('image/png');
      const pdf = new jsPDF('p', 'mm', 'a4');

      const imgWidth = 210;
      const pageHeight = 295;
      const imgHeight = (canvas.height * imgWidth) / canvas.width;
      let heightLeft = imgHeight;

      let position = 0;

      pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
      heightLeft -= pageHeight;

      while (heightLeft >= 0) {
        position = heightLeft - imgHeight;
        pdf.addPage();
        pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
        heightLeft -= pageHeight;
      }

      pdf.save(`position-details-${this.selectedItem?.name.replace(/\s+/g, '-').toLowerCase()}.pdf`);

      this.notificationService.success('Success', 'PDF exported successfully');
    }).catch((error: any) => {
      modalBody.style.overflow = originalModalBodyOverflow;
      modalBody.style.height = originalModalBodyHeight;
      modalBody.style.maxHeight = originalModalBodyMaxHeight;
      if (modalActions) {
        modalActions.style.display = originalModalActionsDisplay;
      }

      console.error('PDF export error:', error);
      this.notificationService.error('Error', 'Failed to export PDF. Please try again.');
    });
    }, 100);
  }

  // Utility Methods
  formatDate(date: Date | string): string {
    return this.commonUtility.formatDate(date);
  }

  formatDateTime(date: Date | string): string {
    return this.commonUtility.formatDateTime(date);
  }

  formatCurrency(amount: number | undefined): string {
    if (amount === undefined || amount === null) return '-';
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
  }

  selectOption(fieldName: string, option: string): void {
    if (fieldName === 'departmentId') {
      const parts = option.split('|');
      if (parts.length >= 2) {
        this.formData.departmentId = parts[0];
        this.formData.departmentName = parts[1];
      }
    } else {
      this.commonUtility.selectFormOption(this.formData, fieldName, option);
    }
    this.closeAllSearchableDropdowns();
    this.validateField(fieldName);
  }

  getDisplayValue(fieldName: string): string {
    if (fieldName === 'departmentId') {
      return this.formData.departmentName || '';
    }
    return this.commonUtility.getDropdownDisplayValue((this.formData as any)[fieldName]);
  }
}

