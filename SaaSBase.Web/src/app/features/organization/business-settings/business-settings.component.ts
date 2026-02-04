import { Component, OnInit, Input, OnChanges, SimpleChanges, OnDestroy, HostListener, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { BusinessSetting, OrganizationService, ImportJobStatus, ExportJobStatus, ImportExportHistory, ImportExportHistoryResponse } from '../../../core/services/organization.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

@Component({
  selector: 'app-business-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './business-settings.component.html',
  styleUrls: ['./business-settings.component.scss']
})
export class BusinessSettingsComponent implements OnInit, OnChanges, OnDestroy {
  // Utility references
  Math = Math;
  document = document;
  parseInt = parseInt;

  // Subscriptions
  private subscriptions = new Subscription();

  @Input() organizationId: string = '';
  items: BusinessSetting[] = [];
  filteredItems: BusinessSetting[] = [];
  paginatedData: BusinessSetting[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: Partial<BusinessSetting> = {
    settingKey: '',
    settingValue: '',
    description: '',
    isActive: true
  };
  businessSettingForm!: FormGroup;
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: BusinessSetting | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '', status: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', status: '' };
  showFilters = false;
  searchQuery = '';
  private searchTimeout: any;
  isActiveFilter: boolean | null = null;

  // Pagination
  currentPage = 1;
  itemsPerPage = 10;
  totalItems = 0;
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

  constructor(
    private orgService: OrganizationService,
    private notification: NotificationService,
    private commonUtility: CommonUtilityService,
    private cdr: ChangeDetectorRef,
    private fb: FormBuilder
  ) {
    this.initializeForm();
  }

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeDropdownStates();
    // Don't load data here - wait for ngOnChanges to handle it
  }

  private initializeForm(): void {
    this.businessSettingForm = this.fb.group({
      settingKey: ['', Validators.required],
      settingValue: ['', Validators.required],
      settingType: ['String'],
      description: [''],
      isActive: [true]
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['organizationId'] && this.organizationId) {
      const orgIdChange = changes['organizationId'];
      // Only load if this is the first time (previousValue is undefined) or if the value actually changed
      if (!orgIdChange.previousValue || orgIdChange.previousValue !== orgIdChange.currentValue) {
        // Load data first, statistics will be updated automatically in loadData callback
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
    const fields = ['filter-status', 'items-per-page', 'settingType'];
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
      case 'items-per-page':
        return ['10', '25', '50', '100'];
      case 'settingType':
        const settingTypes = this.items
          .map(item => item.settingType)
          .filter((type): type is string => !!type && type.trim() !== '')
          .filter((type, index, self) => self.indexOf(type) === index)
          .sort();
        return ['String', 'Number', 'Boolean', 'Date', 'JSON', ...settingTypes];
      default:
        return [];
    }
  }

  // Data loading
  loadData(): void {
    this.isLoading = true;
    const params: any = {
      page: this.currentPage,
      pageSize: this.itemsPerPage,
      sortField: this.sortField,
      sortDirection: this.sortDirection
    };

    // Add search query if provided
    if (this.searchQuery && this.searchQuery.trim()) {
      params.search = this.searchQuery.trim();
    }

    // Add filters if provided
    if (this.filters.isActive !== undefined) {
      params.isActive = this.filters.isActive;
    }
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

    const sub = this.orgService.getBusinessSettings(params).subscribe({
      next: (res: any) => {
        const items = Array.isArray(res?.items) ? res.items : (Array.isArray(res) ? res : []);
        this.items = items.map((item: BusinessSetting) => ({
          ...item,
          showDropdown: false
        }));
        
        // Handle both camelCase and PascalCase response properties
        this.totalItems = res?.totalCount || res?.TotalCount || items.length;
        this.totalPages = res?.totalPages || res?.TotalPages || Math.ceil(this.totalItems / this.itemsPerPage);
        this.paginatedData = [...this.items]; // Server-side pagination, so items are already paginated
        
        // Update statistics after data is loaded (with correct totalItems)
        this.updateStatistics();
        
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading business settings:', error);
        this.isLoading = false;
        this.notification.error('Error', 'Failed to load business settings');
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
    this.filters = { createdFrom: '', createdTo: '', status: '' };
    this.filterFormData = { createdFrom: '', createdTo: '', status: '' };
    this.isActiveFilter = null;
    this.applyFilters();
  }

  refreshData(): void {
    
    // Statistics will be updated automatically in loadData callback
    this.loadData();
  }

  get activeFilterCount(): number {
    let count = 0;
    if (this.searchQuery) count++;
    if (this.filters.status) count++;
    if (this.filters.createdFrom) count++;
    if (this.filters.createdTo) count++;
    return count;
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

  onItemsPerPageChange(): void {
    this.currentPage = 1;
    this.itemsPerPage = this.itemsPerPageFormData;
    this.loadData();
  }

  selectItemsPerPageOption(option: string): void {
    this.itemsPerPageFormData = parseInt(option);
    this.itemsPerPage = parseInt(option);
    this.closeAllSearchableDropdowns();
    this.onItemsPerPageChange();
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

  resetSortToDefault(): void {
    this.sortField = 'createdAtUtc';
    this.sortDirection = 'desc';
    this.currentPage = 1;
  }

  // Dialog Management
  openAddDialog(): void {
    this.dialogMode = 'add';
    this.businessSettingForm.reset({
      settingKey: '',
      settingValue: '',
      settingType: 'String',
      description: '',
      isActive: true
    });
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openEditDialog(item: BusinessSetting): void {
    this.dialogMode = 'edit';
    this.selectedItem = item;
    this.businessSettingForm.patchValue({
      settingKey: item.settingKey || '',
      settingValue: item.settingValue || '',
      settingType: item.settingType || 'String',
      description: item.description || '',
      isActive: item.isActive !== undefined ? item.isActive : true
    });
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: BusinessSetting): void {
    // Load full business setting details with metadata
    const sub = this.orgService.getBusinessSetting(item.id).subscribe({
      next: (fullSetting) => {
        this.selectedItem = fullSetting;
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading business setting details:', error);
        // Fallback to list data if API fails
        this.selectedItem = item;
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    this.subscriptions.add(sub);
  }

  openEditDialogFromView(item: BusinessSetting): void {
    this.showViewDialog = false;
    this.openEditDialog(item);
  }

  exportBusinessSettingDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notification.warning('Warning', 'No business setting selected to export');
      return;
    }

    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const settingDetailsCards = document.querySelector('.modal-overlay .modal .profile-form') as HTMLElement;

    if (!modalBody || !settingDetailsCards) {
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
      html2canvas(settingDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: true,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: settingDetailsCards.scrollWidth,
        windowHeight: settingDetailsCards.scrollHeight
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

        pdf.save(`business-setting-details-${this.selectedItem?.settingKey.replace(/\s+/g, '-').toLowerCase()}.pdf`);
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

  openDeleteDialog(items: BusinessSetting[]): void {
    this.selectedItems.clear();
    items.forEach(item => this.selectedItems.add(item.id));
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notification.warning('Warning', 'Please select at least one setting to clone');
      return;
    }

    this.notification.info('Info', 'Cloning business settings...');
    this.orgService.cloneMultipleBusinessSettings(selectedIds).subscribe({
      next: (response) => {
        this.notification.success('Success', response.message || `${selectedIds.length} setting(s) cloned successfully`);
        this.selectedItems.clear();
        this.resetSortToDefault();
        this.loadData();
      },
      error: (error) => {
        this.notification.error('Error', error.error?.message || 'Failed to clone business settings');
      }
    });
  }

  cloneBusinessSetting(item: BusinessSetting): void {
    this.notification.info('Info', 'Cloning business setting...');
    this.orgService.cloneMultipleBusinessSettings([item.id]).subscribe({
      next: (response) => {
        this.notification.success('Success', response.message || 'Business setting cloned successfully');
        this.resetSortToDefault();
        this.loadData();
      },
      error: (error) => {
        this.notification.error('Error', error.error?.message || 'Failed to clone business setting');
      }
    });
  }

  closeDropdown(item: BusinessSetting): void {
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
    if (this.businessSettingForm.invalid) {
      this.businessSettingForm.markAllAsTouched();
      this.notification.warning('Validation', 'Please fill in all required fields');
      return;
    }

    const formValue = this.businessSettingForm.value;
    this.isSubmitting = true;

    if (this.dialogMode === 'add') {
      this.orgService.createBusinessSetting(formValue).subscribe({
        next: () => {
          this.closeDialogs();
          this.notification.success('Success', 'Business setting created successfully!');
          this.isSubmitting = false;
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          const errorMessage = error.error?.message || 'Failed to create business setting';
          this.notification.error('Error', errorMessage);
          // If it's a duplicate error, set error on the form field
          if (errorMessage.toLowerCase().includes('already exists')) {
            this.businessSettingForm.get('settingKey')?.setErrors({ duplicate: true });
            this.businessSettingForm.get('settingKey')?.markAsTouched();
          }
          this.isSubmitting = false;
        }
      });
    } else if (this.selectedItem?.id) {
      this.orgService.updateBusinessSetting(this.selectedItem.id, formValue).subscribe({
        next: () => {
          this.closeDialogs();
          this.notification.success('Success', 'Business setting updated successfully!');
          this.isSubmitting = false;
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          const errorMessage = error.error?.message || 'Failed to update business setting';
          this.notification.error('Error', errorMessage);
          // If it's a duplicate error, set error on the form field
          if (errorMessage.toLowerCase().includes('already exists')) {
            this.businessSettingForm.get('settingKey')?.setErrors({ duplicate: true });
            this.businessSettingForm.get('settingKey')?.markAsTouched();
          }
          this.isSubmitting = false;
        }
      });
    }
  }

  confirmDelete(): void {
    const itemsToDelete = Array.from(this.selectedItems);
    if (itemsToDelete.length === 0) return;

    if (itemsToDelete.length === 1) {
      this.orgService.deleteBusinessSetting(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notification.success('Success', 'Business setting deleted successfully!');
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          this.notification.error('Error', error.error?.message || 'Failed to delete business setting');
        }
      });
    } else {
      let deletedCount = 0;
      const totalCount = itemsToDelete.length;
      
      itemsToDelete.forEach(id => {
        this.orgService.deleteBusinessSetting(id).subscribe({
          next: () => {
            deletedCount++;
            if (deletedCount === totalCount) {
              this.selectedItems.clear();
              this.closeDialogs();
              this.notification.success('Success', `${totalCount} settings deleted successfully`);
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
              this.notification.warning('Warning', 'Some settings could not be deleted');
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

  // Export functionality (matching roles pattern exactly)
  exportBusinessSettings(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
    const formatMap: { [key: string]: 1 | 2 | 3 | 4 } = {
      'excel': 1,
      'csv': 2,
      'pdf': 3,
      'json': 4
    };

    const params: any = {
      format: formatMap[format],
      search: this.searchQuery || undefined,
      isActive: this.filters.isActive !== undefined ? this.filters.isActive : undefined,
      createdFrom: this.filters.createdFrom ? new Date(this.filters.createdFrom).toISOString() : undefined,
      createdTo: this.filters.createdTo ? new Date(this.filters.createdTo).toISOString() : undefined
    };

    // Add selected IDs if any are selected
    if (this.selectedItems.size > 0) {
      params.selectedIds = Array.from(this.selectedItems);
    }

    this.isExporting = true;
    this.exportProgress = 0;
    this.exportStatus = 'Pending';
    this.showExportDialog = true;

    this.orgService.startBusinessSettingExportAsync(params).subscribe({
      next: (response) => {
        this.exportJobId = response.jobId;
        this.notification.info('Export Started', 'Your export is processing. You can continue working.');
        this.pollExportJobStatus();
      },
      error: (error) => {
        console.error('Export start error:', error);
        this.notification.error('Error', 'Failed to start export job.');
        this.isExporting = false;
        this.showExportDialog = false;
      }
    });
  }

  private pollExportJobStatus(): void {
    if (!this.exportJobId) return;

    const intervalId = setInterval(() => {
      if (!this.exportJobId) { clearInterval(intervalId); return; }

      this.orgService.getBusinessSettingExportJobStatus(this.exportJobId).subscribe({
        next: (status: ExportJobStatus) => {
          this.exportStatus = status.status;
          this.exportProgress = status.progressPercent;
          this.exportFormat = status.format; // Store format for later use

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isExporting = false;

            if (status.status === 'Completed') {
              if (status.totalRows > 0) {
                this.notification.success('Export Completed', `Exported ${status.totalRows} records successfully!`);
                if (this.exportJobId) {
                  this.downloadCompletedExport(this.exportJobId, status.format);
                }
              } else {
                this.notification.warning('Export Completed', 'No data available for export with current filters.');
              }
            } else {
              let errorMessage = status.message || 'Export failed.';
              if (status.totalRows === 0) {
                errorMessage = 'No data available for export with current filters.';
              }
              this.notification.error('Export Failed', errorMessage);
            }

            this.exportJobId = null;
            this.exportFormat = null;
            this.showExportDialog = false;

            // Refresh history if dialog is open
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
          this.notification.error('Error', 'Export status check failed.');
        }
      });
    }, 3000);
  }

  private downloadCompletedExport(jobId: string, format: string): void {
    this.orgService.downloadBusinessSettingExport(jobId).subscribe({
      next: (blob: Blob) => {
        // Determine file extension based on format
        const extension = this.getFileExtension(format);

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `business_settings_export_${new Date().toISOString().split('T')[0]}.${extension}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      },
      error: (error) => {
        console.error('Download error:', error);
        this.notification.error('Error', 'Failed to download export file.');
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

  // Import functionality (matching roles pattern exactly)
  downloadTemplate(): void {
    this.orgService.getBusinessSettingTemplate().subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'business_settings_import_template.xlsx';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        this.notification.success('Success', 'Excel template downloaded successfully');
      },
      error: (error) => {
        console.error('Template download error:', error);
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

    // Add visual feedback
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.add('drag-over');
  }

  onDragEnter(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();

    // Add visual feedback
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.add('drag-enter');
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();

    // Remove visual feedback
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.remove('drag-over', 'drag-enter');
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();

    // Remove visual feedback
    const uploadArea = event.currentTarget as HTMLElement;
    uploadArea.classList.remove('drag-over', 'drag-enter');

    const files = event.dataTransfer?.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    this.validateAndSetFile(file);
  }

  private validateAndSetFile(file: File): void {
    // Validate file type
    const validTypes = ['application/vnd.ms-excel', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'text/csv'];
    const validExtensions = ['.csv', '.xlsx', '.xls'];
    const fileExtension = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

    if (!validTypes.includes(file.type) && !validExtensions.includes(fileExtension)) {
      this.notification.warning('Invalid File', 'Please upload a valid Excel (.xlsx, .xls) or CSV (.csv) file');
      return;
    }

    // Validate file size (5MB max as mentioned in UI)
    const maxSize = 5 * 1024 * 1024; // 5MB
    if (file.size > maxSize) {
      this.notification.warning('File Too Large', `File size should not exceed 5MB. Your file is ${(file.size / (1024 * 1024)).toFixed(2)}MB`);
      return;
    }

    this.selectedFile = file;
    this.importErrors = []; // Clear any previous errors
  }

  importData(): void {
    if (!this.selectedFile) {
      this.notification.warning('Warning', 'Please select a file to import');
      return;
    }

    this.isImporting = true;
    this.importErrors = [];
    this.errorReportId = null;
    this.importProgress = 0;
    this.importStatus = 'Pending';

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    // Start async job
    this.orgService.startBusinessSettingImportAsync(formData).subscribe({
      next: (res) => {
        this.importJobId = res.jobId;
        this.notification.info('Import Started', 'Your import is processing in the background. Check Import History for progress.');
        this.closeImportDialog(); // Close dialog immediately
        this.pollImportJobStatus();
      },
      error: (error) => {
        this.isImporting = false;
        console.error('Async import start error:', error);
        this.notification.error('Error', 'Failed to start import job.');
      }
    });
  }

  private pollImportJobStatus(): void {
    if (!this.importJobId) return;

    const intervalId = setInterval(() => {
      if (!this.importJobId) { clearInterval(intervalId); return; }

      this.orgService.getBusinessSettingImportJobStatus(this.importJobId).subscribe({
        next: (status: ImportJobStatus) => {
          this.importStatus = status.status;
          this.importProgress = status.progressPercent ?? 0;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isImporting = false;
            this.importJobId = null;

            if (status.status === 'Completed') {
              this.notification.success('Import Completed', `Imported ${status.successCount} settings${status.skippedCount ? `, skipped ${status.skippedCount}` : ''}${status.errorCount ? `, errors ${status.errorCount}` : ''}.`);
            } else if (status.status === 'Failed') {
              let errorMessage = status.message || 'Import failed.';
              if (status.totalRows === 0 && status.processedRows === 0) {
                errorMessage = 'File parsing failed. Please check that your file has the correct format and required headers. Download the template for reference.';
              } else if (status.totalRows > 0 && status.processedRows === 0) {
                errorMessage = 'No valid data found in the file. Please check your data format and try again.';
              }
              this.notification.error('Import Failed', errorMessage);

              // If there are errors, show additional info
              if (status.errorCount > 0) {
                this.notification.info('Error Details', `Check Import History to download detailed error report (${status.errorCount} errors found).`);
              }
            }

            // Refresh list and history
            this.resetSortToDefault();
            this.loadData();
            if (this.showImportHistoryDialog) {
              this.loadHistory();
            }
          }
        },
        error: () => {
          // If job not found or temporary error, stop polling to avoid infinite loop
          clearInterval(intervalId);
          this.isImporting = false;
          this.importJobId = null;
          this.notification.error('Error', 'Import job status check failed.');
        }
      });
    }, 5000); // Reduced to 5 seconds to reduce backend load
  }

  downloadErrorReport(): void {
    if (!this.errorReportId) {
      this.notification.warning('Warning', 'No error report available');
      return;
    }

    this.orgService.getBusinessSettingImportErrorReport(this.errorReportId).subscribe({
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

        this.notification.success('Success', 'Error report downloaded successfully');
      },
      error: (error) => {
        console.error('Error report download error:', error);
        this.notification.error('Error', 'Failed to download error report');
      }
    });
  }

  downloadErrorReportForHistory(historyId: string): void {
    // Find the history item to get the errorReportId
    const history = this.history.find(h => h.id === historyId);
    if (!history || !history.errorReportId) {
      this.notification.warning('Warning', 'No error report available for this import');
      return;
    }

    this.orgService.getBusinessSettingImportErrorReport(history.errorReportId).subscribe({
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

        this.notification.success('Success', 'Error report downloaded successfully');
      },
      error: (error) => {
        console.error('Error report download error:', error);
        this.notification.error('Error', 'Failed to download error report. The error report may have expired or been deleted.');
      }
    });
  }

  // Import History Methods (matching roles pattern exactly)
  openImportHistoryDialog(): void {
    this.showImportHistoryDialog = true;
    this.historyPage = 1;
    this.historyType = undefined; // Show all by default
    this.loadHistory();
    this.disableBodyScroll();
  }

  // Unified history loading
  loadHistory(): void {
    this.isLoadingHistory = true;
    this.orgService.getBusinessSettingHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
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

  // Filter history by type
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

    if (!history.jobId) {
      this.notification.warning('Warning', 'Job ID not found for this export');
      return;
    }

    // Download using the jobId
    this.orgService.downloadBusinessSettingExport(history.jobId).subscribe({
      next: (blob: Blob) => {
        // Determine file extension based on format
        const extension = this.getFileExtension(history.format);

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = history.fileName || `business_settings_export_${new Date().toISOString().split('T')[0]}.${extension}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        this.notification.success('Success', 'Export downloaded successfully');
      },
      error: (error) => {
        console.error('Download error:', error);
        this.notification.error('Error', 'Failed to download export file. The file may have expired or been deleted.');
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

  getSelectedItems(): BusinessSetting[] {
    return this.commonUtility.getSelectedItems(this.items, this.selectedItems);
  }

  get deleteItemCount(): number {
    return this.selectedItems.size;
  }

  // Dropdown methods
  isDropdownOpen(item: BusinessSetting): boolean {
    return !!(item as any).showDropdown;
  }

  toggleDropdown(item: BusinessSetting, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.exportDropdownOpen = false;
    this.items.forEach((dataItem: BusinessSetting) => {
      if (dataItem.id !== item.id) {
        (dataItem as any).showDropdown = false;
      }
    });
    (item as any).showDropdown = !(item as any).showDropdown;
  }

  toggleActiveStatusAndClose(item: BusinessSetting): void {
    this.toggleActiveStatus(item);
    (item as any).showDropdown = false;
  }

  deleteItemAndClose(item: BusinessSetting): void {
    this.openDeleteDialog([item]);
    (item as any).showDropdown = false;
  }

  closeAllDropdowns(): void {
    this.items.forEach((item: BusinessSetting) => {
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
   * @param fieldName - Field name (e.g., 'filter-status', 'settingType', etc.)
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
    } else if (fieldName === 'settingType') {
      this.businessSettingForm.patchValue({ settingType: '' });
    } else {
      // Form dropdowns
      this.formData[fieldName as keyof typeof this.formData] = '' as any;
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
    } else if (fieldName === 'settingType') {
      return !!this.businessSettingForm.get('settingType')?.value;
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

  onMainPageClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.form-group') && !target.closest('.searchable-dropdown') && !target.closest('.dropdown-toggle') && !target.closest('.dropdown-menu') && !target.closest('.action-dropdown')) {
      this.closeAllSearchableDropdowns();
      this.closeAllDropdowns();
      this.exportDropdownOpen = false;
    }
  }

  toggleActiveStatus(item: BusinessSetting): void {
    const newStatus = !item.isActive;
    this.orgService.updateBusinessSetting(item.id, {
      settingKey: item.settingKey,
      settingValue: item.settingValue,
      description: item.description,
      isActive: newStatus
    }).subscribe({
      next: () => {
        this.notification.success('Success', `Setting ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.resetSortToDefault();
        this.loadData();
        this.loadStatistics();
      },
      error: () => {
        this.notification.error('Error', 'Failed to update setting status');
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

  openEdit(item: BusinessSetting): void {
    this.openEditDialog(item);
  }

  close(): void {
    this.closeDialogs();
  }

  save(): void {
    this.saveItem();
  }

  remove(item: BusinessSetting): void {
    this.openDeleteDialog([item]);
  }
}
