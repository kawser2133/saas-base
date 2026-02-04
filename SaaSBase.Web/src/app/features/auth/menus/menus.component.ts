import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MenusService, Menu, CreateMenuRequest, UpdateMenuRequest, MenuStatistics, MenuDropdown, ImportJobStatus, ImportExportHistory, ImportExportHistoryResponse, ExportJobStatus } from '../../../core/services/menus.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { AuthorizationService } from '../../../core/services/authorization.service';
import { Subscription, timer } from 'rxjs';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

@Component({
  selector: 'app-menus',
  standalone: true,
  imports: [CommonModule, FormsModule, NotificationContainerComponent, BreadcrumbComponent, HasPermissionDirective],
  templateUrl: './menus.component.html',
  styleUrls: ['./menus.component.scss']
})
export class MenusComponent implements OnInit, OnDestroy {
  // Utility references
  Math = Math;
  document = document;

  // Subscriptions
  private subscriptions = new Subscription();

  // Data properties
  items: Menu[] = [];
  filteredItems: Menu[] = [];
  paginatedData: Menu[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: CreateMenuRequest & UpdateMenuRequest & { id?: string } = {
    label: '',
    route: '',
    icon: '',
    section: '',
    parentMenuId: '',
    sortOrder: 0,
    isActive: true,
    description: '',
    badge: '',
    badgeColor: '',
    isSystemMenu: false
  };
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: Menu | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', status: '' };
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
  levels: string[] = [];

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
  statistics: MenuStatistics = {
    total: 0,
    active: 0,
    inactive: 0,
    systemMenus: 0
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

  // Dropdown options
  sections: string[] = [];
  parentMenus: MenuDropdown[] = [];

  constructor(
    private notificationService: NotificationService,
    private menusService: MenusService,
    private commonUtility: CommonUtilityService,
    private authorizationService: AuthorizationService
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeDropdownStates();
    
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
          this.canCreate = this.authorizationService.hasPermission('Menus.Create');
          this.canUpdate = this.authorizationService.hasPermission('Menus.Update');
          this.canDelete = this.authorizationService.hasPermission('Menus.Delete');
          this.canExport = this.authorizationService.hasPermission('Menus.Export');
          this.canImport = this.authorizationService.hasPermission('Menus.Import');
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
    const fields = ['section', 'parentMenuId', 'isActive', 'filter-status', 'items-per-page'];
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

  getOptionsForField(fieldName: string): any[] {
    switch (fieldName) {
      case 'section': return this.sections;
      case 'parentMenuId': return this.parentMenus; // Return menu objects, not just IDs
      case 'isActive': return ['true', 'false'];
      case 'filter-status': return ['', 'true', 'false'];
      case 'items-per-page': return ['10', '25', '50', '100'];
      default: return [];
    }
  }

  // Data loading
  loadData(): void {
    this.isLoading = true;

    // Convert empty string to undefined for parentMenuId to avoid cache issues
    const parentMenuId = this.filters.parentMenuId && this.filters.parentMenuId.trim() !== '' 
      ? this.filters.parentMenuId 
      : undefined;

    const sub = this.menusService.getMenus(
      this.searchQuery,
      this.filters.section,
      parentMenuId,
      this.filters.isActive,
      this.filters.createdFrom,
      this.filters.createdTo,
      this.currentPage,
      this.itemsPerPage,
      this.sortField,
      this.sortDirection
    ).subscribe({
      next: (response: any) => {
        this.items = response.items?.map((item: Menu) => ({
          ...item,
          showDropdown: false,
          dropdownUp: false
        })) || [];
        this.totalItems = response.totalCount || 0;
        this.totalPages = Math.ceil(this.totalItems / this.itemsPerPage);
        this.filteredItems = [...this.items];
        this.paginatedData = [...this.items];
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading menus:', error);
        this.notificationService.error('Error', 'Failed to load menus. Please try again.');
        this.isLoading = false;
      }
    });
    this.subscriptions.add(sub);
  }

  loadStatistics(): void {
    const sub = this.menusService.getStatistics().subscribe({
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
    const sectionsSub = this.menusService.getSections().subscribe({
      next: (sections) => {
        this.sections = sections || [];
      },
      error: (error: any) => {
        console.error('Error loading sections:', error);
      }
    });
    this.subscriptions.add(sectionsSub);

    const dropdownSub = this.menusService.getDropdownOptions().subscribe({
      next: (menus) => {
        this.parentMenus = menus || [];
      },
      error: (error: any) => {
        console.error('Error loading dropdown options:', error);
      }
    });
    this.subscriptions.add(dropdownSub);
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
    this.filters = { section: '', parentMenuId: '', isActive: undefined, createdFrom: '', createdTo: '' };
    this.filterFormData = { section: '', parentMenuId: '', status: '', createdFrom: '', createdTo: '' };
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
    if (this.filters.section) count++;
    if (this.filters.parentMenuId) count++;
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
      label: '',
      route: '',
      icon: '',
      section: '',
      parentMenuId: '',
      sortOrder: 0,
      isActive: true,
      description: '',
      badge: '',
      badgeColor: '',
      isSystemMenu: false
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openEditDialog(item: Menu): void {
    this.dialogMode = 'edit';
    this.selectedItem = item;
    this.formData = {
      id: item.id,
      label: item.label,
      route: item.route,
      icon: item.icon,
      section: item.section || '',
      parentMenuId: item.parentMenuId || '',
      sortOrder: item.sortOrder,
      isActive: item.isActive,
      description: item.description || '',
      badge: item.badge || '',
      badgeColor: item.badgeColor || ''
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: Menu): void {
    const sub = this.menusService.getMenuById(item.id).subscribe({
      next: (menuDetail: Menu) => {
        this.selectedItem = menuDetail;
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading menu details:', error);
        this.selectedItem = item;
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    this.subscriptions.add(sub);
  }

  openDeleteDialog(items: Menu[]): void {
    this.selectedItems.clear();
    items.forEach(item => this.selectedItems.add(item.id));
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
      const createRequest: CreateMenuRequest = {
        label: this.formData.label,
        route: this.formData.route,
        icon: this.formData.icon,
        section: this.formData.section || undefined,
        parentMenuId: this.formData.parentMenuId || undefined,
        sortOrder: this.formData.sortOrder,
        isActive: this.formData.isActive,
        description: this.formData.description || undefined,
        badge: this.formData.badge || undefined,
        badgeColor: this.formData.badgeColor || undefined,
        isSystemMenu: this.formData.isSystemMenu || false
      };

      this.menusService.create(createRequest).subscribe({
        next: (newMenu: Menu) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Menu created successfully!');
          this.isSubmitting = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error creating menu:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to create menu. Please try again.');
          this.isSubmitting = false;
        }
      });
    } else {
      const updateRequest: UpdateMenuRequest = {
        label: this.formData.label,
        route: this.formData.route,
        icon: this.formData.icon,
        section: this.formData.section || undefined,
        parentMenuId: this.formData.parentMenuId || undefined,
        sortOrder: this.formData.sortOrder,
        isActive: this.formData.isActive,
        description: this.formData.description || undefined,
        badge: this.formData.badge || undefined,
        badgeColor: this.formData.badgeColor || undefined
      };

      this.menusService.update(this.formData.id!, updateRequest).subscribe({
        next: (updatedMenu: Menu) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Menu updated successfully!');
          this.isSubmitting = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error updating menu:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to update menu. Please try again.');
          this.isSubmitting = false;
        }
      });
    }
  }

  confirmDelete(): void {
    const itemsToDelete = Array.from(this.selectedItems);

    if (itemsToDelete.length === 1) {
      this.menusService.delete(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', 'Menu deleted successfully!');
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting menu:', error);
          const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete menu. Please try again.';
          this.notificationService.error('Error', errorMessage);
        }
      });
    } else {
      const ids = itemsToDelete.map((id: string) => id);
      this.menusService.bulkDelete(ids).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', `${itemsToDelete.length} menus deleted successfully`);
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting menus:', error);
          const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete menus. Please try again.';
          this.notificationService.error('Error', errorMessage);
        }
      });
    }
  }

  cloneMenu(menu: Menu): void {
    if (!this.canCreate) {
      this.notificationService.error('Access Denied', 'You do not have permission to clone menus.');
      return;
    }
    this.notificationService.info('Info', 'Cloning menu...');
    this.menusService.cloneMultiple([menu.id]).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || 'Menu cloned successfully');
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone menu');
      }
    });
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notificationService.warning('Warning', 'Please select at least one menu to clone');
      return;
    }

    if (!this.canCreate) {
      this.notificationService.error('Access Denied', 'You do not have permission to clone menus.');
      return;
    }

    this.notificationService.info('Info', 'Cloning menus...');
    this.menusService.cloneMultiple(selectedIds).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || `${selectedIds.length} menu(s) cloned successfully`);
        this.selectedItems.clear();
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone menus');
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

    // Helper function to clean empty strings to null/undefined
    const cleanValue = (value: any): any => {
      if (value === '' || value === null || value === undefined) {
        return null;
      }
      return value;
    };

    // Helper function to clean GUID strings
    const cleanGuid = (value: any): string | null => {
      if (!value || value === '' || value.trim() === '') {
        return null;
      }
      // Validate GUID format
      const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
      if (typeof value === 'string' && guidRegex.test(value)) {
        return value;
      }
      return null;
    };

    const params: any = {
      format: formatMap[format],
      search: cleanValue(this.searchQuery),
      section: cleanValue(this.filters.section),
      parentMenuId: cleanGuid(this.filters.parentMenuId),
      isActive: this.filters.isActive !== undefined ? this.filters.isActive : null,
      createdFrom: cleanValue(this.filters.createdFrom),
      createdTo: cleanValue(this.filters.createdTo)
    };

    if (this.selectedItems.size > 0) {
      params.selectedIds = Array.from(this.selectedItems);
    }

    this.isExporting = true;
    this.exportProgress = 0;
    this.exportStatus = 'Pending';
    this.showExportDialog = true;

    this.menusService.startExport(formatMap[format].toString(), params).subscribe({
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

      this.menusService.getExportJobStatus(this.exportJobId).subscribe({
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
    this.menusService.downloadExport(jobId).subscribe({
      next: (blob: Blob) => {
        const extension = this.getFileExtension(format);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `menus_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.menusService.getImportTemplate().subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'menus_import_template.xlsx';
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

    this.menusService.startImport(this.selectedFile!).subscribe({
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

      this.menusService.getImportJobStatus(this.importJobId).subscribe({
        next: (status: ImportJobStatus) => {
          this.importStatus = status.status;
          this.importProgress = status.progressPercent ?? 0;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isImporting = false;
            this.importJobId = null;

            if (status.status === 'Completed') {
              this.notificationService.success('Import Completed', `Imported ${status.successCount} menus${status.skippedCount ? `, skipped ${status.skippedCount}` : ''}${status.errorCount ? `, errors ${status.errorCount}` : ''}.`);
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

    this.menusService.getImportErrorReport(this.errorReportId).subscribe({
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

    this.menusService.getImportErrorReport(history.errorReportId).subscribe({
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

    this.menusService.downloadExport(history.jobId).subscribe({
      next: (blob: Blob) => {
        const extension = this.getFileExtension(history.format);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = history.fileName || `menus_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.menusService.getHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
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

  toggleActiveStatus(menu: Menu): void {
    const newStatus = !menu.isActive;
    this.menusService.setActive(menu.id, newStatus).subscribe({
      next: () => {
        this.notificationService.success('Success', `Menu ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        console.error('Error toggling active status:', error);
        this.notificationService.error('Error', 'Failed to update menu status.');
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
  toggleDropdown(item: Menu, event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }
    
    this.exportDropdownOpen = false;

    this.items.forEach((dataItem: Menu) => {
      if (dataItem.id !== item.id) {
        dataItem.showDropdown = false;
      }
    });

    item.showDropdown = !item.showDropdown;
  }

  closeAllDropdowns(): void {
    this.items.forEach((item: Menu) => {
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
    
    if (fieldName === 'parentMenuId') {
      // For parentMenuId, filter menu objects by label
      const searchLower = (searchTerm || '').toLowerCase();
      this.dropdownStates[fieldName].filteredOptions = allOptions.filter((menu: MenuDropdown) =>
        menu.label.toLowerCase().includes(searchLower)
      );
    } else {
      // For other fields, use the common utility
      this.dropdownStates[fieldName].filteredOptions = this.commonUtility.filterDropdownOptions(allOptions, searchTerm);
    }
  }

  selectOption(fieldName: string, option: string): void {
    this.commonUtility.selectFormOption(this.formData, fieldName, option);
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
    if (fieldName === 'parentMenuId') {
      const parentMenuId = (this.formData as any)[fieldName];
      if (!parentMenuId) return '';
      const menu = this.parentMenus.find(m => m.id === parentMenuId);
      return menu ? menu.label : '';
    }
    return this.commonUtility.getDropdownDisplayValue((this.formData as any)[fieldName]);
  }

  getParentMenuName(parentMenuId: string | undefined): string {
    if (!parentMenuId) return '-';
    const menu = this.parentMenus.find(m => m.id === parentMenuId);
    return menu ? menu.label : parentMenuId;
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

  clearDropdownValue(fieldName: string, event?: Event): void {
    if (event) {
      event.stopPropagation();
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

  getSelectedItems(): Menu[] {
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

    if (!this.formData.label || this.formData.label.trim() === '') {
      this.errors['label'] = 'Label is required';
    }
    if (!this.formData.route || this.formData.route.trim() === '') {
      this.errors['route'] = 'Route is required';
    }
    if (!this.formData.icon || this.formData.icon.trim() === '') {
      this.errors['icon'] = 'Icon is required';
    }
  }

  validateField(fieldName: string): void {
    switch (fieldName) {
      case 'label':
        if (!this.formData.label || this.formData.label.trim() === '') {
          this.errors['label'] = 'Label is required';
        } else {
          delete this.errors['label'];
        }
        break;
      case 'route':
        if (!this.formData.route || this.formData.route.trim() === '') {
          this.errors['route'] = 'Route is required';
        } else {
          delete this.errors['route'];
        }
        break;
      case 'icon':
        if (!this.formData.icon || this.formData.icon.trim() === '') {
          this.errors['icon'] = 'Icon is required';
        } else {
          delete this.errors['icon'];
        }
        break;
    }
  }

  private markAllFieldsAsTouched(): void {
    const fields = ['label', 'route', 'icon'];
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
  exportMenuDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notificationService.warning('Warning', 'No menu selected to export');
      return;
    }

    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const menuDetailsCards = document.querySelector('.modal-overlay .modal .profile-form') as HTMLElement;

    if (!modalBody || !menuDetailsCards) {
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
      html2canvas(menuDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: true,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: menuDetailsCards.scrollWidth,
        windowHeight: menuDetailsCards.scrollHeight
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

      pdf.save(`menu-details-${this.selectedItem?.label.replace(/\s+/g, '-').toLowerCase()}.pdf`);

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


