import { Component, OnInit, OnDestroy, HostListener, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommonUtilityService } from '../services/common-utility.service';

// Make Math and document available in template
@Component({
  selector: 'app-base-crud',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="crud-page" (click)="onMainPageClick($event)">
      <!-- This will be replaced by child components with proper templates -->
      <ng-content></ng-content>
    </div>
  `,
  styleUrl: './base-crud.component.scss'
})
export class BaseCrudComponent<T> implements OnInit, OnDestroy {

  @Input() pageTitle: string = '';
  @Input() pageDescription: string = '';
  @Input() entityName: string = '';
  @Input() entityIcon: string = 'fas fa-file';

  // Data properties
  items: T[] = [];
  filteredItems: T[] = [];
  selectedItems: Set<string> = new Set();

  // Search and filter properties
  searchQuery = '';
  showFilters = false;
  filters: any = {};
  filterFormData: any = {};

  // Pagination properties
  currentPage = 1;
  itemsPerPage = 10;
  totalItems = 0;

  // Sorting properties
  sortField: string = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  // UI state properties
  isLoading = false;
  isMobile = false;

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: T | null = null;
  deleteItemCount = 0;
  exportDropdownOpen = false;

  // Import properties
  selectedFile: File | null = null;
  importErrors: string[] = [];
  isImporting = false;

  // Form data for add/edit
  formData: any = {};
  formErrors: { [key: string]: string } = {};
  formTouched: { [key: string]: boolean } = {};
  isSubmitting = false;

  // Items per page form data
  itemsPerPageFormData: string = '10';

  // Searchable dropdown properties
  dropdownStates: { [key: string]: { isOpen: boolean; searchTerm: string; filteredOptions: string[] } } = {};

  // Make Math and document available in template
  Math = Math;
  document = document;

  constructor(
    protected snackBar: MatSnackBar,
    protected commonUtility: CommonUtilityService
  ) {}

  ngOnInit() {
    this.checkMobile();
    this.loadData();
    this.initializeAllDropdownStates();
  }

  initializeAllDropdownStates() {
    const fields = this.getDropdownFieldNames();
    fields.forEach(field => {
      this.initializeDropdownState(field);
    });
  }

  getDropdownFieldNames(): string[] {
    return ['filter-status', 'items-per-page'];
  }

  ngOnDestroy() {
    this.enableBodyScroll();
  }

  @HostListener('window:resize', ['$event'])
  onResize() {
    this.checkMobile();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event) {
    const target = event.target as HTMLElement;

    // Don't close anything if clicking on modal buttons
    if (target.closest('.header-buttons') || target.closest('.modal-overlay')) {
      return;
    }

    // Close searchable dropdowns if click is outside them
    if (!target.closest('.searchable-dropdown')) {
      this.closeAllSearchableDropdowns();
    }

    // Close regular dropdowns if click is outside them
    if (!target.closest('.dropdown-toggle') && !target.closest('.dropdown-menu') && !target.closest('.action-dropdown')) {
      this.closeAllDropdowns();
      this.exportDropdownOpen = false;
    }
  }

  protected checkMobile() {
    this.isMobile = this.commonUtility.isMobile();
  }

  // Abstract methods to be implemented by child components
  protected loadData(): void {
    // Override in child component
  }

  protected refreshData(): void {
    this.loadData();
  }

  protected applyFilters(): void {
    // Override in child component for server-side filtering
    // or use default client-side filtering
    this.applyClientSideFilters();
  }

  protected applyClientSideFilters(): void {
    let filtered = [...this.items];

    // Search filter
    if (this.searchQuery) {
      filtered = filtered.filter(item =>
        this.getItemSearchText(item).toLowerCase().includes(this.searchQuery.toLowerCase())
      );
    }

    // Apply other filters - override in child component
    filtered = this.applyCustomFilters(filtered);

    // Sort data
    this.sortItems(filtered);

    this.filteredItems = filtered;
    this.totalItems = filtered.length;
    this.currentPage = 1;
  }

  protected getItemSearchText(item: T): string {
    // Override in child component to specify search fields
    return JSON.stringify(item);
  }

  protected applyCustomFilters(filtered: T[]): T[] {
    // Override in child component for custom filtering
    return filtered;
  }

  protected sortItems(items: T[]): void {
    if (!this.sortField) return;

    items.sort((a, b) => {
      const aValue = (a as any)[this.sortField];
      const bValue = (b as any)[this.sortField];

      if (typeof aValue === 'string' && typeof bValue === 'string') {
        return this.sortDirection === 'asc'
          ? aValue.localeCompare(bValue)
          : bValue.localeCompare(aValue);
      }

      if (typeof aValue === 'number' && typeof bValue === 'number') {
        return this.sortDirection === 'asc'
          ? aValue - bValue
          : bValue - aValue;
      }

      return 0;
    });
  }

  onSearch(): void {
    this.applyFilters();
  }

  onFilterChange(): void {
    this.applyFilters();
  }

  clearFilters(): void {
    this.filters = this.getDefaultFilters();
    this.filterFormData = {};
    this.searchQuery = '';
    this.applyFilters();
  }

  protected getDefaultFilters(): any {
    return {
      status: 'all',
      dateFrom: '',
      dateTo: ''
    };
  }

  get activeFilterCount(): number {
    return this.commonUtility.getActiveFilterCount(this.filters, this.searchQuery);
  }

  // Sorting methods
  sortData(field: string): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.applyFilters();
  }

  // Selection methods
  isAllSelected(): boolean {
    return this.commonUtility.isAllSelected(this.paginatedData, this.selectedItems);
  }

  masterToggle(): void {
    this.commonUtility.masterToggle(this.paginatedData, this.selectedItems);
  }

  toggleItemSelection(id: string): void {
    this.commonUtility.toggleItemSelection(id, this.selectedItems);
  }

  // Pagination methods
  get paginatedData(): T[] {
    return this.commonUtility.getPaginatedData(this.filteredItems, this.currentPage, this.itemsPerPage);
  }

  get totalPages(): number {
    return this.commonUtility.getTotalPages(this.totalItems, this.itemsPerPage);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
  }

  onItemsPerPageChange(itemsPerPage: number): void {
    this.itemsPerPage = itemsPerPage;
    this.currentPage = 1;
    this.loadData(); // Reload with new page size
  }

  // Dialog methods
  openAddDialog(): void {
    this.dialogMode = 'add';
    this.formData = this.getDefaultFormData();
    this.resetFormValidation();
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openEditDialog(item: T): void {
    this.dialogMode = 'edit';
    this.selectedItem = item;
    this.formData = { ...item };
    this.resetFormValidation();
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: T): void {
    this.selectedItem = item;
    this.showViewDialog = true;
    this.disableBodyScroll();
  }

  openDeleteDialog(items: T[]): void {
    this.deleteItemCount = items.length;
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  closeDialogs(): void {
    this.showAddEditDialog = false;
    this.showViewDialog = false;
    this.showDeleteDialog = false;
    this.showImportDialog = false;
    this.selectedItem = null;
    this.closeAllSearchableDropdowns();
    this.enableBodyScroll();
  }

  protected getDefaultFormData(): any {
    return {};
  }

  openImportDialog(): void {
    this.showImportDialog = true;
    this.selectedFile = null;
    this.importErrors = [];
    this.disableBodyScroll();
  }

  closeImportDialog(): void {
    this.showImportDialog = false;
    this.selectedFile = null;
    this.importErrors = [];
    this.enableBodyScroll();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];

      const validTypes = ['application/vnd.ms-excel', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'text/csv'];

      if (!validTypes.includes(file.type) && !file.name.endsWith('.csv') && !file.name.endsWith('.xlsx') && !file.name.endsWith('.xls')) {
        this.snackBar.open('Please upload a valid Excel or CSV file', 'Close', { duration: 4000 });
        input.value = '';
        return;
      }

      const maxSize = 5 * 1024 * 1024;
      if (file.size > maxSize) {
        this.snackBar.open('File size should not exceed 5MB', 'Close', { duration: 4000 });
        input.value = '';
        return;
      }

      this.selectedFile = file;
      this.importErrors = [];
    }
  }

  // Abstract methods for child implementation
  protected downloadTemplate(): void {
    // Override in child component
  }

  protected importData(): void {
    // Override in child component
  }

  protected exportData(format: string): void {
    // Override in child component
  }

  protected saveItem(): void {
    // Override in child component
  }

  protected confirmDelete(): void {
    // Override in child component
  }

  // Dropdown methods
  protected closeAllDropdowns(): void {
    // Implement if needed
  }

  protected getOptionsForField(fieldName: string): string[] {
    switch (fieldName) {
      case 'filter-status': return ['active', 'inactive'];
      case 'items-per-page': return ['10', '25', '50', '100'];
      default: return [];
    }
  }

  protected initializeDropdownState(fieldName: string): void {
    if (!this.dropdownStates[fieldName]) {
      const options = this.getOptionsForField(fieldName);
      this.dropdownStates[fieldName] = this.commonUtility.initializeDropdownState(fieldName, options);
    }
  }

  protected toggleSearchableDropdown(fieldName: string): void {
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

  protected filterOptions(fieldName: string): void {
    this.initializeDropdownState(fieldName);
    const allOptions = this.getOptionsForField(fieldName);
    const searchTerm = this.dropdownStates[fieldName].searchTerm;

    this.dropdownStates[fieldName].filteredOptions = this.commonUtility.filterDropdownOptions(allOptions, searchTerm);
  }

  protected closeAllSearchableDropdowns(): void {
    Object.keys(this.dropdownStates).forEach(key => {
      this.dropdownStates[key].isOpen = false;
      this.dropdownStates[key].searchTerm = '';
    });
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

  // Utility methods
  protected getStatusClass(isActive: boolean): string {
    return isActive ? 'status-active' : 'status-inactive';
  }

  protected formatDate(dateString: string): string {
    return this.commonUtility.formatDate(dateString);
  }

  protected disableBodyScroll(): void {
    this.commonUtility.disableBodyScroll();
  }

  protected enableBodyScroll(): void {
    this.commonUtility.enableBodyScroll();
  }

  protected getSerialNumber(index: number): number {
    return (this.currentPage - 1) * this.itemsPerPage + index + 1;
  }

  // Form validation methods
  protected resetFormValidation(): void {
    this.commonUtility.resetFormValidation(this.formErrors, this.formTouched, this.isSubmitting);
  }

  protected isFormValid(): boolean {
    return this.commonUtility.isFormValid(this.formErrors);
  }

  protected markAllFieldsAsTouched(): void {
    const fields = this.getFormFieldNames();
    this.commonUtility.markAllFieldsAsTouched(this.formTouched, fields);
  }

  protected getFormFieldNames(): string[] {
    return Object.keys(this.formData);
  }

  protected validateField(fieldName: string): void {
    this.formTouched[fieldName] = true;
    // Override in child component for field-specific validation
  }

  protected hasError(fieldName: string): boolean {
    return this.commonUtility.hasError(fieldName, this.formErrors, this.formTouched);
  }

  protected getError(fieldName: string): string {
    return this.commonUtility.getError(fieldName, this.formErrors);
  }

  protected isFieldTouched(fieldName: string): boolean {
    return this.commonUtility.isFieldTouched(fieldName, this.formTouched);
  }

  // File input helpers
  protected triggerFileInput(): void {
    const fileInput = document.getElementById('fileInput') as HTMLInputElement;
    if (fileInput) {
      fileInput.click();
    }
  }

  removeSelectedFile(): void {
    this.selectedFile = null;
    this.importErrors = [];
  }
}
