import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PermissionsService, Permission, CreatePermissionRequest, UpdatePermissionRequest, PermissionStatistics, DropdownOptions, ImportJobStatus, ImportExportHistory, ImportExportHistoryResponse, ExportJobStatus, Menu } from '../../../core/services/permissions.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { AuthorizationService } from '../../../core/services/authorization.service';
import { UserContextService } from '../../../core/services/user-context.service';
import { Subscription, timer } from 'rxjs';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

@Component({
  selector: 'app-permissions',
  standalone: true,
  imports: [CommonModule, FormsModule, NotificationContainerComponent, BreadcrumbComponent, HasPermissionDirective],
  templateUrl: './permissions.component.html',
  styleUrls: ['./permissions.component.scss']
})
export class PermissionsComponent implements OnInit, OnDestroy {
  // Utility references
  Math = Math;
  document = document;

  // Subscriptions
  private subscriptions = new Subscription();

  // Data properties
  items: Permission[] = [];
  filteredItems: Permission[] = [];
  paginatedData: Permission[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: CreatePermissionRequest & UpdatePermissionRequest & { id?: string } = {
    code: '',
    name: '',
    description: '',
    module: '',
    action: '',
    resource: '',
    isActive: true,
    sortOrder: 0,
    category: '',
    menuId: '', // ✅ Menu Foreign Key (Required)
    isSystemAdminOnly: false // System Admin only flag (default: false - Company Admin accessible)
  };
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: Permission | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '', category: '', module: '', action: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', category: '', module: '', action: '', status: '' };
  showFilters = false;
  searchQuery = '';

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
  modules: string[] = [];
  actions: string[] = [];
  categories: string[] = [];
  menus: Menu[] = []; // ✅ Menu dropdown options

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
  statistics: PermissionStatistics = {
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
    private permissionsService: PermissionsService,
    private commonUtility: CommonUtilityService,
    private authorizationService: AuthorizationService,
    private userContextService: UserContextService
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeDropdownStates();
    
    // Load critical data first (permissions and main data)
    this.loadPermissions();
    this.loadData();
    
    // Load non-critical data with small delays to avoid rate limiting
    // Statistics and dropdown options can load slightly later
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
          // Check if user is System Admin - System Admin has full access
          const isSystemAdmin = this.userContextService.isSystemAdmin();
          
          if (isSystemAdmin) {
            // System Admin has full access
            this.canCreate = this.authorizationService.hasPermission('Permissions.Create');
            this.canUpdate = this.authorizationService.hasPermission('Permissions.Update');
            this.canDelete = this.authorizationService.hasPermission('Permissions.Delete');
            this.canExport = this.authorizationService.hasPermission('Permissions.Export');
            this.canImport = this.authorizationService.hasPermission('Permissions.Import');
          } else {
            // Company Admin: Only Read and Update (for activate/deactivate) access
            this.canCreate = false; // No create access
            this.canUpdate = this.authorizationService.hasPermission('Permissions.Update'); // Only for activate/deactivate
            this.canDelete = false; // No delete access
            this.canExport = false; // No export access
            this.canImport = false; // No import access
          }
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
    const fields = ['module', 'action', 'category', 'menuId', 'isActive', 'filter-module', 'filter-action', 'filter-category', 'filter-status', 'items-per-page'];
    fields.forEach(field => {
      this.initializeDropdownState(field);
    });
  }

  initializeDropdownState(fieldName: string): void {
    if (!this.dropdownStates[fieldName]) {
      const options = this.getOptionsForField(fieldName);
      this.dropdownStates[fieldName] = this.commonUtility.initializeDropdownState(fieldName, options);
    }
  }

  getOptionsForField(fieldName: string): string[] {
    switch (fieldName) {
      case 'module': return this.modules;
      case 'action': return this.actions;
      case 'category': return this.categories;
      case 'menuId': 
        // ✅ Return menu labels with sections for dropdown
        return this.menus.map(m => {
          const section = m.section ? ` (${m.section})` : '';
          return `${m.id}|${m.label}${section}`; // Store ID with label for selection
        });
      case 'isActive': return ['true', 'false'];
      case 'filter-module': return this.modules;
      case 'filter-action': return this.actions;
      case 'filter-category': return this.categories;
      case 'filter-status': return ['', 'true', 'false'];
      case 'items-per-page': return ['10', '25', '50', '100'];
      default: return [];
    }
  }

  // Data loading
  loadData(): void {
    this.isLoading = true;

    const params = {
      page: this.currentPage,
      pageSize: this.itemsPerPage,
      search: this.searchQuery,
      sortField: this.sortField,
      sortDirection: this.sortDirection,
      module: this.filters.module,
      action: this.filters.action,
      category: this.filters.category,
      isActive: this.filters.isActive,
      createdFrom: this.filters.createdFrom || undefined,
      createdTo: this.filters.createdTo || undefined
    };

    const sub = this.permissionsService.getData(params).subscribe({
      next: (response: any) => {
        this.items = response.items?.map((item: Permission) => ({
          ...item,
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
        console.error('Error loading permissions:', error);
        this.notificationService.error('Error', 'Failed to load permissions. Please try again.');
        this.isLoading = false;
      }
    });
    this.subscriptions.add(sub);
  }

  loadStatistics(): void {
    const sub = this.permissionsService.getStatistics().subscribe({
      next: (stats: any) => {
        this.statistics = stats;
      },
      error: (error) => {
        console.error('Error loading statistics:', error);
        // Don't show error notification for statistics as it's non-critical
      }
    });
    this.subscriptions.add(sub);
  }

  loadDropdownOptions(): void {
    // Load modules, actions, categories
    const sub1 = this.permissionsService.getDropdownOptions().subscribe({
      next: (options) => {
        this.modules = options.modules;
        this.actions = options.actions;
        this.categories = options.categories;
      },
      error: (error: any) => {
        console.error('Error loading dropdown options:', error);
        this.notificationService.error('Error', 'Failed to load dropdown options');
      }
    });
    this.subscriptions.add(sub1);

    // ✅ Load menus dropdown with small delay to avoid rate limiting
    const menuTimer = timer(50).subscribe(() => {
      const sub2 = this.permissionsService.getMenuDropdownOptions().subscribe({
        next: (menus) => {
          this.menus = menus;
        },
        error: (error: any) => {
          console.error('Error loading menu options:', error);
          this.notificationService.error('Error', 'Failed to load menu options');
        }
      });
      this.subscriptions.add(sub2);
    });
    this.subscriptions.add(menuTimer);
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
    this.filters = { createdFrom: '', createdTo: '', category: '', module: '', action: '' };
    this.filterFormData = { createdFrom: '', createdTo: '', category: '', module: '', action: '', status: '' };
    this.applyFilters();
  }

  refreshData(): void {
    
    this.loadData();
    this.loadStatistics();
  }

  get activeFilterCount(): number {
    let count = 0;
    if (this.searchQuery) count++;
    if (this.filters.module) count++;
    if (this.filters.action) count++;
    if (this.filters.category) count++;
    if (this.filters.isActive !== undefined) count++;
    if (this.filters.createdFrom) count++;
    if (this.filters.createdTo) count++;
    return count;
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
      code: '',
      name: '',
      description: '',
      module: '',
      action: '',
      resource: '',
      isActive: true,
      sortOrder: 0,
      category: '',
      menuId: '', // ✅ Menu Foreign Key (Required)
      isSystemAdminOnly: false // System Admin only flag (default: false - Company Admin accessible)
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openEditDialog(item: Permission): void {
    this.dialogMode = 'edit';
    this.selectedItem = item;
    this.formData = {
      id: item.id,
      code: item.code,
      name: item.name,
      description: item.description || '',
      module: item.module,
      action: item.action,
      resource: item.resource,
      isActive: item.isActive,
      sortOrder: item.sortOrder,
      category: item.category || '',
      menuId: item.menuId || '', // ✅ Menu Foreign Key (Required)
      isSystemAdminOnly: item.isSystemAdminOnly || false // System Admin only flag
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: Permission): void {
    // Fetch fresh detail data to ensure metadata is up-to-date
    const sub = this.permissionsService.getPermissionById(item.id).subscribe({
      next: (permissionDetail: Permission) => {
        this.selectedItem = permissionDetail;
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading permission details:', error);
        // Fallback to list item if detail fetch fails
        this.selectedItem = item;
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    this.subscriptions.add(sub);
  }

  openDeleteDialog(items: Permission[]): void {
    this.selectedItems.clear();
    items.forEach(item => this.selectedItems.add(item.id));
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notificationService.warning('Warning', 'Please select at least one permission to clone');
      return;
    }

    this.notificationService.info('Info', 'Cloning permissions...');
    this.permissionsService.cloneMultiple(selectedIds).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || `${selectedIds.length} permission(s) cloned successfully`);
        this.selectedItems.clear();
        this.loadData();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone permissions');
      }
    });
  }

  clonePermission(permission: any): void {
    this.notificationService.info('Info', 'Cloning permission...');
    this.permissionsService.cloneMultiple([permission.id]).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || 'Permission cloned successfully');
        this.loadData();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone permission');
      }
    });
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
      const createRequest: CreatePermissionRequest = {
        code: this.formData.code,
        name: this.formData.name,
        description: this.formData.description,
        module: this.formData.module,
        action: this.formData.action,
        resource: this.formData.resource,
        sortOrder: this.formData.sortOrder,
        category: this.formData.category,
        menuId: this.formData.menuId, // ✅ Menu Foreign Key (Required)
        isSystemAdminOnly: this.formData.isSystemAdminOnly || false // System Admin only flag
      };

      this.permissionsService.create(createRequest).subscribe({
        next: (newPermission: Permission) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Permission created successfully!');
          this.isSubmitting = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error creating permission:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to create permission. Please try again.');
          this.isSubmitting = false;
        }
      });
    } else {
      const updateRequest: UpdatePermissionRequest = {
        code: this.formData.code,
        name: this.formData.name,
        description: this.formData.description,
        module: this.formData.module,
        action: this.formData.action,
        resource: this.formData.resource,
        isActive: this.formData.isActive,
        sortOrder: this.formData.sortOrder,
        category: this.formData.category,
        menuId: this.formData.menuId, // ✅ Menu Foreign Key (Required)
        isSystemAdminOnly: this.formData.isSystemAdminOnly || false // System Admin only flag
      };

      this.permissionsService.update(this.formData.id!, updateRequest).subscribe({
        next: (updatedPermission: Permission) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Permission updated successfully!');
          this.isSubmitting = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error updating permission:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to update permission. Please try again.');
          this.isSubmitting = false;
        }
      });
    }
  }

  confirmDelete(): void {
    const itemsToDelete = Array.from(this.selectedItems);

    if (itemsToDelete.length === 1) {
      this.permissionsService.delete(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', 'Permission deleted successfully!');
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting permission:', error);
          const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete permission. Please try again.';
          this.notificationService.error('Error', errorMessage);
        }
      });
    } else {
      const ids = itemsToDelete.map((id: string) => id);
      this.permissionsService.deleteMultiple(ids).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', `${itemsToDelete.length} permissions deleted successfully`);
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting permissions:', error);
          const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete permissions. Please try again.';
          this.notificationService.error('Error', errorMessage);
        }
      });
    }
  }

  // Export method (wrapper for startAsyncExport)
  exportPermissions(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
    this.startAsyncExport(format);
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
      module: this.filters.module,
      action: this.filters.action,
      category: this.filters.category,
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

    this.permissionsService.startExportAsync(params).subscribe({
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

      this.permissionsService.getExportJobStatus(this.exportJobId).subscribe({
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
    this.permissionsService.downloadExport(jobId).subscribe({
      next: (blob: Blob) => {
        const extension = this.getFileExtension(format);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `permissions_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.permissionsService.getTemplate().subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'permissions_import_template.xlsx';
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

    this.permissionsService.startImportAsync(formData).subscribe({
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

      this.permissionsService.getImportJobStatus(this.importJobId).subscribe({
        next: (status: ImportJobStatus) => {
          this.importStatus = status.status;
          this.importProgress = status.progressPercent ?? 0;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isImporting = false;
            this.importJobId = null;

            if (status.status === 'Completed') {
              this.notificationService.success('Import Completed', `Imported ${status.successCount} permissions${status.skippedCount ? `, skipped ${status.skippedCount}` : ''}${status.errorCount ? `, errors ${status.errorCount}` : ''}.`);
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

    this.permissionsService.getImportErrorReport(this.errorReportId).subscribe({
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

    this.permissionsService.getImportErrorReport(history.errorReportId).subscribe({
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

    this.permissionsService.downloadExport(history.jobId).subscribe({
      next: (blob: Blob) => {
        const extension = this.getFileExtension(history.format);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = history.fileName || `permissions_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.permissionsService.getHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
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

  toggleActiveStatus(permission: Permission): void {
    const newStatus = !permission.isActive;
    // Note: You might need to create a setActive method in the service
    this.permissionsService.update(permission.id, {
      code: permission.code,
      name: permission.name,
      description: permission.description,
      module: permission.module,
      action: permission.action,
      resource: permission.resource,
      isActive: newStatus,
      sortOrder: permission.sortOrder,
      category: permission.category,
      menuId: permission.menuId || ''
    }).subscribe({
      next: () => {
        this.notificationService.success('Success', `Permission ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        console.error('Error toggling active status:', error);
        this.notificationService.error('Error', 'Failed to update permission status.');
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
  toggleDropdown(item: Permission, event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }
    
    this.exportDropdownOpen = false;

    this.items.forEach((dataItem: Permission) => {
      if (dataItem.id !== item.id) {
        dataItem.showDropdown = false;
      }
    });

    item.showDropdown = !item.showDropdown;
  }

  closeAllDropdowns(): void {
    this.items.forEach((item: Permission) => {
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
    
    if (fieldName === 'menuId') {
      // ✅ For menuId, filter by label/section but keep full format (id|label)
      const filtered = allOptions.filter(opt => {
        const parts = opt.split('|');
        const displayText = parts.length >= 2 ? parts[1] : opt;
        return displayText.toLowerCase().includes(searchTerm.toLowerCase());
      });
      this.dropdownStates[fieldName].filteredOptions = filtered;
    } else {
      this.dropdownStates[fieldName].filteredOptions = this.commonUtility.filterDropdownOptions(allOptions, searchTerm);
    }
  }

  selectOption(fieldName: string, option: string): void {
    if (fieldName === 'menuId') {
      // ✅ Extract menu ID from option format: "id|label (section)"
      const parts = option.split('|');
      if (parts.length >= 2) {
        this.formData.menuId = parts[0]; // Set the menu ID
      }
    } else {
      this.commonUtility.selectFormOption(this.formData, fieldName, option);
    }
    this.closeAllSearchableDropdowns();
    this.validateField(fieldName);
  }

  selectFilterOption(fieldName: string, option: string): void {
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

  getDisplayValue(fieldName: string): string {
    if (fieldName === 'menuId') {
      // ✅ Find menu by ID and return label with section
      const menu = this.menus.find(m => m.id === this.formData.menuId);
      if (menu) {
        return menu.section ? `${menu.label} (${menu.section})` : menu.label;
      }
      return '';
    }
    return this.commonUtility.getDropdownDisplayValue((this.formData as any)[fieldName]);
  }

  // ✅ Helper methods for Menu dropdown
  getMenuDisplayText(option: string): string {
    const parts = option.split('|');
    return parts.length >= 2 ? parts[1] : option;
  }

  isMenuOptionSelected(option: string): boolean {
    if (!this.formData.menuId) return false;
    const parts = option.split('|');
    return parts.length >= 2 && parts[0] === this.formData.menuId;
  }

  getFilterDisplayValue(fieldName: string): string {
    return this.commonUtility.getFilterDisplayValue(this.filterFormData, fieldName);
  }

  closeAllSearchableDropdowns(): void {
    Object.keys(this.dropdownStates).forEach((key: string) => {
      this.dropdownStates[key].isOpen = false;
      this.dropdownStates[key].searchTerm = '';
    });
  }

  /**
   * Clear dropdown value - Can be used for any dropdown field
   * @param fieldName - Field name (e.g., 'filter-module', 'menuId', etc.)
   */
  clearDropdownValue(fieldName: string, event?: Event): void {
    if (event) {
      event.stopPropagation(); // Prevent dropdown from opening/closing
    }

    if (fieldName.startsWith('filter-')) {
      const actualField = fieldName.replace('filter-', '');
      if (actualField === 'status') {
        this.filterFormData.status = '';
        this.filters.isActive = undefined;
      } else {
        this.filterFormData[actualField] = '';
        this.filters[actualField] = '';
      }
      this.onFilterChange();
    } else {
      // Form dropdowns
      if (fieldName === 'menuId') {
        this.formData.menuId = '';
      } else {
        (this.formData as any)[fieldName] = '';
      }
      this.validateField(fieldName);
    }

    // Close the dropdown
    if (this.dropdownStates[fieldName]) {
      this.dropdownStates[fieldName].isOpen = false;
      this.dropdownStates[fieldName].searchTerm = '';
    }
  }

  /**
   * Check if dropdown has a value (for showing clear button)
   * @param fieldName - Field name
   * @returns true if dropdown has a value
   */
  hasDropdownValue(fieldName: string): boolean {
    if (fieldName.startsWith('filter-')) {
      const actualField = fieldName.replace('filter-', '');
      if (actualField === 'status') {
        return !!this.filterFormData.status;
      }
      return !!this.filterFormData[actualField];
    } else {
      // Form dropdowns
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

  getSelectedItems(): Permission[] {
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

    if (!this.formData.code || this.formData.code.trim() === '') {
      this.errors['code'] = 'Code is required';
    }

    if (!this.formData.name || this.formData.name.trim() === '') {
      this.errors['name'] = 'Name is required';
    }

    if (!this.formData.module || this.formData.module.trim() === '') {
      this.errors['module'] = 'Module is required';
    }

    if (!this.formData.action || this.formData.action.trim() === '') {
      this.errors['action'] = 'Action is required';
    }

    if (!this.formData.resource || this.formData.resource.trim() === '') {
      this.errors['resource'] = 'Resource is required';
    }

    // ✅ Validate Menu (Required)
    if (!this.formData.menuId || this.formData.menuId.trim() === '') {
      this.errors['menuId'] = 'Menu is required';
    }
  }

  validateField(fieldName: string): void {
    switch (fieldName) {
      case 'code':
        if (!this.formData.code || this.formData.code.trim() === '') {
          this.errors['code'] = 'Code is required';
        } else {
          delete this.errors['code'];
        }
        break;
      case 'name':
        if (!this.formData.name || this.formData.name.trim() === '') {
          this.errors['name'] = 'Name is required';
        } else {
          delete this.errors['name'];
        }
        break;
      case 'module':
        if (!this.formData.module || this.formData.module.trim() === '') {
          this.errors['module'] = 'Module is required';
        } else {
          delete this.errors['module'];
        }
        break;
      case 'action':
        if (!this.formData.action || this.formData.action.trim() === '') {
          this.errors['action'] = 'Action is required';
        } else {
          delete this.errors['action'];
        }
        break;
      case 'resource':
        if (!this.formData.resource || this.formData.resource.trim() === '') {
          this.errors['resource'] = 'Resource is required';
        } else {
          delete this.errors['resource'];
        }
        break;
      case 'menuId':
        // ✅ Validate Menu (Required)
        if (!this.formData.menuId || this.formData.menuId.trim() === '') {
          this.errors['menuId'] = 'Menu is required';
        } else {
          delete this.errors['menuId'];
        }
        break;
    }
  }

  private markAllFieldsAsTouched(): void {
    const fields = ['code', 'name', 'module', 'action', 'resource', 'menuId'];
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
  exportPermissionDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notificationService.warning('Warning', 'No permission selected to export');
      return;
    }

    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const permissionDetailsCards = document.querySelector('.modal-overlay .modal .entity-details-cards') as HTMLElement;

    if (!modalBody || !permissionDetailsCards) {
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
      html2canvas(permissionDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: true,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: permissionDetailsCards.scrollWidth,
        windowHeight: permissionDetailsCards.scrollHeight
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

      pdf.save(`permission-details-${this.selectedItem?.name.replace(/\s+/g, '-').toLowerCase()}.pdf`);

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
}
