import { Component, OnInit, Input, OnChanges, SimpleChanges, OnDestroy, HostListener, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { TaxRate, OrganizationService, ImportJobStatus, ExportJobStatus, ImportExportHistory, ImportExportHistoryResponse } from '../../../core/services/organization.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';

@Component({
  selector: 'app-tax-rates',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './tax-rates.component.html',
  styleUrls: ['./tax-rates.component.scss']
})
export class TaxRatesComponent implements OnInit, OnChanges, OnDestroy {
  // Utility references
  Math = Math;
  document = document;
  parseInt = parseInt;

  // Subscriptions
  private subscriptions = new Subscription();

  @Input() organizationId: string = '';
  items: TaxRate[] = [];
  filteredItems: TaxRate[] = [];
  paginatedData: TaxRate[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: Partial<TaxRate> = {
    name: '',
    rate: 0,
    description: '',
    taxType: '',
    isDefault: false,
    effectiveFrom: undefined,
    effectiveTo: undefined,
    isActive: true
  };
  taxRateForm!: FormGroup;
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: TaxRate | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '', status: '', taxType: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', status: '', taxType: '' };
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
  taxTypes: string[] = ['Sales Tax', 'VAT', 'GST', 'Service Tax', 'Other'];

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
    this.taxRateForm = this.fb.group({
      name: ['', Validators.required],
      rate: [0, [Validators.required, Validators.min(0)]],
      description: [''],
      taxType: [''],
      isDefault: [false],
      isActive: [true],
      effectiveFrom: [''],
      effectiveTo: ['']
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
    const fields = ['filter-status', 'filter-taxType', 'taxType', 'items-per-page'];
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
      case 'filter-taxType':
      case 'taxType':
        return this.taxTypes;
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
      taxType: this.filters.taxType || undefined,
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

    const sub = this.orgService.getTaxRates(params).subscribe({
      next: (response: any) => {
        const items = Array.isArray(response?.items) ? response.items : (Array.isArray(response) ? response : []);
        this.items = items.map((item: TaxRate) => ({
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
        console.error('Error loading tax rates:', error);
        this.notification.error('Error', 'Failed to load tax rates. Please try again.');
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
    this.orgService.getTaxTypes().subscribe({
      next: (taxTypes) => {
        this.taxTypes = taxTypes;
      },
      error: () => {
        // Fallback to default list if API fails
        this.taxTypes = ['Sales Tax', 'VAT', 'GST', 'Service Tax', 'Other'];
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
    this.filters = { createdFrom: '', createdTo: '', status: '', taxType: '' };
    this.filterFormData = { createdFrom: '', createdTo: '', status: '', taxType: '' };
    this.isActiveFilter = null;
    this.applyFilters();
  }

  get activeFilterCount(): number {
    let count = 0;
    if (this.searchQuery) count++;
    if (this.filters.status) count++;
    if (this.filters.taxType) count++;
    if (this.filters.createdFrom) count++;
    if (this.filters.createdTo) count++;
    return count;
  }

  getActiveFilterCount(): number {
    return this.activeFilterCount;
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

  refreshData(): void {
    
    this.loadData();
    this.loadStatistics();
  }

  loadRoles(): void {
    this.refreshData();
  }

  getTotalPages(): number {
    return this.totalPages;
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

  // Dialog Management
  openAddDialog(): void {
    this.dialogMode = 'add';
    this.taxRateForm.reset({
      name: '',
      rate: 0,
      description: '',
      taxType: '',
      isDefault: false,
      isActive: true,
      effectiveFrom: '',
      effectiveTo: ''
    });
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openEditDialog(item: TaxRate): void {
    this.dialogMode = 'edit';
    this.selectedItem = item;
    const formatDate = (date: string | Date | null | undefined): string => {
      if (!date) return '';
      const d = typeof date === 'string' ? new Date(date) : date;
      if (isNaN(d.getTime())) return '';
      return d.toISOString().split('T')[0];
    };
    this.taxRateForm.patchValue({
      name: item.name || '',
      rate: item.rate ?? 0,
      description: item.description || '',
      taxType: item.taxType || '',
      isDefault: item.isDefault !== undefined ? item.isDefault : false,
      isActive: item.isActive !== undefined ? item.isActive : true,
      effectiveFrom: formatDate(item.effectiveFrom),
      effectiveTo: formatDate(item.effectiveTo)
    });
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: TaxRate): void {
    // Load full tax rate details with metadata
    const sub = this.orgService.getTaxRate(item.id).subscribe({
      next: (fullTaxRate) => {
        this.selectedItem = fullTaxRate;
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading tax rate details:', error);
        // Fallback to list data if API fails
        this.selectedItem = item;
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    if (this.subscriptions) {
      this.subscriptions.add(sub);
    }
  }

  openEditDialogFromView(item: TaxRate): void {
    this.showViewDialog = false;
    this.openEditDialog(item);
  }

  exportTaxRateDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notification.warning('Warning', 'No tax rate selected to export');
      return;
    }

    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const taxRateDetailsCards = document.querySelector('.modal-overlay .modal .profile-form') as HTMLElement;

    if (!modalBody || !taxRateDetailsCards) {
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
      html2canvas(taxRateDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: true,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: taxRateDetailsCards.scrollWidth,
        windowHeight: taxRateDetailsCards.scrollHeight
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

        pdf.save(`tax-rate-details-${this.selectedItem?.name.replace(/\s+/g, '-').toLowerCase()}.pdf`);
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

  openDeleteDialog(items: TaxRate[]): void {
    this.selectedItems.clear();
    items.forEach(item => this.selectedItems.add(item.id));
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notification.warning('Warning', 'Please select at least one tax rate to clone');
      return;
    }

    this.notification.info('Info', 'Cloning tax rates...');
    this.orgService.cloneMultipleTaxRates(selectedIds).subscribe({
      next: (response) => {
        this.notification.success('Success', response.message || `${selectedIds.length} tax rate(s) cloned successfully`);
        this.selectedItems.clear();
        this.resetSortToDefault();
        this.loadData();
      },
      error: (error) => {
        this.notification.error('Error', error.error?.message || 'Failed to clone tax rates');
      }
    });
  }

  cloneTaxRate(item: TaxRate): void {
    this.notification.info('Info', 'Cloning tax rate...');
    this.orgService.cloneMultipleTaxRates([item.id]).subscribe({
      next: (response) => {
        this.notification.success('Success', response.message || 'Tax rate cloned successfully');
        this.resetSortToDefault();
        this.loadData();
      },
      error: (error) => {
        this.notification.error('Error', error.error?.message || 'Failed to clone tax rate');
      }
    });
  }

  closeDropdown(item: TaxRate): void {
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
    if (this.taxRateForm.invalid) {
      this.taxRateForm.markAllAsTouched();
      this.notification.warning('Validation', 'Please fill in all required fields');
      return;
    }

    this.isSubmitting = true;
    const formValue = this.taxRateForm.value;

    if (this.dialogMode === 'add') {
      this.orgService.createTaxRate(formValue).subscribe({
        next: () => {
          this.closeDialogs();
          this.notification.success('Success', 'Tax rate created successfully!');
          this.isSubmitting = false;
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          this.notification.error('Error', error.error?.message || 'Failed to create tax rate');
          this.isSubmitting = false;
        }
      });
    } else if (this.selectedItem?.id) {
      this.orgService.updateTaxRate(this.selectedItem.id, formValue).subscribe({
        next: () => {
          this.closeDialogs();
          this.notification.success('Success', 'Tax rate updated successfully!');
          this.isSubmitting = false;
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          this.notification.error('Error', error.error?.message || 'Failed to update tax rate');
          this.isSubmitting = false;
        }
      });
    }
  }

  confirmDelete(): void {
    const itemsToDelete = Array.from(this.selectedItems);
    if (itemsToDelete.length === 0) return;

    if (itemsToDelete.length === 1) {
      this.orgService.deleteTaxRate(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notification.success('Success', 'Tax rate deleted successfully!');
          this.resetSortToDefault();
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          this.notification.error('Error', error.error?.message || 'Failed to delete tax rate');
        }
      });
    } else {
      let deletedCount = 0;
      const totalCount = itemsToDelete.length;
      
      itemsToDelete.forEach(id => {
        this.orgService.deleteTaxRate(id).subscribe({
          next: () => {
            deletedCount++;
            if (deletedCount === totalCount) {
              this.selectedItems.clear();
              this.closeDialogs();
              this.notification.success('Success', `${totalCount} tax rates deleted successfully`);
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
              this.notification.warning('Warning', 'Some tax rates could not be deleted');
              this.resetSortToDefault();
              this.loadData();
              this.loadStatistics();
            }
          }
        });
      });
    }
  }

  // Export functionality (matching roles pattern exactly)
  toggleExportDropdown(): void {
    this.closeAllDropdowns();
    this.exportDropdownOpen = !this.exportDropdownOpen;
  }

  exportTaxRates(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
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
      taxType: this.filters.taxType || undefined,
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

    this.orgService.startTaxRateExportAsync(params).subscribe({
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

      this.orgService.getTaxRateExportJobStatus(this.exportJobId).subscribe({
        next: (status: ExportJobStatus) => {
          this.exportStatus = status.status;
          this.exportProgress = status.progressPercent;
          this.exportFormat = status.format;

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
    this.orgService.downloadTaxRateExport(jobId).subscribe({
      next: (blob: Blob) => {
        const extension = this.getFileExtension(format);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `tax_rates_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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

  closeExportDialog(): void {
    this.showExportDialog = false;
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

  // Import functionality (matching roles pattern exactly)
  downloadTemplate(): void {
    this.orgService.getTaxRateTemplate().subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'tax_rates_import_template.xlsx';
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
    this.errorReportId = null;
    this.importProgress = 0;
    this.importStatus = 'Pending';

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    // Start async job
    this.orgService.startTaxRateImportAsync(formData).subscribe({
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

      this.orgService.getTaxRateImportJobStatus(this.importJobId).subscribe({
        next: (status: ImportJobStatus) => {
          this.importStatus = status.status;
          this.importProgress = status.progressPercent ?? 0;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isImporting = false;
            this.importJobId = null;

            if (status.status === 'Completed') {
              this.notification.success('Import Completed', `Imported ${status.successCount} tax rates${status.skippedCount ? `, skipped ${status.skippedCount}` : ''}${status.errorCount ? `, errors ${status.errorCount}` : ''}.`);
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

    this.orgService.getTaxRateImportErrorReport(this.errorReportId).subscribe({
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

  // Unified history loading (matching roles pattern exactly)
  openImportHistoryDialog(): void {
    this.showImportHistoryDialog = true;
    this.historyPage = 1;
    this.historyType = undefined;
    this.loadHistory();
    this.disableBodyScroll();
  }

  loadHistory(): void {
    this.isLoadingHistory = true;
    this.orgService.getTaxRateHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
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
    this.orgService.downloadTaxRateExport(history.jobId).subscribe({
      next: (blob: Blob) => {
        // Determine file extension based on format
        const extension = this.getFileExtension(history.format);

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = history.fileName || `tax_rates_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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

  downloadErrorReportForHistory(historyId: string): void {
    // Find the history item to get the errorReportId
    const history = this.history.find(h => h.id === historyId);
    if (!history || !history.errorReportId) {
      this.notification.warning('Warning', 'No error report available for this import');
      return;
    }

    this.orgService.getTaxRateImportErrorReport(history.errorReportId).subscribe({
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

  getSelectedItems(): TaxRate[] {
    return this.commonUtility.getSelectedItems(this.items, this.selectedItems);
  }

  get deleteItemCount(): number {
    return this.selectedItems.size;
  }

  // Dropdown methods
  isDropdownOpen(item: TaxRate): boolean {
    return !!(item as any).showDropdown;
  }

  toggleDropdown(item: TaxRate, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.exportDropdownOpen = false;
    this.items.forEach((dataItem: TaxRate) => {
      if (dataItem.id !== item.id) {
        (dataItem as any).showDropdown = false;
      }
    });
    (item as any).showDropdown = !(item as any).showDropdown;
  }

  toggleActiveStatusAndClose(item: TaxRate): void {
    this.toggleActiveStatus(item);
    (item as any).showDropdown = false;
  }

  deleteItemAndClose(item: TaxRate): void {
    this.openDeleteDialog([item]);
    (item as any).showDropdown = false;
  }

  closeAllDropdowns(): void {
    this.items.forEach((item: TaxRate) => {
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
      if (this.taxRateForm && this.taxRateForm.get(fieldName)) {
        this.taxRateForm.patchValue({ [fieldName]: option });
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
   * @param fieldName - Field name (e.g., 'filter-status', 'taxType', etc.)
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
    } else if (fieldName === 'taxType') {
      this.taxRateForm.patchValue({ taxType: '' });
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
    } else if (fieldName === 'taxType') {
      return !!this.taxRateForm.get('taxType')?.value;
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

  toggleActiveStatus(item: TaxRate): void {
    const newStatus = !item.isActive;
    this.orgService.updateTaxRate(item.id, { ...item, isActive: newStatus }).subscribe({
      next: () => {
        this.notification.success('Success', `Tax rate ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.resetSortToDefault();
        this.loadData();
        this.loadStatistics();
      },
      error: () => {
        this.notification.error('Error', 'Failed to update tax rate status');
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

  openEdit(item: TaxRate): void {
    this.openEditDialog(item);
  }

  close(): void {
    this.closeDialogs();
  }

  save(): void {
    this.saveItem();
  }

  remove(item: TaxRate): void {
    this.openDeleteDialog([item]);
  }
}
