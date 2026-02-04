import { Injectable, inject } from '@angular/core';
import { CurrencyService } from './currency.service';

/**
 * CommonUtilityService - Common utility functions
 * Use this service for consistent utility operations across the application
 */
@Injectable({
  providedIn: 'root'
})
export class CommonUtilityService {
  private currencyService = inject(CurrencyService);

  // ================================
  // FORMATTING UTILITIES
  // ================================

  /**
   * Format currency value
   * @param value - Value to format
   * @param currency - Currency code (if not provided, uses default currency)
   * @param locale - Locale string
   */
  formatCurrency(value: number, currency?: string, locale: string = 'en-US'): string {
    const currencyCode = currency || this.currencyService.getDefaultCurrencyCode();
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency: currencyCode
    }).format(value);
  }

  /**
   * Format number value
   * @param value - Value to format
   * @param locale - Locale string
   */
  formatNumber(value: number, locale: string = 'en-US'): string {
    return new Intl.NumberFormat(locale).format(value);
  }

  /**
   * Format date value
   * @param dateString - Date string or Date object to format
   * @param locale - Locale string
   * @param options - Intl.DateTimeFormatOptions
   */
  formatDate(dateString: string | Date, locale: string = 'en-US', options?: Intl.DateTimeFormatOptions): string {
    const defaultOptions: Intl.DateTimeFormatOptions = {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    };
    
    const date = dateString instanceof Date ? dateString : new Date(dateString);
    return date.toLocaleDateString(locale, options || defaultOptions);
  }

  /**
   * Format date and time value
   * @param dateString - Date string or Date object to format
   * @param locale - Locale string
   * @param options - Intl.DateTimeFormatOptions
   */
  formatDateTime(dateString: string | Date, locale: string = 'en-US', options?: Intl.DateTimeFormatOptions): string {
    const defaultOptions: Intl.DateTimeFormatOptions = {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    };
    
    const date = dateString instanceof Date ? dateString : new Date(dateString);
    return date.toLocaleString(locale, options || defaultOptions);
  }

  /**
   * Format file size in bytes to human readable format
   * @param bytes - File size in bytes
   * @param decimals - Number of decimal places
   */
  formatFileSize(bytes: number, decimals: number = 2): string {
    if (bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
  }

  // ================================
  // STATUS UTILITIES
  // ================================

  /**
   * Get CSS class for status
   * @param status - Status string
   */
  getStatusClass(status: string): string {
    switch (status) {
      case 'active': return 'status-active';
      case 'inactive': return 'status-inactive';
      case 'pending': return 'status-pending';
      default: return '';
    }
  }

  /**
   * Get status color
   * @param status - Status string
   */
  getStatusColor(status: string): string {
    switch (status) {
      case 'active': return '#059669';
      case 'inactive': return '#6c757d';
      case 'pending': return '#ffc107';
      default: return '#6c757d';
    }
  }

  /**
   * Get status icon
   * @param status - Status string
   */
  getStatusIcon(status: string): string {
    switch (status) {
      case 'active': return 'fas fa-check-circle';
      case 'inactive': return 'fas fa-times-circle';
      case 'pending': return 'fas fa-clock';
      default: return 'fas fa-question-circle';
    }
  }

  // ================================
  // FORM VALIDATION UTILITIES
  // ================================

  /**
   * Check if field has error
   * @param fieldName - Field name
   * @param errors - Form errors
   * @param touched - Form touched state
   */
  hasError(fieldName: string, errors: { [key: string]: string }, touched: { [key: string]: boolean }): boolean {
    return !!(touched[fieldName] && errors[fieldName]);
  }

  /**
   * Get error message for field
   * @param fieldName - Field name
   * @param errors - Form errors
   */
  getError(fieldName: string, errors: { [key: string]: string }): string {
    return errors[fieldName] || '';
  }

  /**
   * Check if field is touched
   * @param fieldName - Field name
   * @param touched - Form touched state
   */
  isFieldTouched(fieldName: string, touched: { [key: string]: boolean }): boolean {
    return !!touched[fieldName];
  }

  // ================================
  // UI STATE UTILITIES
  // ================================

  /**
   * Check if device is mobile
   * @param breakpoint - Breakpoint width
   */
  isMobile(breakpoint: number = 1180): boolean {
    return window.innerWidth <= breakpoint;
  }

  /**
   * Check if all items are selected
   * @param items - Array of items
   * @param selectedItems - Set of selected item IDs
   */
  isAllSelected(items: any[], selectedItems: Set<string>): boolean {
    return items.length > 0 && selectedItems.size === items.length;
  }

  /**
   * Get selected items
   * @param items - Array of items
   * @param selectedItems - Set of selected item IDs
   */
  getSelectedItems(items: any[], selectedItems: Set<string>): any[] {
    return items.filter(item => selectedItems.has(item.id));
  }

  /**
   * Master toggle for select all
   * @param items - Array of items
   * @param selectedItems - Set of selected item IDs
   */
  masterToggle(items: any[], selectedItems: Set<string>): void {
    if (this.isAllSelected(items, selectedItems)) {
      selectedItems.clear();
    } else {
      items.forEach(item => selectedItems.add(item.id));
    }
  }

  /**
   * Toggle item selection
   * @param itemId - Item ID
   * @param selectedItems - Set of selected item IDs
   */
  toggleItemSelection(itemId: string, selectedItems: Set<string>): void {
    if (selectedItems.has(itemId)) {
      selectedItems.delete(itemId);
    } else {
      selectedItems.add(itemId);
    }
  }

  // ================================
  // COUNT UTILITIES
  // ================================

  /**
   * Get count by status
   * @param items - Array of items
   * @param status - Status to count
   */
  getCountByStatus(items: any[], status: string): number {
    return items.filter(item => item.status === status).length;
  }

  /**
   * Get active count
   * @param items - Array of items
   */
  getActiveCount(items: any[]): number {
    return this.getCountByStatus(items, 'active');
  }

  /**
   * Get pending count
   * @param items - Array of items
   */
  getPendingCount(items: any[]): number {
    return this.getCountByStatus(items, 'pending');
  }

  /**
   * Get inactive count
   * @param items - Array of items
   */
  getInactiveCount(items: any[]): number {
    return this.getCountByStatus(items, 'inactive');
  }

  // ================================
  // DROPDOWN UTILITIES
  // ================================

  /**
   * Initialize dropdown state
   * @param fieldName - Field name
   * @param options - Available options
   */
  initializeDropdownState(fieldName: string, options: string[]): { isOpen: boolean; searchTerm: string; filteredOptions: string[] } {
    return {
      isOpen: false,
      searchTerm: '',
      filteredOptions: options
    };
  }

  /**
   * Toggle dropdown state
   * @param dropdownStates - Current dropdown states
   * @param fieldName - Field name
   * @param options - Available options
   */
  toggleDropdownState(
    dropdownStates: { [key: string]: { isOpen: boolean; searchTerm: string; filteredOptions: string[] } },
    fieldName: string,
    options: string[]
  ): void {
    if (!dropdownStates[fieldName]) {
      dropdownStates[fieldName] = this.initializeDropdownState(fieldName, options);
    }
    
    if (dropdownStates[fieldName].isOpen) {
      dropdownStates[fieldName].isOpen = false;
      dropdownStates[fieldName].searchTerm = '';
    } else {
      // Close all other dropdowns first
      Object.keys(dropdownStates).forEach(key => {
        if (key !== fieldName) {
          dropdownStates[key].isOpen = false;
          dropdownStates[key].searchTerm = '';
        }
      });
      
      dropdownStates[fieldName].isOpen = true;
      dropdownStates[fieldName].searchTerm = '';
      dropdownStates[fieldName].filteredOptions = options;
    }
  }

  /**
   * Filter dropdown options
   * @param options - All options
   * @param searchTerm - Search term
   */
  filterDropdownOptions(options: string[], searchTerm: string): string[] {
    if (!searchTerm.trim()) return options;
    return options.filter(option =>
      option.toLowerCase().includes(searchTerm.toLowerCase())
    );
  }

  /**
   * Close all dropdowns
   * @param dropdownStates - Current dropdown states
   */
  closeAllDropdowns(dropdownStates: { [key: string]: { isOpen: boolean; searchTerm: string; filteredOptions: string[] } }): void {
    Object.keys(dropdownStates).forEach(key => {
      dropdownStates[key].isOpen = false;
      dropdownStates[key].searchTerm = '';
    });
  }

  /**
   * Clear dropdown value - Generic method to clear dropdown selection
   * This method can be used by components to clear dropdown values
   * @param fieldName - Field name to identify the dropdown
   * @param clearCallback - Callback function to clear the actual value (e.g., form control or filter)
   * @param dropdownStates - Dropdown states object (optional, for closing dropdown)
   */
  clearDropdownValue(
    fieldName: string,
    clearCallback: () => void,
    dropdownStates?: { [key: string]: { isOpen: boolean; searchTerm: string; filteredOptions: string[] } }
  ): void {
    // Clear the actual value using callback
    clearCallback();
    
    // Close the dropdown if states provided
    if (dropdownStates && dropdownStates[fieldName]) {
      dropdownStates[fieldName].isOpen = false;
      dropdownStates[fieldName].searchTerm = '';
    }
  }

  /**
   * Get display value for dropdown
   * @param value - Current value
   * @param defaultValue - Default value if empty
   */
  getDropdownDisplayValue(value: string, defaultValue: string = ''): string {
    return value || defaultValue;
  }

  // ================================
  // EVENT HANDLING UTILITIES
  // ================================

  /**
   * Handle main page click to close dropdowns
   * @param event - Click event
   * @param dropdownStates - Current dropdown states
   * @param exportDropdownOpen - Export dropdown state
   */
  onMainPageClick(event: Event, dropdownStates: { [key: string]: { isOpen: boolean; searchTerm: string; filteredOptions: string[] } }, exportDropdownOpen: boolean): void {
    const target = event.target as HTMLElement;
    
    // Check if click is on dropdown elements
    if (target.closest('.dropdown-menu') || target.closest('.dropdown-toggle') || target.closest('.searchable-dropdown')) {
      return;
    }
    
    // Close all dropdowns
    this.closeAllDropdowns(dropdownStates);
    if (exportDropdownOpen) {
      // This would need to be handled by the component
    }
  }

  /**
   * Handle modal body click to prevent closing
   * @param event - Click event
   */
  onModalBodyClick(event: Event): void {
    event.stopPropagation();
  }

  /**
   * Check if should use searchable dropdown
   * @param fieldName - Field name
   */
  shouldUseSearchableDropdown(fieldName: string): boolean {
    // Always use searchable dropdown for all fields
    return true;
  }

  // ================================
  // PAGINATION UTILITIES
  // ================================

  /**
   * Get paginated data
   * @param data - Full data array
   * @param currentPage - Current page number
   * @param itemsPerPage - Items per page
   */
  getPaginatedData<T>(data: T[], currentPage: number, itemsPerPage: number): T[] {
    const startIndex = (currentPage - 1) * itemsPerPage;
    const endIndex = startIndex + itemsPerPage;
    return data.slice(startIndex, endIndex);
  }

  /**
   * Get total pages
   * @param totalItems - Total number of items
   * @param itemsPerPage - Items per page
   */
  getTotalPages(totalItems: number, itemsPerPage: number): number {
    return Math.ceil(totalItems / itemsPerPage);
  }

  // ================================
  // FILTER UTILITIES
  // ================================

  /**
   * Get active filter count
   * @param filters - Filter object
   * @param searchQuery - Search query
   */
  getActiveFilterCount(filters: { [key: string]: any }, searchQuery: string = ''): number {
    return Object.values(filters).filter(v => v !== 'all' && v !== '').length + 
           (searchQuery ? 1 : 0);
  }

  // ================================
  // SORTING UTILITIES
  // ================================

  /**
   * Sort data by field
   * @param data - Data array
   * @param field - Field to sort by
   * @param direction - Sort direction
   */
  sortData<T>(data: T[], field: keyof T, direction: 'asc' | 'desc' = 'asc'): T[] {
    return [...data].sort((a, b) => {
      const aValue = a[field];
      const bValue = b[field];

      if (typeof aValue === 'string' && typeof bValue === 'string') {
        return direction === 'asc' 
          ? aValue.localeCompare(bValue) 
          : bValue.localeCompare(aValue);
      }

      if (typeof aValue === 'number' && typeof bValue === 'number') {
        return direction === 'asc' 
          ? aValue - bValue 
          : bValue - aValue;
      }

      return 0;
    });
  }

  // ================================
  // BODY SCROLL UTILITIES
  // ================================

  /**
   * Disable body scroll
   */
  disableBodyScroll(): void {
    document.body.style.overflow = 'hidden';
  }

  /**
   * Enable body scroll
   */
  enableBodyScroll(): void {
    document.body.style.overflow = '';
  }

  // ================================
  // FORM VALIDATION UTILITIES
  // ================================

  /**
   * Check if form is valid
   * @param errors - Form errors object
   */
  isFormValid(errors: { [key: string]: string }): boolean {
    return Object.keys(errors).length === 0;
  }

  /**
   * Reset form validation state
   * @param errors - Form errors object
   * @param touched - Form touched object
   * @param isSubmitting - Is submitting flag
   */
  resetFormValidation(
    errors: { [key: string]: string }, 
    touched: { [key: string]: boolean }, 
    isSubmitting: boolean
  ): void {
    Object.keys(errors).forEach(key => delete errors[key]);
    Object.keys(touched).forEach(key => delete touched[key]);
    isSubmitting = false;
  }

  /**
   * Mark all fields as touched
   * @param touched - Form touched object
   * @param fields - Array of field names
   */
  markAllFieldsAsTouched(touched: { [key: string]: boolean }, fields: string[]): void {
    fields.forEach(field => {
      touched[field] = true;
    });
  }

  // ================================
  // CALCULATION UTILITIES
  // ================================

  /**
   * Calculate unit value
   * @param totalValue - Total value
   * @param quantity - Quantity
   * @param decimals - Number of decimal places
   */
  calculateUnitValue(totalValue: number, quantity: number, decimals: number = 2): string {
    if (quantity === 0) return '0.00';
    return (totalValue / quantity).toFixed(decimals);
  }

  /**
   * Get selected items from array
   * @param items - Full items array
   * @param selectedIds - Set of selected IDs
   */
  getSelectedItemsFromArray<T extends { id: string }>(items: T[], selectedIds: Set<string>): T[] {
    return Array.from(selectedIds)
      .map(id => items.find(item => item.id === id))
      .filter((item): item is T => item !== undefined);
  }

  // ================================
  // DROPDOWN SELECTION UTILITIES
  // ================================

  /**
   * Select option for form field
   * @param formData - Form data object
   * @param fieldName - Field name
   * @param option - Selected option
   */
  selectFormOption(formData: any, fieldName: string, option: string): void {
    // If clicking on already selected option, unselect it
    if (formData[fieldName] === option) {
      formData[fieldName] = '';
    } else {
      formData[fieldName] = option;
    }
  }

  /**
   * Select filter option
   * @param filterFormData - Filter form data object
   * @param filters - Filters object
   * @param fieldName - Field name
   * @param option - Selected option
   */
  selectFilterOption(
    filterFormData: { [key: string]: any }, 
    filters: { [key: string]: any }, 
    fieldName: string, 
    option: string
  ): void {
    // Toggle behavior:
    // - If clicking the same selected option, unselect to "All" (empty string)
    // - If clicking the explicit All option ('' or 'all'), set to "All"
    // - Otherwise set to the chosen option
    const current = filterFormData[fieldName];
    if (option === '' || option === 'all') {
      filterFormData[fieldName] = '';
      filters[fieldName] = '';
    } else if (current === option) {
      filterFormData[fieldName] = '';
      filters[fieldName] = '';
    } else {
      filterFormData[fieldName] = option;
      filters[fieldName] = option;
    }
  }

  /**
   * Handle search input for dropdown
   * @param dropdownStates - Dropdown states object
   * @param fieldName - Field name
   * @param event - Input event
   * @param filterCallback - Callback to filter options
   */
  onDropdownSearchInput(
    dropdownStates: { [key: string]: { isOpen: boolean; searchTerm: string; filteredOptions: string[] } },
    fieldName: string,
    event: Event,
    filterCallback: (fieldName: string) => void
  ): void {
    const target = event.target as HTMLInputElement;
    dropdownStates[fieldName].searchTerm = target.value;
    filterCallback(fieldName);
  }

  /**
   * Get filter display value
   * @param filterFormData - Filter form data object
   * @param fieldName - Field name
   */
  getFilterDisplayValue(filterFormData: { [key: string]: any }, fieldName: string): string {
    const value = filterFormData[fieldName];
    return value || '';
  }

  // ================================
  // EVENT HANDLING UTILITIES
  // ================================

  /**
   * Handle form field focus
   * @param dropdownStates - Current dropdown states
   */
  onFormFieldFocus(dropdownStates: { [key: string]: { isOpen: boolean; searchTerm: string; filteredOptions: string[] } }): void {
    this.closeAllDropdowns(dropdownStates);
  }


  /**
   * Generate unique ID
   * @param prefix - ID prefix
   * @param length - ID length
   */
  generateId(prefix: string = 'ID', length: number = 4): string {
    const randomNum = Math.floor(Math.random() * Math.pow(10, length));
    return `${prefix}-${String(randomNum).padStart(length, '0')}`;
  }

  /**
   * Deep clone object
   * @param obj - Object to clone
   */
  deepClone<T>(obj: T): T {
    return JSON.parse(JSON.stringify(obj));
  }

  /**
   * Check if value is empty
   * @param value - Value to check
   */
  isEmpty(value: any): boolean {
    if (value === null || value === undefined) return true;
    if (typeof value === 'string') return value.trim() === '';
    if (Array.isArray(value)) return value.length === 0;
    if (typeof value === 'object') return Object.keys(value).length === 0;
    return false;
  }

  /**
   * Debounce function
   * @param func - Function to debounce
   * @param wait - Wait time in milliseconds
   */
  debounce<T extends (...args: any[]) => any>(func: T, wait: number): T {
    let timeout: any;
    return ((...args: any[]) => {
      clearTimeout(timeout);
      timeout = setTimeout(() => func.apply(this, args), wait);
    }) as T;
  }

  /**
   * Throttle function
   * @param func - Function to throttle
   * @param limit - Limit time in milliseconds
   */
  throttle<T extends (...args: any[]) => any>(func: T, limit: number): T {
    let inThrottle: boolean;
    return ((...args: any[]) => {
      if (!inThrottle) {
        func.apply(this, args);
        inThrottle = true;
        setTimeout(() => inThrottle = false, limit);
      }
    }) as T;
  }

  // ================================
  // ARRAY UTILITIES
  // ================================

  /**
   * Remove duplicates from array
   * @param array - Array to process
   * @param key - Key to check for duplicates
   */
  removeDuplicates<T>(array: T[], key?: keyof T): T[] {
    if (!key) {
      return [...new Set(array)];
    }
    
    const seen = new Set();
    return array.filter(item => {
      const value = item[key];
      if (seen.has(value)) {
        return false;
      }
      seen.add(value);
      return true;
    });
  }

  /**
   * Group array by key
   * @param array - Array to group
   * @param key - Key to group by
   */
  groupBy<T>(array: T[], key: keyof T): { [key: string]: T[] } {
    return array.reduce((groups, item) => {
      const value = String(item[key]);
      if (!groups[value]) {
        groups[value] = [];
      }
      groups[value].push(item);
      return groups;
    }, {} as { [key: string]: T[] });
  }

  /**
   * Sort array by key
   * @param array - Array to sort
   * @param key - Key to sort by
   * @param direction - Sort direction
   */
  sortBy<T>(array: T[], key: keyof T, direction: 'asc' | 'desc' = 'asc'): T[] {
    return [...array].sort((a, b) => {
      const aValue = a[key];
      const bValue = b[key];
      
      if (typeof aValue === 'string' && typeof bValue === 'string') {
        return direction === 'asc' 
          ? aValue.localeCompare(bValue) 
          : bValue.localeCompare(aValue);
      }
      
      if (typeof aValue === 'number' && typeof bValue === 'number') {
        return direction === 'asc' 
          ? aValue - bValue 
          : bValue - aValue;
      }
      
      return 0;
    });
  }

  // ================================
  // OBJECT UTILITIES
  // ================================

  /**
   * Get nested object value
   * @param obj - Object to search
   * @param path - Path to value
   */
  getNestedValue(obj: any, path: string): any {
    return path.split('.').reduce((current, key) => current?.[key], obj);
  }

  /**
   * Set nested object value
   * @param obj - Object to modify
   * @param path - Path to value
   * @param value - Value to set
   */
  setNestedValue(obj: any, path: string, value: any): void {
    const keys = path.split('.');
    const lastKey = keys.pop()!;
    const target = keys.reduce((current, key) => {
      if (!current[key]) current[key] = {};
      return current[key];
    }, obj);
    target[lastKey] = value;
  }

  /**
   * Pick specific keys from object
   * @param obj - Source object
   * @param keys - Keys to pick
   */
  pick<T extends object, K extends keyof T>(obj: T, keys: K[]): Pick<T, K> {
    const result = {} as Pick<T, K>;
    keys.forEach(key => {
      if (key in obj) {
        result[key] = obj[key];
      }
    });
    return result;
  }

  /**
   * Omit specific keys from object
   * @param obj - Source object
   * @param keys - Keys to omit
   */
  omit<T, K extends keyof T>(obj: T, keys: K[]): Omit<T, K> {
    const result = { ...obj };
    keys.forEach(key => {
      delete result[key];
    });
    return result;
  }
}
