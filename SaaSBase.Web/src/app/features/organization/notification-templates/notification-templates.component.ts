import { Component, OnInit, Input, OnChanges, SimpleChanges, OnDestroy, HostListener, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { NotificationTemplate, OrganizationService, ImportJobStatus, ExportJobStatus, ImportExportHistory, ImportExportHistoryResponse } from '../../../core/services/organization.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

@Component({
  selector: 'app-notification-templates',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './notification-templates.component.html',
  styleUrls: ['./notification-templates.component.scss']
})
export class NotificationTemplatesComponent implements OnInit, OnChanges, OnDestroy {
  // Utility references
  Math = Math;
  document = document;
  parseInt = parseInt;
  
  // Placeholder text for variables field
  variablesPlaceholder = 'Example: ["name", "email"]';

  // Subscriptions
  private subscriptions = new Subscription();

  @Input() organizationId: string = '';
  items: NotificationTemplate[] = [];
  filteredItems: NotificationTemplate[] = [];
  paginatedData: NotificationTemplate[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: Partial<NotificationTemplate> = {
    name: '',
    subject: '',
    body: '',
    templateType: '',
    description: '',
    category: '',
    variables: '',
    isActive: true
  };
  notificationTemplateForm!: FormGroup;
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: NotificationTemplate | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '', status: '', templateType: '', category: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', status: '', templateType: '', category: '' };
  showFilters = false;
  searchQuery = '';
  searchTerm = ''; // For template binding (matching roles pattern)
  private searchTimeout: any;
  isActiveFilter: boolean | null = null;

  // Pagination
  currentPage = 1;
  itemsPerPage = 10;
  pageSize = 10; // For template binding (matching roles pattern)
  totalItems = 0;
  totalCount = 0; // For template binding (matching roles pattern)
  totalPages = 1;
  itemsPerPageFormData: number = 10;

  // Sorting
  sortField = 'createdAtUtc';
  sortDirection: 'asc' | 'desc' = 'desc';

  // Dropdowns
  dropdownStates: { [key: string]: any } = {};
  exportDropdownOpen = false;

  // Import properties
  selectedFile: File | null = null;
  importErrors: string[] = [];
  isImporting = false;
  importJobId: string | null = null;
  importProgress = 0;
  importStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed' | null = null;
  errorReportId: string | null = null;

  // Export job tracking
  exportJobId: string | null = null;
  exportProgress = 0;
  exportStatus: 'Pending' | 'Processing' | 'Completed' | 'Failed' | null = null;
  exportFormat: string | null = null;
  isExporting = false;
  showExportDialog = false;

  // Unified history properties (matching roles pattern)
  history: ImportExportHistory[] = [];
  historyTotalCount = 0;
  historyPage = 1;
  historyPageSize = 10;
  historyType: 'import' | 'export' | undefined = undefined; // undefined = show both
  isLoadingHistory = false;

  // Loading states
  isLoading = false;
  loading = false; // For template binding (matching roles pattern)
  isSubmitting = false;
  isMobile = false;

  // Error handling
  errors: { [key: string]: string } = {};

  // Statistics
  statistics: { total: number; active: number; inactive: number } = {
    total: 0,
    active: 0,
    inactive: 0
  };

  // Available options
  templateTypes: string[] = ['EMAIL', 'SMS', 'PUSH', 'IN_APP'];
  categories: string[] = ['ORDER', 'INVENTORY', 'USER', 'SYSTEM', 'PAYMENT', 'SHIPPING'];

  constructor(
    private orgService: OrganizationService,
    private notification: NotificationService,
    private commonUtility: CommonUtilityService,
    private cdr: ChangeDetectorRef,
    private fb: FormBuilder
  ) {
    this.initializeForm();
  }

  private initializeForm(): void {
    this.notificationTemplateForm = this.fb.group({
      name: ['', Validators.required],
      subject: ['', Validators.required],
      body: ['', Validators.required],
      templateType: ['', Validators.required],
      description: [''],
      category: [''],
      variables: [''],
      isActive: [true]
    });
  }

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeDropdownStates();
    // Don't load data here - wait for ngOnChanges to handle it
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['organizationId'] && this.organizationId) {
      const orgIdChange = changes['organizationId'];
      // Only load if this is the first time (previousValue is undefined) or if the value actually changed
      if (!orgIdChange.previousValue || orgIdChange.previousValue !== orgIdChange.currentValue) {
        // Load filter options and data
        this.loadFilterOptions();
        this.loadData();
      }
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
  }

  @HostListener('window:resize', ['$event'])
  onResize(): void {
    this.checkScreenSize();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.searchable-dropdown') && !target.closest('.dropdown-toggle') && !target.closest('.dropdown-menu') && !target.closest('.action-dropdown')) {
      this.closeAllSearchableDropdowns();
      this.exportDropdownOpen = false;
      this.closeAllDropdowns();
    }
  }

  private checkScreenSize(): void {
    this.isMobile = this.commonUtility.isMobile();
  }

  initializeDropdownStates(): void {
    const fields = ['filter-status', 'filter-templateType', 'filter-category', 'templateType', 'category', 'items-per-page'];
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
      case 'filter-status':
        return ['', 'true', 'false'];
      case 'filter-templateType':
      case 'templateType':
        return this.templateTypes;
      case 'filter-category':
      case 'category':
        return this.categories;
      case 'items-per-page':
        return ['10', '25', '50', '100'];
      default:
        return [];
    }
  }

  // Data loading (matching roles pattern exactly)
  loadData(): void {
    this.isLoading = true;
    this.loading = true;

    const params: any = {
      page: this.currentPage,
      pageSize: this.itemsPerPage,
      search: this.searchQuery,
      sortField: this.sortField,
      sortDirection: this.sortDirection,
      templateType: this.filters.templateType || undefined,
      category: this.filters.category || undefined,
      isActive: this.filters.isActive !== undefined ? this.filters.isActive : undefined
    };

    if (this.filters.createdFrom) {
      params.createdFrom = new Date(this.filters.createdFrom);
    }
    if (this.filters.createdTo) {
      params.createdTo = new Date(this.filters.createdTo);
    }

    // Add organizationId if provided (for system admin viewing another organization)
    if (this.organizationId) {
      params.organizationId = this.organizationId;
    }

    const sub = this.orgService.getNotificationTemplates(params).subscribe({
      next: (response: any) => {
        const items = Array.isArray(response?.items) ? response.items : (Array.isArray(response) ? response : []);
        this.items = items.map((item: NotificationTemplate) => ({
          ...item,
          showDropdown: false,
          dropdownUp: false
        }));
        this.totalItems = response?.totalCount || items.length;
        this.totalCount = response?.totalCount || items.length;
        this.totalPages = response?.totalPages || Math.ceil(this.totalItems / this.itemsPerPage);
        this.filteredItems = [...this.items];
        this.paginatedData = [...this.items];
        
        // Update statistics after data is loaded
        this.updateStatistics();
        
        this.isLoading = false;
        this.loading = false;

        // Force change detection
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Error loading notification templates:', error);
        this.notification.error('Error', 'Failed to load notification templates. Please try again.');
        this.isLoading = false;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
    this.subscriptions.add(sub);
  }

  loadStatistics(): void {
    // This method can be called independently, but it will use current totalItems
    this.updateStatistics();
  }

  private updateStatistics(): void {
    // Calculate statistics from total items count, not just paginated items
    this.statistics = {
      total: this.totalItems, // Use totalItems which comes from API response
      active: this.items.filter(item => item.isActive).length,
      inactive: this.items.filter(item => !item.isActive).length
    };
  }

  loadDropdownOptions(): void {
    // This method is kept for backward compatibility but filter options now come from backend
    // Filter options are loaded separately via loadFilterOptions()
  }

  loadFilterOptions(): void {
    // Load filter options from backend
    this.orgService.getNotificationTemplateCategories().subscribe({
      next: (categories) => {
        this.categories = categories;
      },
      error: () => {
        // Fallback to default list if API fails
        this.categories = ['ORDER', 'INVENTORY', 'USER', 'SYSTEM', 'PAYMENT', 'SHIPPING'];
      }
    });
  }

  // Filter & Search
  applyFilters(): void {
    this.currentPage = 1;
    this.loadData();
  }

  onSearch(): void {
    this.searchQuery = this.searchTerm;
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
    this.searchTerm = '';
    this.filters = { createdFrom: '', createdTo: '', status: '', templateType: '', category: '' };
    this.filterFormData = { createdFrom: '', createdTo: '', status: '', templateType: '', category: '' };
    this.isActiveFilter = null;
    this.applyFilters();
  }

  refreshData(): void {
    
    this.loadData();
    this.loadStatistics();
  }

  get activeFilterCount(): number {
    let count = 0;
    if (this.searchQuery) count++;
    if (this.filters.status) count++;
    if (this.filters.templateType) count++;
    if (this.filters.category) count++;
    if (this.filters.createdFrom) count++;
    if (this.filters.createdTo) count++;
    return count;
  }

  getActiveFilterCount(): number {
    return this.activeFilterCount;
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  onMainPageClick(event: Event): void {
    // Close dropdowns when clicking on main page
    const target = event.target as HTMLElement;
    if (!target.closest('.searchable-dropdown') && !target.closest('.dropdown-toggle') && !target.closest('.dropdown-menu')) {
      this.closeAllSearchableDropdowns();
    }
  }

  loadRoles(): void {
    this.refreshData();
  }

  getTotalPages(): number {
    return this.totalPages;
  }

  // Pagination & Sorting
  updatePagination(): void {
    const start = (this.currentPage - 1) * this.itemsPerPage;
    const end = start + this.itemsPerPage;
    this.paginatedData = this.filteredItems.slice(start, end);
    this.totalPages = Math.ceil(this.totalItems / this.itemsPerPage);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.loadData();
  }

  onPageSizeChange(newSize?: number): void {
    if (newSize) {
      this.itemsPerPage = newSize;
      this.pageSize = newSize;
    } else {
      this.itemsPerPage = this.itemsPerPageFormData;
      this.pageSize = this.itemsPerPageFormData;
    }
    this.currentPage = 1;
    this.loadData();
  }

  selectItemsPerPageOption(option: string): void {
    this.itemsPerPageFormData = parseInt(option);
    this.itemsPerPage = parseInt(option);
    this.pageSize = parseInt(option);
    this.closeAllSearchableDropdowns();
    this.onPageSizeChange();
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

  onSort(field: string): void {
    this.sortData(field);
  }

  resetSortToDefault(): void {
    this.sortField = 'createdAtUtc';
    this.sortDirection = 'desc';
    this.currentPage = 1;
  }

  // Dialog Management
  openAddDialog(): void {
    this.dialogMode = 'add';
    this.notificationTemplateForm.reset({
      name: '',
      subject: '',
      body: '',
      templateType: '',
      description: '',
      category: '',
      variables: '',
      isActive: true
    });
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openEditDialog(item: NotificationTemplate): void {
    this.dialogMode = 'edit';
    this.selectedItem = item;
    this.notificationTemplateForm.patchValue({
      name: item.name || '',
      subject: item.subject || '',
      body: item.body || '',
      templateType: item.templateType || '',
      description: item.description || '',
      category: item.category || '',
      variables: item.variables || '',
      isActive: item.isActive !== undefined ? item.isActive : true
    });
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: NotificationTemplate): void {
    // Load full notification template details with metadata
    const sub = this.orgService.getNotificationTemplate(item.id).subscribe({
      next: (fullTemplate) => {
        this.selectedItem = fullTemplate;
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading notification template details:', error);
        // Fallback to list data if API fails
        this.selectedItem = item;
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    this.subscriptions.add(sub);
  }

  openEditDialogFromView(item: NotificationTemplate): void {
    this.showViewDialog = false;
    this.openEditDialog(item);
  }

  exportNotificationTemplateDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notification.warning('Warning', 'No notification template selected to export');
      return;
    }

    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const templateDetailsCards = document.querySelector('.modal-overlay .modal .profile-form') as HTMLElement;

    if (!modalBody || !templateDetailsCards) {
      this.notification.error('Error', 'Unable to find content to export');
      return;
    }

    this.notification.info('Info', 'Generating PDF...');

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
      html2canvas(templateDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: true,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: templateDetailsCards.scrollWidth,
        windowHeight: templateDetailsCards.scrollHeight
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

        pdf.save(`notification-template-details-${this.selectedItem?.name.replace(/\s+/g, '-').toLowerCase()}.pdf`);
        this.notification.success('Success', 'PDF exported successfully');
      }).catch((error: any) => {
        modalBody.style.overflow = originalModalBodyOverflow;
        modalBody.style.height = originalModalBodyHeight;
        modalBody.style.maxHeight = originalModalBodyMaxHeight;
        if (modalActions) {
          modalActions.style.display = originalModalActionsDisplay;
        }

        console.error('PDF export error:', error);
        this.notification.error('Error', 'Failed to export PDF. Please try again.');
      });
    }, 100);
  }

  openDeleteDialog(items: NotificationTemplate[]): void {
    this.selectedItems.clear();
    items.forEach(item => this.selectedItems.add(item.id));
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notification.warning('Warning', 'Please select at least one template to clone');
      return;
    }

    this.notification.info('Info', 'Cloning templates...');
    this.orgService.cloneMultipleNotificationTemplates(selectedIds).subscribe({
      next: (response) => {
        this.notification.success('Success', response.message || `${selectedIds.length} template(s) cloned successfully`);
        this.selectedItems.clear();
        this.resetSortToDefault();
        this.loadData();
      },
      error: (error) => {
        this.notification.error('Error', error.error?.message || 'Failed to clone notification templates');
      }
    });
  }

  cloneNotificationTemplate(item: NotificationTemplate): void {
    this.notification.info('Info', 'Cloning notification template...');
    this.orgService.cloneMultipleNotificationTemplates([item.id]).subscribe({
      next: (response) => {
        this.notification.success('Success', response.message || 'Notification template cloned successfully');
        this.resetSortToDefault();
        this.loadData();
      },
      error: (error) => {
        this.notification.error('Error', error.error?.message || 'Failed to clone notification template');
      }
    });
  }

  closeDropdown(item: NotificationTemplate): void {
    this.dropdownStates[item.id] = { isOpen: false };
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
    if (this.notificationTemplateForm.invalid) {
      this.notificationTemplateForm.markAllAsTouched();
      this.notification.warning('Validation', 'Please fill in all required fields');
      return;
    }

    this.isSubmitting = true;
    const formValue = this.notificationTemplateForm.value;

    if (this.dialogMode === 'add') {
      this.orgService.createNotificationTemplate(formValue).subscribe({
        next: () => {
          this.closeDialogs();
          this.notification.success('Success', 'Notification template created successfully!');
          this.isSubmitting = false;
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          this.notification.error('Error', error.error?.message || 'Failed to create notification template');
          this.isSubmitting = false;
        }
      });
    } else if (this.selectedItem?.id) {
      this.orgService.updateNotificationTemplate(this.selectedItem.id, formValue).subscribe({
        next: () => {
          this.closeDialogs();
          this.notification.success('Success', 'Notification template updated successfully!');
          this.isSubmitting = false;
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          this.notification.error('Error', error.error?.message || 'Failed to update notification template');
          this.isSubmitting = false;
        }
      });
    }
  }

  confirmDelete(): void {
    const itemsToDelete = Array.from(this.selectedItems);
    if (itemsToDelete.length === 0) return;

    if (itemsToDelete.length === 1) {
      this.orgService.deleteNotificationTemplate(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notification.success('Success', 'Notification template deleted successfully!');
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          this.notification.error('Error', error.error?.message || 'Failed to delete notification template');
        }
      });
    } else {
      let deletedCount = 0;
      const totalCount = itemsToDelete.length;
      
      itemsToDelete.forEach(id => {
        this.orgService.deleteNotificationTemplate(id).subscribe({
          next: () => {
            deletedCount++;
            if (deletedCount === totalCount) {
              this.selectedItems.clear();
              this.closeDialogs();
              this.notification.success('Success', `${totalCount} templates deleted successfully`);
              this.resetSortToDefault();
              this.loadData();
              this.loadStatistics();
            }
          },
          error: () => {
            deletedCount++;
            if (deletedCount === totalCount) {
              this.selectedItems.clear();
              this.closeDialogs();
              this.notification.warning('Warning', 'Some templates could not be deleted');
              this.resetSortToDefault();
              this.loadData();
              this.loadStatistics();
            }
          }
        });
      });
    }
  }

  // Export functionality
  toggleExportDropdown(): void {
    this.closeAllDropdowns();
    this.exportDropdownOpen = !this.exportDropdownOpen;
  }

  exportNotificationTemplates(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
    this.exportDropdownOpen = false;
    this.isExporting = true;
    this.exportStatus = 'Pending';
    this.exportProgress = 0;
    this.showExportDialog = true;

    const formatMap: { [key: string]: number } = {
      'excel': 1,
      'csv': 2,
      'pdf': 3,
      'json': 4
    };

    const exportParams: any = {
      format: formatMap[format] || 1,
      search: this.searchQuery || undefined,
      isActive: this.filters.isActive !== undefined ? this.filters.isActive : undefined,
      category: this.filters.category || undefined,
      templateType: this.filters.templateType || undefined,
      createdFrom: this.filters.createdFrom ? new Date(this.filters.createdFrom).toISOString() : undefined,
      createdTo: this.filters.createdTo ? new Date(this.filters.createdTo).toISOString() : undefined
    };

    if (this.selectedItems.size > 0) {
      exportParams.selectedIds = Array.from(this.selectedItems);
    }

    this.orgService.startNotificationTemplateExportAsync(exportParams).subscribe({
      next: (response: { jobId: string }) => {
        this.exportJobId = response.jobId;
        this.exportFormat = format;
        this.exportStatus = 'Processing';
        this.exportProgress = 10;
        this.pollExportStatus();
      },
      error: (error: any) => {
        this.isExporting = false;
        this.exportStatus = 'Failed';
        this.notification.error('Error', error.error?.message || 'Failed to start export');
      }
    });
  }

  pollExportStatus(): void {
    if (!this.exportJobId) return;

    const checkStatus = () => {
      this.orgService.getNotificationTemplateExportJobStatus(this.exportJobId!).subscribe({
        next: (status: any) => {
          this.exportProgress = status.progressPercent || 0;
          this.exportStatus = status.status;

          if (status.status === 'Completed') {
            this.downloadExportFile();
          } else if (status.status === 'Failed') {
            this.isExporting = false;
            this.notification.error('Error', status.errorMessage || 'Export failed');
          } else {
            setTimeout(checkStatus, 2000);
          }
        },
        error: () => {
          this.isExporting = false;
          this.exportStatus = 'Failed';
          this.notification.error('Error', 'Failed to check export status');
        }
      });
    };

    checkStatus();
  }

  downloadExportFile(): void {
    if (!this.exportJobId) return;

    this.orgService.downloadNotificationTemplateExport(this.exportJobId).subscribe({
      next: (blob: Blob) => {
        const format = this.exportFormat || 'excel';
        const extension = format === 'excel' ? 'xlsx' : format === 'csv' ? 'csv' : format === 'pdf' ? 'pdf' : 'json';
        const filename = `notification_templates_export_${new Date().toISOString().split('T')[0]}.${extension}`;
        
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        this.isExporting = false;
        this.exportStatus = 'Completed';
        this.exportProgress = 100;
        this.showExportDialog = false;
        this.exportJobId = null;
        this.notification.success('Success', 'Export completed and downloaded successfully');
      },
      error: () => {
        this.isExporting = false;
        this.exportStatus = 'Failed';
        this.notification.error('Error', 'Failed to download export file');
      }
    });
  }

  // Import functionality
  downloadTemplate(): void {
    this.orgService.getNotificationTemplateTemplate().subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'notification_template_import_template.xlsx';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        this.notification.success('Success', 'Template downloaded successfully');
      },
      error: () => {
        this.notification.error('Error', 'Failed to download template');
      }
    });
  }

  onFileSelected(event: any): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;
    this.validateAndSetFile(input.files[0]);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    event.dataTransfer!.dropEffect = 'copy';
  }

  onDragEnter(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const files = event.dataTransfer?.files;
    if (!files || files.length === 0) return;
    this.validateAndSetFile(files[0]);
  }

  private validateAndSetFile(file: File): void {
    const validTypes = ['application/vnd.ms-excel', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'text/csv'];
    const validExtensions = ['.csv', '.xlsx', '.xls'];
    const fileExtension = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

    if (!validTypes.includes(file.type) && !validExtensions.includes(fileExtension)) {
      this.notification.warning('Invalid File', 'Please upload a valid Excel (.xlsx, .xls) or CSV (.csv) file');
      return;
    }

    const maxSize = 5 * 1024 * 1024;
    if (file.size > maxSize) {
      this.notification.warning('File Too Large', `File size should not exceed 5MB. Your file is ${(file.size / (1024 * 1024)).toFixed(2)}MB`);
      return;
    }

    this.selectedFile = file;
    this.importErrors = [];
  }

  importData(): void {
    if (!this.selectedFile) {
      this.notification.warning('Warning', 'Please select a file to import');
      return;
    }

    this.isImporting = true;
    this.importErrors = [];
    this.importStatus = 'Pending';
    this.importProgress = 0;
    this.errorReportId = null;

    const formData = new FormData();
    formData.append('file', this.selectedFile);
    
    this.orgService.startNotificationTemplateImportAsync(formData).subscribe({
      next: (response: { jobId: string }) => {
        this.importJobId = response.jobId;
        this.importStatus = 'Processing';
        this.importProgress = 10;
        this.pollImportStatus();
      },
      error: (error: any) => {
        this.isImporting = false;
        this.importStatus = 'Failed';
        this.notification.error('Error', error.error?.message || 'Failed to start import');
      }
    });
  }

  pollImportStatus(): void {
    if (!this.importJobId) return;

    const checkStatus = () => {
      this.orgService.getNotificationTemplateImportJobStatus(this.importJobId!).subscribe({
        next: (status: any) => {
          this.importProgress = status.progressPercent || 0;
          this.importStatus = status.status;

          if (status.status === 'Completed') {
            this.isImporting = false;
            this.importProgress = 100;
            const successCount = status.successCount || 0;
            const errorCount = status.errorCount || 0;
            this.errorReportId = status.errorReportId || null;
            
            if (errorCount > 0 && this.errorReportId) {
              this.notification.warning('Import Completed', `Imported ${successCount} template(s), ${errorCount} error(s). Click to download error report.`);
            } else {
              this.notification.success('Import Completed', `Successfully imported ${successCount} template(s)`);
            }
            
            this.closeImportDialog();
            this.loadData();
            this.loadHistory();
          } else if (status.status === 'Failed') {
            this.isImporting = false;
            this.notification.error('Error', status.errorMessage || 'Import failed');
          } else {
            setTimeout(checkStatus, 2000);
          }
        },
        error: () => {
          this.isImporting = false;
          this.importStatus = 'Failed';
          this.notification.error('Error', 'Failed to check import status');
        }
      });
    };

    checkStatus();
  }

  downloadErrorReport(): void {
    if (!this.errorReportId) return;

    this.orgService.getNotificationTemplateImportErrorReport(this.errorReportId).subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `notification_template_import_errors_${new Date().toISOString().split('T')[0]}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        this.notification.success('Success', 'Error report downloaded');
      },
      error: () => {
        this.notification.error('Error', 'Failed to download error report');
      }
    });
  }

  // History
  openImportHistoryDialog(): void {
    this.showImportHistoryDialog = true;
    this.historyPage = 1;
    this.historyType = undefined;
    this.loadHistory();
    this.disableBodyScroll();
  }

  loadHistory(): void {
    this.isLoadingHistory = true;
    this.orgService.getNotificationTemplateHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
      next: (response) => {
        this.history = response.items;
        this.historyTotalCount = response.totalCount;
        this.isLoadingHistory = false;
      },
      error: (error) => {
        console.error('Error loading history:', error);
        this.notification.error('Error', 'Failed to load history');
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

  isCurrentJob(history: ImportExportHistory): boolean {
    // Check if this is a very recent import/export (within last 2 minutes) and still processing
    const historyTime = new Date(history.createdAtUtc).getTime();
    const now = Date.now();
    const timeDiff = now - historyTime;
    const twoMinutes = 2 * 60 * 1000;

    // Only show progress for records that are still processing (not completed)
    const isStillProcessing = history.status === 'Processing' || history.status === 'Pending';

    return timeDiff < twoMinutes && isStillProcessing;
  }

  getHistoryStatusClass(status: string): string {
    const normalized = (status || '').toLowerCase();
    if (normalized.includes('success') || normalized === 'completed') return 'status-badge status-success';
    if (normalized.includes('fail') || normalized === 'error') return 'status-badge status-failed';
    if (normalized.includes('pending') || normalized.includes('processing')) return 'status-badge status-warning';
    return 'status-badge';
  }

  downloadExportFromHistory(historyId: string): void {
    const history = this.history.find(h => h.id === historyId);
    if (!history) {
      this.notification.warning('Warning', 'Export not found');
      return;
    }
    this.notification.info('Info', 'Download functionality will be implemented with backend API');
  }

  downloadErrorReportForHistory(historyId: string): void {
    // Find the history item to get the errorReportId
    const history = this.history.find(h => h.id === historyId);
    if (!history || !history.errorReportId) {
      this.notification.warning('Warning', 'No error report available for this import');
      return;
    }

    this.orgService.getNotificationTemplateImportErrorReport(history.errorReportId).subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `notification-templates-import-error-report-${historyId}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        this.notification.success('Success', 'Error report downloaded successfully');
      },
      error: (error) => {
        console.error('Error downloading error report:', error);
        this.notification.error('Error', 'Failed to download error report');
      }
    });
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

  // Utility Methods
  formatDate(date: string): string {
    return this.commonUtility.formatDate(date);
  }

  formatDateTime(date: string): string {
    return this.commonUtility.formatDate(date, 'en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
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

  getSelectedItems(): NotificationTemplate[] {
    return this.commonUtility.getSelectedItems(this.items, this.selectedItems);
  }

  get deleteItemCount(): number {
    return this.selectedItems.size;
  }

  // Dropdown methods
  isDropdownOpen(item: NotificationTemplate): boolean {
    return !!(item as any).showDropdown;
  }

  toggleDropdown(item: NotificationTemplate, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.exportDropdownOpen = false;
    this.items.forEach((dataItem: NotificationTemplate) => {
      if (dataItem.id !== item.id) {
        (dataItem as any).showDropdown = false;
      }
    });
    (item as any).showDropdown = !(item as any).showDropdown;
  }

  toggleActiveStatusAndClose(item: NotificationTemplate): void {
    this.toggleActiveStatus(item);
    (item as any).showDropdown = false;
  }

  deleteItemAndClose(item: NotificationTemplate): void {
    this.openDeleteDialog([item]);
    (item as any).showDropdown = false;
  }

  closeAllDropdowns(): void {
    this.items.forEach((item: NotificationTemplate) => {
      (item as any).showDropdown = false;
    });
    this.commonUtility.closeAllDropdowns(this.dropdownStates);
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

  selectOption(fieldName: string, option: string): void {
    if (fieldName.startsWith('filter-')) {
      this.selectFilterOption(fieldName.replace('filter-', ''), option);
    } else {
      this.commonUtility.selectFormOption(this.formData, fieldName, option);
      // Sync with reactive form
      if (this.notificationTemplateForm && this.notificationTemplateForm.get(fieldName)) {
        this.notificationTemplateForm.patchValue({ [fieldName]: option });
      }
    }
    this.closeAllSearchableDropdowns();
  }

  selectFilterOption(fieldName: string, option: string | boolean | null): void {
    // Special handling for status filter - convert to boolean
    if (fieldName === 'status') {
      if (option === null || option === '') {
        this.filterFormData.status = '';
        this.filters.status = '';
        this.filters.isActive = undefined;
        this.isActiveFilter = null;
      } else if (typeof option === 'boolean') {
        this.filterFormData.status = option ? 'true' : 'false';
        this.filters.status = option ? 'true' : 'false';
        this.filters.isActive = option;
        this.isActiveFilter = option;
      } else {
        this.filterFormData.status = option;
        this.filters.status = option;
        this.filters.isActive = option === 'true';
        this.isActiveFilter = option === 'true';
      }
    } else {
      this.commonUtility.selectFilterOption(this.filterFormData, this.filters, fieldName, option as string);
    }
    this.closeAllSearchableDropdowns();
    this.onFilterChange();
  }

  onSearchInput(fieldName: string, event: Event): void {
    this.commonUtility.onDropdownSearchInput(this.dropdownStates, fieldName, event, (fieldName: string) => this.filterOptions(fieldName));
  }

  getDisplayValue(fieldName: string): string {
    return this.commonUtility.getDropdownDisplayValue((this.formData as any)[fieldName]);
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
   * @param fieldName - Field name (e.g., 'filter-status', 'templateType', etc.)
   */
  clearDropdownValue(fieldName: string, event?: Event): void {
    if (event) {
      event.stopPropagation(); // Prevent dropdown from opening/closing
    }

    if (fieldName.startsWith('filter-')) {
      const actualField = fieldName.replace('filter-', '');
      if (actualField === 'status') {
        this.filterFormData.status = '';
        this.filters.status = '';
        this.filters.isActive = undefined;
        this.isActiveFilter = null;
      } else {
        this.filterFormData[actualField] = '';
        this.filters[actualField] = '';
      }
      this.onFilterChange();
    } else {
      // Form dropdowns
      this.formData[fieldName as keyof typeof this.formData] = '' as any;
      if (this.notificationTemplateForm && this.notificationTemplateForm.get(fieldName)) {
        this.notificationTemplateForm.patchValue({ [fieldName]: '' });
      }
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
        return this.isActiveFilter !== null;
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
    if (target.classList.contains('modal-body') || target.classList.contains('form-section') || target.classList.contains('form-sections')) {
      this.closeAllSearchableDropdowns();
    }
  }

  toggleActiveStatus(item: NotificationTemplate): void {
    const newStatus = !item.isActive;
    this.orgService.updateNotificationTemplate(item.id, { ...item, isActive: newStatus }).subscribe({
      next: () => {
        this.notification.success('Success', `Template ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.resetSortToDefault();
        this.loadData();
        this.loadStatistics();
      },
      error: () => {
        this.notification.error('Error', 'Failed to update template status');
      }
    });
  }

  triggerFileInput(): void {
    const fileInput = document.getElementById('fileInput') as HTMLInputElement;
    if (fileInput) fileInput.click();
  }

  removeSelectedFile(): void {
    this.selectedFile = null;
    this.importErrors = [];
  }

  // Legacy methods for backward compatibility
  openAdd(): void {
    this.openAddDialog();
  }

  openEdit(item: NotificationTemplate): void {
    this.openEditDialog(item);
  }

  close(): void {
    this.closeDialogs();
  }

  save(): void {
    this.saveItem();
  }

  remove(item: NotificationTemplate): void {
    this.openDeleteDialog([item]);
  }
}
