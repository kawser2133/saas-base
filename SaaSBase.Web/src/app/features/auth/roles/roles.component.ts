import { Component, OnInit, OnDestroy, HostListener, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RolesService, Role, CreateRoleRequest, UpdateRoleRequest, RoleStatistics, ImportJobStatus, ImportExportHistory, ImportExportHistoryResponse, ExportJobStatus, Permission, User, RoleHierarchy } from '../../../core/services/roles.service';
import { UsersService } from '../../../core/services/users.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { AuthorizationService } from '../../../core/services/authorization.service';
import { UserContextService } from '../../../core/services/user-context.service';
import { HttpClient } from '@angular/common/http';
import { Subscription, forkJoin, timer } from 'rxjs';
import jsPDF from 'jspdf';
import { environment } from '../../../../environments/environment';
import html2canvas from 'html2canvas';

@Component({
  selector: 'app-roles',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, NotificationContainerComponent, BreadcrumbComponent, HasPermissionDirective],
  templateUrl: './roles.component.html',
  styleUrls: ['./roles.component.scss']
})
export class RolesComponent implements OnInit, OnDestroy {
  // Utility references
  Math = Math;
  document = document;

  // Subscriptions
  private subscriptions = new Subscription();

  // Data properties
  items: Role[] = [];
  filteredItems: Role[] = [];
  paginatedData: Role[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: CreateRoleRequest & UpdateRoleRequest & { id?: string } = {
    name: '',
    description: '',
    roleType: 'CUSTOM',
    parentRoleId: '',
    sortOrder: 0,
    color: '#3B82F6',
    icon: 'fas fa-user',
    isActive: true
  };
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: Role | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '', roleType: '', parentRoleId: '', organizationId: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', roleType: '', parentRoleId: '', organizationId: '' };
  showFilters = false;
  searchQuery = '';
  
  // Organizations list for System Admin
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
  sortDirection: 'asc' | 'desc' = 'desc';

  // Dropdowns
  dropdownStates: { [key: string]: any } = {};
  exportDropdownOpen = false;

  // Dropdown options from API
  roleTypes: string[] = ['SYSTEM', 'CUSTOM', 'INHERITED'];
  parentRoles: Role[] = [];

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
  historyType: 'import' | 'export' | undefined = undefined; // undefined = show both
  isLoadingHistory = false;

  // NEW: Export job tracking properties
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
  statistics: RoleStatistics = {
    total: 0,
    active: 0,
    inactive: 0,
    systemRoles: 0,
    businessRoles: 0
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
    private rolesService: RolesService,
    private usersService: UsersService,
    private commonUtility: CommonUtilityService,
    private authorizationService: AuthorizationService,
    private userContextService: UserContextService,
    private http: HttpClient,
    private fb: FormBuilder,
    private cdr: ChangeDetectorRef
  ) {
    this.initializeRoleForm();
  }

  private initializeRoleForm(): void {
    this.roleForm = this.fb.group({
      name: ['', Validators.required],
      description: [''],
      roleType: ['CUSTOM', Validators.required],
      parentRoleId: [''],
      sortOrder: [0],
      color: ['#3B82F6'],
      icon: ['fas fa-user'],
      isActive: [true]
    });
  }

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeDropdownStates();
    
    // Check if user is System Admin
    this.isSystemAdmin = this.userContextService.isSystemAdmin();
    
    // Load organizations if System Admin
    if (this.isSystemAdmin) {
      this.loadOrganizations();
    }
    
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
          this.canCreate = this.authorizationService.hasPermission('Roles.Create');
          this.canUpdate = this.authorizationService.hasPermission('Roles.Update');
          this.canDelete = this.authorizationService.hasPermission('Roles.Delete');
          this.canExport = this.authorizationService.hasPermission('Roles.Export');
          this.canImport = this.authorizationService.hasPermission('Roles.Import');
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

    // Don't close anything if clicking on modal buttons
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
    const fields = ['roleType', 'isActive', 'parentRoleId', 'filter-roleType', 'filter-parentRoleId', 'filter-status', 'items-per-page'];
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
      case 'roleType': return this.roleTypes;
      case 'parentRoleId': return this.parentRoles.map(r => r.id);
      case 'isActive': return ['true', 'false'];
      case 'filter-roleType': return this.roleTypes;
      case 'filter-parentRoleId': return this.parentRoles.map(r => r.id);
      case 'filter-organizationId': return this.organizations;
      case 'filter-status': return ['', 'true', 'false'];
      case 'items-per-page': return ['10', '25', '50', '100'];
      default: return [];
    }
  }

  // Data loading
  loadData(): void {
    this.isLoading = true;
    this.loading = true;

    const params: any = {
      page: this.currentPage,
      pageSize: this.itemsPerPage,
      search: this.searchQuery,
      sortField: this.sortField,
      sortDirection: this.sortDirection,
      roleType: this.filters.roleType,
      isActive: this.filters.isActive,
      parentRoleId: this.filters.parentRoleId,
      createdFrom: this.filters.createdFrom || undefined,
      createdTo: this.filters.createdTo || undefined
    };

    // Add organizationId filter for System Admin
    if (this.isSystemAdmin && this.filters.organizationId) {
      params.organizationId = this.filters.organizationId;
    }

    const sub = this.rolesService.getData(params).subscribe({
      next: (response: any) => {
        this.items = response.items?.map((item: any) => ({
          ...item,
          id: item.id || item.Id,
          name: item.name || item.Name,
          organizationId: item.organizationId || item.OrganizationId,
          organizationName: item.organizationName || item.OrganizationName,
          showDropdown: false,
          dropdownUp: false
        })) || [];
        this.totalItems = response.totalCount || 0;
        this.totalCount = response.totalCount || 0;
        this.totalPages = response.totalPages || Math.ceil(this.totalItems / this.itemsPerPage);
        this.filteredItems = [...this.items];
        this.paginatedData = [...this.items];
        this.isLoading = false;
        this.loading = false;

        // Force change detection
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Error loading roles:', error);
        this.notificationService.error('Error', 'Failed to load roles. Please try again.');
        this.isLoading = false;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
    this.subscriptions.add(sub);
  }

  loadStatistics(): void {
    const sub = this.rolesService.getStatistics().subscribe({
      next: (stats: any) => {
        this.statistics = {
          total: stats.totalRoles || stats.TotalRoles || 0,
          active: stats.activeRoles || stats.ActiveRoles || 0,
          inactive: stats.inactiveRoles || stats.InactiveRoles || 0,
          systemRoles: stats.systemRoles || stats.SystemRoles || 0,
          businessRoles: stats.businessRoles || stats.BusinessRoles || 0
        };
      },
      error: (error) => {
        console.error('Error loading statistics:', error);
        // Don't show error notification for statistics as it's non-critical
      }
    });
    this.subscriptions.add(sub);
  }

  loadDropdownOptions(): void {
    const sub = this.rolesService.getDropdownOptions().subscribe({
      next: (options) => {
        this.roleTypes = options.roleTypes || ['SYSTEM', 'CUSTOM', 'INHERITED'];
        this.parentRoles = options.parentRoles || [];
      },
      error: (error: any) => {
        console.error('Error loading dropdown options:', error);
        this.notificationService.error('Error', 'Failed to load dropdown options');
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
    this.searchQuery = this.searchTerm;
    clearTimeout(this.searchTimeout);
    this.searchTimeout = setTimeout(() => {
      this.applyFilters();
    }, 300);
  }

  onFilterChange(): void {
    this.applyFilters();
  }

  onDateFilterChange(): void {
    this.filters.createdFrom = this.createdFromFilter;
    this.filters.createdTo = this.createdToFilter;
    this.filterFormData.createdFrom = this.createdFromFilter;
    this.filterFormData.createdTo = this.createdToFilter;
    this.applyFilters();
  }

  clearFilters(): void {
    this.searchQuery = '';
    this.searchTerm = '';
    this.filters = { createdFrom: '', createdTo: '', roleType: '', parentRoleId: '', organizationId: '' };
    this.filterFormData = { createdFrom: '', createdTo: '', roleType: '', parentRoleId: '', organizationId: '' };
    this.roleTypeFilter = '';
    this.isActiveFilter = null;
    this.createdFromFilter = '';
    this.createdToFilter = '';
    this.applyFilters();
  }

  refreshData(): void {
    
    this.loadData();
    this.loadStatistics();
  }

  get activeFilterCount(): number {
    let count = 0;
    if (this.searchQuery) count++;
    if (this.filters.roleType) count++;
    if (this.filters.parentRoleId) count++;
    if (this.filters.isActive !== undefined) count++;
    if (this.filters.organizationId) count++;
    if (this.filters.createdFrom) count++;
    if (this.filters.createdTo) count++;
    return count;
  }
  
  // Check if user can edit/delete this item (System Admin can only edit their own organization's data)
  canEditItem(item: Role): boolean {
    if (!this.canUpdate) return false;
    if (!this.isSystemAdmin) return true; // Regular users can edit their org's data
    // System Admin can only edit their own organization's data
    const currentOrgId = this.userContextService.getCurrentOrganizationId();
    return item.organizationId === currentOrgId;
  }

  canDeleteItem(item: Role): boolean {
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

  onPageSizeChange(newSize?: number): void {
    if (newSize) {
      this.itemsPerPage = newSize;
    }
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
    if (!this.canCreate) {
      this.notificationService.error('Access Denied', 'You do not have permission to create roles.');
      return;
    }
    this.dialogMode = 'add';
    this.errors = {};
    
    // Reset the reactive form
    this.roleForm.reset({
      name: '',
      description: '',
      roleType: 'CUSTOM',
      parentRoleId: '',
      sortOrder: 0,
      color: '#3B82F6',
      icon: 'fas fa-user',
      isActive: true
    });
    
    // Sync formData with reactive form
    this.formData = {
      name: '',
      description: '',
      roleType: 'CUSTOM',
      parentRoleId: '',
      sortOrder: 0,
      color: '#3B82F6',
      icon: 'fas fa-user',
      isActive: true
    };
    
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.showRoleModal = true;
    this.selectedRole = null;
    this.disableBodyScroll();
  }

  openEditDialog(item: Role): void {
    if (!this.canEditItem(item)) {
      if (!this.canUpdate) {
        this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
      } else {
        this.notificationService.error('Access Denied', 'You can only edit roles from your own organization.');
      }
      return;
    }
    this.dialogMode = 'edit';
    this.selectedItem = item;
    this.selectedRole = item;
    this.errors = {};
    
    // Update the reactive form
    this.roleForm.patchValue({
      name: item.name,
      description: item.description || '',
      roleType: item.roleType,
      parentRoleId: item.parentRoleId || '',
      sortOrder: item.sortOrder,
      color: item.color || '#3B82F6',
      icon: item.icon || 'fas fa-user',
      isActive: item.isActive
    });
    
    // Sync formData with reactive form
    this.formData = {
      id: item.id,
      name: item.name,
      description: item.description || '',
      roleType: item.roleType,
      parentRoleId: item.parentRoleId || '',
      sortOrder: item.sortOrder,
      color: item.color || '#3B82F6',
      icon: item.icon || 'fas fa-user',
      isActive: item.isActive
    };
    
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.showRoleModal = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: Role): void {
    // Fetch fresh detail data to ensure metadata is up-to-date
    const sub = this.rolesService.getRoleById(item.id).subscribe({
      next: (roleDetail: Role) => {
        this.selectedItem = roleDetail;
        this.selectedRole = roleDetail;
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading role details:', error);
        // Fallback to list item if detail fetch fails
        this.selectedItem = item;
        this.selectedRole = item;
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    this.subscriptions.add(sub);
  }

  openDeleteDialog(items: Role[]): void {
    // Filter out items that cannot be deleted
    const deletableItems = items.filter(item => this.canDeleteItem(item));
    
    if (deletableItems.length === 0) {
      this.notificationService.error('Access Denied', 'You can only delete roles from your own organization.');
      return;
    }
    
    if (deletableItems.length < items.length) {
      this.notificationService.warning('Partial Selection', `Only ${deletableItems.length} of ${items.length} selected roles can be deleted. Roles from other organizations will be skipped.`);
    }
    
    // Clear current selection and add the items to be deleted
    this.selectedItems.clear();
    deletableItems.forEach(item => this.selectedItems.add(item.id));
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notificationService.warning('Warning', 'Please select at least one role to clone');
      return;
    }

    this.notificationService.info('Info', 'Cloning roles...');
    this.rolesService.cloneMultiple(selectedIds).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || `${selectedIds.length} role(s) cloned successfully`);
        this.selectedItems.clear();
        this.loadData();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone roles');
      }
    });
  }

  cloneRole(role: any): void {
    this.notificationService.info('Info', 'Cloning role...');
    this.rolesService.cloneMultiple([role.id]).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || 'Role cloned successfully');
        this.loadData();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone role');
      }
    });
  }

  openImportDialog(): void {
    if (!this.canImport) {
      this.notificationService.error('Access Denied', 'You do not have permission to import roles.');
      return;
    }
    this.showImportDialog = true;
    this.selectedFile = null;
    this.importErrors = [];
    this.disableBodyScroll();
  }

  closeDialogs(): void {
    this.showAddEditDialog = false;
    this.showViewDialog = false;
    this.showDeleteDialog = false;
    this.showRoleModal = false;
    this.selectedItem = null;
    this.selectedRole = null;
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
    // Mark all form fields as touched to show validation errors
    this.roleForm.markAllAsTouched();
    
    // Check if the reactive form is valid
    if (this.roleForm.invalid) {
      this.notificationService.error('Validation Error', 'Please fix the validation errors before saving');
      return;
    }
    
    // Additional custom validation
    this.validateForm();
    if (!this.isFormValid()) {
      this.notificationService.error('Validation Error', 'Please fix the validation errors before saving');
      return;
    }

    this.isSubmitting = true;
    this.saving = true;
    const formValue = this.roleForm.value;

    if (this.dialogMode === 'add') {
      if (!this.canCreate) {
        this.notificationService.error('Access Denied', 'You do not have permission to create roles.');
        this.isSubmitting = false;
        this.saving = false;
        return;
      }
      const createRequest: CreateRoleRequest = {
        name: formValue.name,
        description: formValue.description,
        roleType: formValue.roleType,
        parentRoleId: formValue.parentRoleId || undefined,
        sortOrder: formValue.sortOrder,
        color: formValue.color,
        icon: formValue.icon,
        isActive: formValue.isActive
      };

      this.rolesService.create(createRequest).subscribe({
        next: (newRole: Role) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Role created successfully!');
          this.isSubmitting = false;
          this.saving = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error creating role:', error);
          if (error.status === 403) {
            this.notificationService.error('Access Denied', 'You do not have permission to create roles.');
          } else {
            this.notificationService.error('Error', error.error?.message || 'Failed to create role. Please try again.');
          }
          this.isSubmitting = false;
          this.saving = false;
        }
      });
    } else {
      if (!this.canUpdate) {
        this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
        this.isSubmitting = false;
        this.saving = false;
        return;
      }
      const updateRequest: UpdateRoleRequest = {
        name: formValue.name,
        description: formValue.description,
        roleType: formValue.roleType,
        parentRoleId: formValue.parentRoleId || undefined,
        sortOrder: formValue.sortOrder,
        color: formValue.color,
        icon: formValue.icon,
        isActive: formValue.isActive
      };

      this.rolesService.update(this.formData.id!, updateRequest).subscribe({
        next: (updatedRole: Role) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'Role updated successfully!');
          this.isSubmitting = false;
          this.saving = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error updating role:', error);
          if (error.status === 403) {
            this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
          } else {
            this.notificationService.error('Error', error.error?.message || 'Failed to update role. Please try again.');
          }
          this.isSubmitting = false;
          this.saving = false;
        }
      });
    }
  }

  saveRole(): void {
    this.saveItem();
  }

  confirmDelete(): void {
    if (!this.canDelete) {
      this.notificationService.error('Access Denied', 'You do not have permission to delete roles.');
      return;
    }
    
    // Filter out items that cannot be deleted (System Admin can only delete their own org's roles)
    const allItems = this.items.filter(item => this.selectedItems.has(item.id));
    const deletableItems = allItems.filter(item => this.canDeleteItem(item));
    
    if (deletableItems.length === 0) {
      this.notificationService.error('Access Denied', 'You can only delete roles from your own organization.');
      this.closeDialogs();
      return;
    }
    
    if (deletableItems.length < allItems.length) {
      this.notificationService.warning('Partial Selection', `Only ${deletableItems.length} of ${allItems.length} selected roles can be deleted. Roles from other organizations will be skipped.`);
    }
    
    const itemsToDelete = deletableItems.map(item => item.id);

    if (itemsToDelete.length === 1) {
      this.rolesService.delete(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', 'Role deleted successfully!');
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting role:', error);
          if (error.status === 403) {
            this.notificationService.error('Access Denied', 'You do not have permission to delete roles.');
          } else {
            const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete role. Please try again.';
            this.notificationService.error('Error', errorMessage);
          }
        }
      });
    } else {
      const ids = itemsToDelete.map((id: string) => id);
      this.rolesService.deleteMultiple(ids).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', `${itemsToDelete.length} roles deleted successfully`);
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting roles:', error);
          if (error.status === 403) {
            this.notificationService.error('Access Denied', 'You do not have permission to delete roles.');
          } else {
            const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete roles. Please try again.';
            this.notificationService.error('Error', errorMessage);
          }
        }
      });
    }
  }

  // Async export with progress tracking
  startAsyncExport(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
    if (!this.canExport) {
      this.notificationService.error('Access Denied', 'You do not have permission to export roles.');
      return;
    }
    const formatMap: { [key: string]: 1 | 2 | 3 | 4 } = {
      'excel': 1,
      'csv': 2,
      'pdf': 3,
      'json': 4
    };

    const params: any = {
      format: formatMap[format],
      search: this.searchQuery || undefined,
      roleType: this.filters.roleType || undefined,
      isActive: this.filters.isActive,
      parentRoleId: this.filters.parentRoleId && this.filters.parentRoleId !== 'all' && this.filters.parentRoleId !== '' ? this.filters.parentRoleId : undefined,
      createdFrom: this.filters.createdFrom || undefined,
      createdTo: this.filters.createdTo || undefined
    };

    // Add selected IDs if any are selected
    if (this.selectedItems.size > 0) {
      params.selectedIds = Array.from(this.selectedItems);
    }

    this.isExporting = true;
    this.exportProgress = 0;
    this.exportStatus = 'Pending';
    this.showExportDialog = true;

    this.rolesService.startExportAsync(params).subscribe({
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

      this.rolesService.getExportJobStatus(this.exportJobId).subscribe({
        next: (status: ExportJobStatus) => {
          this.exportStatus = status.status;
          this.exportProgress = status.progressPercent;
          this.exportFormat = status.format; // Store format for later use

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isExporting = false;

            if (status.status === 'Completed') {
              if (status.totalRows > 0) {
                this.notificationService.success('Export Completed', `Exported ${status.totalRows} records successfully!`);
                if (this.exportJobId) {
                  this.downloadCompletedExport(this.exportJobId, status.format);
                }
              } else {
                this.notificationService.warning('Export Completed', 'No data available for export with current filters.');
              }
            } else {
              let errorMessage = status.message || 'Export failed.';
              if (status.totalRows === 0) {
                errorMessage = 'No data available for export with current filters.';
              }
              this.notificationService.error('Export Failed', errorMessage);
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
          this.notificationService.error('Error', 'Export status check failed.');
        }
      });
    }, 3000);
  }

  private downloadCompletedExport(jobId: string, format: string): void {
    this.rolesService.downloadExport(jobId).subscribe({
      next: (blob: Blob) => {
        // Determine file extension based on format
        const extension = this.getFileExtension(format);

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `roles_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.rolesService.getTemplate().subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'roles_import_template.xlsx';
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
      this.notificationService.warning('Invalid File', 'Please upload a valid Excel (.xlsx, .xls) or CSV (.csv) file');
      return;
    }

    // Validate file size (5MB max as mentioned in UI)
    const maxSize = 5 * 1024 * 1024; // 5MB
    if (file.size > maxSize) {
      this.notificationService.warning('File Too Large', `File size should not exceed 5MB. Your file is ${(file.size / (1024 * 1024)).toFixed(2)}MB`);
      return;
    }

    this.selectedFile = file;
    this.importErrors = []; // Clear any previous errors
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

    // Start async job
    this.rolesService.startImportAsync(formData).subscribe({
      next: (res) => {
        this.importJobId = res.jobId;
        this.notificationService.info('Import Started', 'Your import is processing in the background. Check Import History for progress.');
        this.closeImportDialog(); // Close dialog immediately
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

      this.rolesService.getImportJobStatus(this.importJobId).subscribe({
        next: (status: ImportJobStatus) => {
          this.importStatus = status.status;
          this.importProgress = status.progressPercent ?? 0;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isImporting = false;
            this.importJobId = null;

            if (status.status === 'Completed') {
              this.notificationService.success('Import Completed', `Imported ${status.successCount} roles${status.skippedCount ? `, skipped ${status.skippedCount}` : ''}${status.errorCount ? `, errors ${status.errorCount}` : ''}.`);
            } else if (status.status === 'Failed') {
              let errorMessage = status.message || 'Import failed.';
              if (status.totalRows === 0 && status.processedRows === 0) {
                errorMessage = 'File parsing failed. Please check that your file has the correct format and required headers. Download the template for reference.';
              } else if (status.totalRows > 0 && status.processedRows === 0) {
                errorMessage = 'No valid data found in the file. Please check your data format and try again.';
              }
              this.notificationService.error('Import Failed', errorMessage);

              // If there are errors, show additional info
              if (status.errorCount > 0) {
                this.notificationService.info('Error Details', `Check Import History to download detailed error report (${status.errorCount} errors found).`);
              }
            }

            // Refresh list and history
            this.loadData();
            this.loadStatistics();
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
          this.notificationService.error('Error', 'Import job status check failed.');
        }
      });
    }, 5000); // Reduced to 5 seconds to reduce backend load
  }

  downloadErrorReport(): void {
    if (!this.errorReportId) {
      this.notificationService.warning('Warning', 'No error report available');
      return;
    }

    this.rolesService.getImportErrorReport(this.errorReportId).subscribe({
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
    // Find the history item to get the errorReportId
    const history = this.history.find(h => h.id === historyId);
    if (!history || !history.errorReportId) {
      this.notificationService.warning('Warning', 'No error report available for this import');
      return;
    }

    this.rolesService.getImportErrorReport(history.errorReportId).subscribe({
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

    // Download using the jobId
    this.rolesService.downloadExport(history.jobId).subscribe({
      next: (blob: Blob) => {
        // Determine file extension based on format
        const extension = this.getFileExtension(history.format);

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = history.fileName || `roles_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.historyType = undefined; // Show all by default
    this.loadHistory();
    this.disableBodyScroll();
  }

  // NEW: Unified history loading
  loadHistory(): void {
    this.isLoadingHistory = true;
    this.rolesService.getHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
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

  // Role-specific actions
  toggleActiveStatus(role: Role): void {
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
      return;
    }
    const newStatus = !role.isActive;
    this.rolesService.setActive(role.id, newStatus).subscribe({
      next: () => {
        this.notificationService.success('Success', `Role ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        console.error('Error toggling active status:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
        } else {
          this.notificationService.error('Error', 'Failed to update role status.');
        }
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
    // Only show progress for records that are actually being processed
    if (!this.isImporting || !this.importJobId) return false;

    // Check if this is a very recent import (within last 2 minutes) and still processing
    const historyTime = new Date(history.createdAtUtc).getTime();
    const now = Date.now();
    const isVeryRecent = (now - historyTime) < 120000; // 2 minutes

    // Only show progress for records that are still processing (not completed)
    const isStillProcessing = history.status === 'Processing' || history.status === 'Pending';

    return isVeryRecent && isStillProcessing;
  }

  getStatusClass(isActive: boolean): string {
    return isActive ? 'status-active' : 'status-inactive';
  }

  getStatusIcon(isActive: boolean): string {
    return isActive ? 'fa-check-circle' : 'fa-times-circle';
  }

  getBadgeClass(verified: boolean): string {
    return verified ? 'badge-success' : 'badge-warning';
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
  toggleDropdown(item: Role, event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }

    this.exportDropdownOpen = false;

    this.items.forEach((dataItem: Role) => {
      if (dataItem.id !== item.id) {
        dataItem.showDropdown = false;
      }
    });

    item.showDropdown = !item.showDropdown;
  }

  closeAllDropdowns(): void {
    this.items.forEach((item: Role) => {
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

    // Special handling for parentRoleId - filter by role name
    if (fieldName === 'parentRoleId' || fieldName === 'filter-parentRoleId') {
      const filteredRoles = this.parentRoles.filter(role =>
        role.name.toLowerCase().includes(searchTerm.toLowerCase())
      );
      this.dropdownStates[fieldName].filteredOptions = filteredRoles.map(r => r.id);
    } else {
      const allOptions = this.getOptionsForField(fieldName);
      this.dropdownStates[fieldName].filteredOptions = this.commonUtility.filterDropdownOptions(allOptions, searchTerm);
    }
  }

  selectOption(fieldName: string, option: string): void {
    // Update both formData and reactive form
    this.commonUtility.selectFormOption(this.formData, fieldName, option);
    this.roleForm.patchValue({ [fieldName]: option });
    this.closeAllSearchableDropdowns();
    this.validateField(fieldName);
  }

  selectFilterOption(fieldName: string, option: string | boolean | null | any): void {
    // Handle organizationId filter which uses object with id property
    if (fieldName === 'organizationId' && typeof option === 'object' && option.id) {
      option = option.id;
    }
    
    // Special handling for status filter - convert string to boolean
    if (fieldName === 'status') {
      if (option === null || option === '') {
        this.filterFormData.status = '';
        this.filters.isActive = undefined;
        this.isActiveFilter = null;
      } else if (typeof option === 'boolean') {
        this.filterFormData.status = option ? 'true' : 'false';
        this.filters.isActive = option;
        this.isActiveFilter = option;
      } else {
        this.filterFormData.status = option;
        this.filters.isActive = option === 'true';
        this.isActiveFilter = option === 'true';
      }
    } else if (fieldName === 'roleType') {
      this.filterFormData.roleType = option as string;
      this.filters.roleType = option as string;
      this.roleTypeFilter = option as string;
    } else {
      this.commonUtility.selectFilterOption(this.filterFormData, this.filters, fieldName, option as string);
    }
    this.closeAllSearchableDropdowns();
    this.onFilterChange();
  }

  selectItemsPerPageOption(option: string): void {
    this.itemsPerPageFormData = parseInt(option);
    this.itemsPerPage = parseInt(option);
    this.closeAllSearchableDropdowns();
    this.onPageSizeChange();
  }

  onSearchInput(fieldName: string, event: Event): void {
    this.commonUtility.onDropdownSearchInput(this.dropdownStates, fieldName, event, (fieldName: string) => this.filterOptions(fieldName));
  }

  getDisplayValue(fieldName: string): string {
    const value = (this.formData as any)[fieldName];

    // Special handling for parentRoleId - display role name instead of ID
    if (fieldName === 'parentRoleId' && value) {
      const role = this.parentRoles.find(r => r.id === value);
      return role ? role.name : value;
    }

    return this.commonUtility.getDropdownDisplayValue(value);
  }

  getParentRoleName(roleId: string): string {
    const role = this.parentRoles.find(r => r.id === roleId);
    return role ? role.name : '';
  }

  getFilterDisplayValue(fieldName: string): string {
    if (fieldName === 'organizationId' && this.filterFormData.organizationId) {
      const org = this.organizations.find(o => o.id === this.filterFormData.organizationId);
      return org ? org.name : '';
    }
    // Special handling for parentRoleId - display role name instead of ID
    if (fieldName === 'parentRoleId') {
      if (!this.filterFormData.parentRoleId || this.filterFormData.parentRoleId === '' || this.filterFormData.parentRoleId === 'all') {
        return '';
      }
      const role = this.parentRoles.find(r => r.id === this.filterFormData.parentRoleId);
      return role ? role.name : this.filterFormData.parentRoleId;
    }
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
   * @param fieldName - Field name (e.g., 'filter-roleType', 'parentRoleId', etc.)
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
        this.isActiveFilter = null;
      } else if (actualField === 'roleType') {
        this.filterFormData.roleType = '';
        this.filters.roleType = '';
        this.roleTypeFilter = '';
      } else if (actualField === 'organizationId') {
        this.filterFormData.organizationId = '';
        this.filters.organizationId = '';
      } else {
        this.filterFormData[actualField] = '';
        this.filters[actualField] = '';
      }
      this.onFilterChange();
    } else {
      // Form dropdowns
      (this.formData as any)[fieldName] = '';
      this.roleForm.patchValue({ [fieldName]: '' });
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
        return this.isActiveFilter !== null;
      } else if (actualField === 'roleType') {
        return !!this.roleTypeFilter;
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

  getSelectedItems(): Role[] {
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
    const formValue = this.roleForm.value;

    if (!formValue.name || formValue.name.trim() === '') {
      this.errors['name'] = 'Role name is required';
    }

    if (!formValue.roleType || formValue.roleType.trim() === '') {
      this.errors['roleType'] = 'Role type is required';
    }
  }

  validateField(fieldName: string): void {
    const formValue = this.roleForm.value;
    
    switch (fieldName) {
      case 'name':
        if (!formValue.name || formValue.name.trim() === '') {
          this.errors['name'] = 'Role name is required';
        } else {
          delete this.errors['name'];
        }
        break;

      case 'roleType':
        if (!formValue.roleType || formValue.roleType.trim() === '') {
          this.errors['roleType'] = 'Role type is required';
        } else {
          delete this.errors['roleType'];
        }
        break;

      case 'description':
        delete this.errors['description'];
        break;

      case 'parentRoleId':
        delete this.errors['parentRoleId'];
        break;
    }
  }

  private markAllFieldsAsTouched(): void {
    const fields = ['name', 'description', 'roleType', 'parentRoleId', 'sortOrder', 'color', 'icon'];
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
  exportRoleDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notificationService.warning('Warning', 'No role selected to export');
      return;
    }

    // Get the modal content - we'll capture just the content, not the header/buttons
    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const roleDetailsCards = document.querySelector('.modal-overlay .modal .profile-form') as HTMLElement;

    if (!modalBody || !roleDetailsCards) {
      this.notificationService.error('Error', 'Unable to find content to export');
      return;
    }

    // Show loading notification
    this.notificationService.info('Info', 'Generating PDF...');

    // Store original styles
    const originalModalBodyOverflow = modalBody.style.overflow;
    const originalModalBodyHeight = modalBody.style.height;
    const originalModalBodyMaxHeight = modalBody.style.maxHeight;
    const originalModalActionsDisplay = modalBody.querySelector('.modal-actions') ? (modalBody.querySelector('.modal-actions') as HTMLElement).style.display : '';

    // Temporarily hide modal actions (Close/Edit buttons) and modify styles to show full content
    const modalActions = modalBody.querySelector('.modal-actions') as HTMLElement;
    if (modalActions) {
      modalActions.style.display = 'none';
    }

    modalBody.style.overflow = 'visible';
    modalBody.style.height = 'auto';
    modalBody.style.maxHeight = 'none';

    // Small delay to ensure DOM updates are complete
    setTimeout(() => {
      // Use html2canvas to capture the entity-details-cards div with full content
      html2canvas(roleDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: false,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: roleDetailsCards.scrollWidth,
        windowHeight: roleDetailsCards.scrollHeight,
        logging: false,
        imageTimeout: 15000,
        onclone: (clonedDoc) => {
          // Ensure images are loaded in cloned document
          const images = clonedDoc.querySelectorAll('img');
          images.forEach((img: HTMLImageElement) => {
            if (img.src && !img.complete) {
              img.crossOrigin = 'anonymous';
            }
          });
        }
      }).then(canvas => {
      // Restore original styles
      modalBody.style.overflow = originalModalBodyOverflow;
      modalBody.style.height = originalModalBodyHeight;
      modalBody.style.maxHeight = originalModalBodyMaxHeight;
      if (modalActions) {
        modalActions.style.display = originalModalActionsDisplay;
      }

      // Create PDF
      const imgData = canvas.toDataURL('image/png');
      const pdf = new jsPDF('p', 'mm', 'a4');

      // Calculate dimensions
      const imgWidth = 210; // A4 width in mm
      const pageHeight = 295; // A4 height in mm
      const imgHeight = (canvas.height * imgWidth) / canvas.width;
      let heightLeft = imgHeight;

      let position = 0;

      // Add image to PDF
      pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
      heightLeft -= pageHeight;

      // Add new page if content is longer than one page
      while (heightLeft >= 0) {
        position = heightLeft - imgHeight;
        pdf.addPage();
        pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
        heightLeft -= pageHeight;
      }

      // Save the PDF
      pdf.save(`role-details-${this.selectedItem?.name.replace(/\s+/g, '-').toLowerCase()}.pdf`);

      this.notificationService.success('Success', 'PDF exported successfully');
    }).catch((error: any) => {
      // Restore original styles in case of error
      modalBody.style.overflow = originalModalBodyOverflow;
      modalBody.style.height = originalModalBodyHeight;
      modalBody.style.maxHeight = originalModalBodyMaxHeight;
      if (modalActions) {
        modalActions.style.display = originalModalActionsDisplay;
      }

      console.error('PDF export error:', error);
      this.notificationService.error('Error', 'Failed to export PDF. Please try again.');
    });
    }, 100); // 100ms delay to ensure DOM updates
  }

  // Helper methods for role display
  getRoleColor(color?: string): string {
    return color || '#3B82F6';
  }

  getRoleIcon(icon?: string): string {
    return icon || 'fas fa-user';
  }

  getRoleTypeClass(roleType: string): string {
    switch (roleType.toUpperCase()) {
      case 'SYSTEM': return 'badge-danger';
      case 'CUSTOM': return 'badge-primary';
      case 'INHERITED': return 'badge-info';
      default: return 'badge-secondary';
    }
  }

  // Alias for closeDialogs to match HTML template
  closeModal(): void {
    this.closeDialogs();
    this.showRoleModal = false;
    this.selectedRole = null;
  }

  // Permission-related methods
  showPermissionModal = false;
  showUserModal = false;
  showHierarchyModal = false;
  permissionModuleFilter = '';
  permissionActionFilter = '';
  permissionStatusFilter = '';
  permissionSearchQuery = '';
  permissionModules: string[] = [];
  permissionActions: string[] = [];
  permissions: Permission[] = [];
  allPermissions: Permission[] = [];
  assignedPermissionIds: Set<string> = new Set();
  users: User[] = [];
  allUsers: User[] = [];
  hierarchy: RoleHierarchy[] = [];
  isLoadingPermissions = false;
  isLoadingUsers = false;
  userSearchQuery = '';
  userDepartmentFilter = '';
  userStatusFilter: string = '';
  selectedUserIds: Set<string> = new Set();
  availableDepartments: string[] = [];
  totalAvailableUsers: number = 0;

  // Permission Pagination
  permissionCurrentPage = 1;
  permissionPageSize = 10;
  permissionTotalCount = 0;
  permissionTotalPages = 0;

  // User Pagination
  userCurrentPage = 1;
  userPageSize = 10;
  userTotalCount = 0;
  userTotalPages = 0;

  openPermissionModal(role: Role): void {
    this.selectedItem = role;
    this.selectedRole = role;
    this.showPermissionModal = true;
    
    // Reset pagination and filters
    this.permissionCurrentPage = 1;
    this.permissionPageSize = 10;
    this.permissionSearchQuery = '';
    this.permissionModuleFilter = '';
    this.permissionActionFilter = '';
    this.permissionStatusFilter = '';
    
    // Load dropdown options from dedicated endpoints
    this.loadPermissionDropdownOptions();
    this.loadRolePermissions(role.id);
    this.disableBodyScroll();
  }

  closePermissionModal(): void {
    this.showPermissionModal = false;
    this.selectedRole = null;
    this.enableBodyScroll();
    this.loadData();
    this.loadStatistics();
  }

  openUserModal(role: Role): void {
    this.selectedItem = role;
    this.selectedRole = role;
    this.showUserModal = true;
    this.loadRoleUsers(role.id); // This will also call loadAllUsers internally
    this.loadDepartments();
    this.disableBodyScroll();
  }

  closeUserModal(): void {
    this.showUserModal = false;
    this.selectedRole = null;
    this.enableBodyScroll();
    this.loadData();
    this.loadStatistics();
  }

  openHierarchyModal(): void {
    this.showHierarchyModal = true;
    this.loadHierarchy();
    this.disableBodyScroll();
  }

  closeHierarchyModal(): void {
    this.showHierarchyModal = false;
    this.enableBodyScroll();
  }

  private loadRolePermissions(roleId: string): void {
    this.isLoadingPermissions = true;
    
    // Load assigned permissions for this role (no pagination needed for assigned permissions)
    this.rolesService.getRolePermissions(roleId).subscribe({
      next: (assignedPermissions) => {
        this.permissions = assignedPermissions;
        this.assignedPermissionIds = new Set(assignedPermissions.map(p => p.id));
        
        // Now load available permissions with pagination and filtering
        this.loadAvailablePermissions();
      },
      error: (error) => {
        console.error('Error loading assigned permissions:', error);
        this.notificationService.error('Error', 'Failed to load assigned permissions');
        this.isLoadingPermissions = false;
      }
    });
  }

  private loadAvailablePermissions(): void {
    // Build filter parameters for backend
    const params: any = {
      page: this.permissionCurrentPage,
      pageSize: this.permissionPageSize,
      sortField: 'name',
      sortDirection: 'asc' as 'asc' | 'desc'
    };

    // Add search filter
    if (this.permissionSearchQuery?.trim()) {
      params.search = this.permissionSearchQuery.trim();
    }

    // Add module filter
    if (this.permissionModuleFilter) {
      params.module = this.permissionModuleFilter;
    }

    // Add action filter
    if (this.permissionActionFilter) {
      params.action = this.permissionActionFilter;
    }

    // Add status filter - convert assigned/unassigned to isActive
    if (this.permissionStatusFilter && this.permissionStatusFilter !== '') {
      if (this.permissionStatusFilter === 'assigned') {
        // For assigned status, we need to get all permissions and filter client-side
        // because backend doesn't know which permissions are assigned to this role
        params.isActive = true; // Show only active permissions even for assigned/unassigned
      } else if (this.permissionStatusFilter === 'unassigned') {
        // For unassigned, load ALL permissions (large pageSize) to filter client-side
        // because we need to check against assignedPermissionIds which may not be in current page
        params.isActive = true;
        params.page = 1;
        params.pageSize = 1000; // Load large number to get all permissions for filtering
      } else {
        // For active/inactive status
        params.isActive = this.permissionStatusFilter === 'true';
      }
    } else {
      // Default: show only active permissions
      params.isActive = true;
    }

    this.rolesService.getPermissions(params).subscribe({
      next: (response) => {
        this.allPermissions = response.items;
        this.permissionTotalCount = response.totalCount;
        
        // For unassigned filter, pagination doesn't make sense since we're showing all
        if (this.permissionStatusFilter === 'unassigned') {
          this.permissionTotalPages = 1; // Single page when showing all for unassigned filter
        } else {
          this.permissionTotalPages = Math.ceil(response.totalCount / this.permissionPageSize);
        }
        
        this.isLoadingPermissions = false;
      },
      error: (error) => {
        console.error('Error loading available permissions:', error);
        this.notificationService.error('Error', 'Failed to load available permissions');
        this.isLoadingPermissions = false;
      }
    });
  }

  private loadPermissionDropdownOptions(): void {
    // Load modules and actions from dedicated endpoints
    forkJoin({
      modules: this.rolesService.getPermissionModules(),
      actions: this.rolesService.getPermissionActions()
    }).subscribe({
      next: (response) => {
        this.permissionModules = response.modules;
        this.permissionActions = response.actions;
      },
      error: (error) => {
        console.error('Error loading permission dropdown options:', error);
        this.permissionModules = [];
        this.permissionActions = [];
      }
    });
  }

  private loadRoleUsers(roleId: string): void {
    this.isLoadingUsers = true;
    
    // Load assigned users for this role
    this.rolesService.getRoleUsers(roleId).subscribe({
      next: (assignedUsers) => {
        this.users = assignedUsers;
        
        // Load all available users for assignment
        this.loadAllUsers();
      },
      error: (error) => {
        console.error('Error loading assigned users:', error);
        this.notificationService.error('Error', 'Failed to load assigned users');
        this.isLoadingUsers = false;
      }
    });
  }

  private loadAllUsers(): void {
    // Build filter parameters for backend
    const params: any = {
      page: this.userCurrentPage,
      pageSize: this.userPageSize,
      sortField: 'fullName',
      sortDirection: 'asc' as 'asc' | 'desc'
    };

    // Add search filter
    if (this.userSearchQuery?.trim()) {
      params.search = this.userSearchQuery.trim();
    }

    // Add department filter
    if (this.userDepartmentFilter) {
      params.department = this.userDepartmentFilter;
    }

    // Add status filter
    if (this.userStatusFilter) {
      params.isActive = this.userStatusFilter === 'true';
    }

    this.usersService.getData(params).subscribe({
      next: (response) => {
        this.allUsers = response.items;
        this.userTotalCount = response.totalCount;
        this.userTotalPages = Math.ceil(response.totalCount / this.userPageSize);
        // Available users are total (with current filters) minus assigned to this role
        this.totalAvailableUsers = Math.max(0, (this.userTotalCount || 0) - (this.users?.length || 0));
        this.isLoadingUsers = false;
      },
      error: (error) => {
        console.error('Error loading available users:', error);
        this.notificationService.error('Error', 'Failed to load users');
        this.isLoadingUsers = false;
      }
    });
  }

  private loadDepartments(): void {
    this.usersService.getDepartmentOptions().subscribe({
      next: (departments) => {
        this.availableDepartments = departments;
      },
      error: (error) => {
        console.error('Error loading departments:', error);
        // Fallback to hardcoded departments if API fails
        this.availableDepartments = ['IT', 'HR', 'Finance', 'Operations', 'Sales', 'Marketing', 'Quality', 'Logistics'];
      }
    });
  }

  private loadHierarchy(): void {
    this.rolesService.getHierarchy().subscribe({
      next: (hierarchy) => {
        this.hierarchy = hierarchy;
      },
      error: (error) => {
        console.error('Error loading hierarchy:', error);
        this.notificationService.error('Error', 'Failed to load hierarchy');
      }
    });
  }

  // Hierarchy Statistics Methods
  getTotalRoles(): number {
    const countRoles = (roles: RoleHierarchy[]): number => {
      let count = roles.length;
      roles.forEach(role => {
        if (role.children && role.children.length > 0) {
          count += countRoles(role.children);
        }
      });
      return count;
    };
    return countRoles(this.hierarchy);
  }

  getTotalChildRoles(): number {
    const countChildren = (roles: RoleHierarchy[]): number => {
      let count = 0;
      roles.forEach(role => {
        if (role.children && role.children.length > 0) {
          count += role.children.length;
          count += countChildren(role.children);
        }
      });
      return count;
    };
    return countChildren(this.hierarchy);
  }

  getTotalUsers(): number {
    const sumUsers = (roles: RoleHierarchy[]): number => {
      let total = 0;
      roles.forEach(role => {
        total += role.userCount || 0;
        if (role.children && role.children.length > 0) {
          total += sumUsers(role.children);
        }
      });
      return total;
    };
    return sumUsers(this.hierarchy);
  }

  exportHierarchy(): void {
    // Show loading notification
    this.notificationService.info('Info', 'Exporting hierarchy... Please wait');
    
    // Get the element to export
    const hierarchyContent = document.getElementById('hierarchy-export-content') as HTMLElement;
    
    if (!hierarchyContent) {
      this.notificationService.error('Error', 'Failed to export hierarchy');
      return;
    }
    
    // Get modal body and actions to modify styles temporarily
    const modalBodyElement = hierarchyContent.closest('.modal-body');
    const modalBody = modalBodyElement as HTMLElement;
    const modalActionsElement = hierarchyContent.closest('.modal')?.querySelector('.modal-actions');
    const modalActions = modalActionsElement as HTMLElement;
    
    if (!modalBody) {
      this.notificationService.error('Error', 'Failed to find modal body');
      return;
    }
    
    // Store original styles
    const originalModalBodyOverflow = modalBody.style.overflow;
    const originalModalBodyHeight = modalBody.style.height;
    const originalModalBodyMaxHeight = modalBody.style.maxHeight;
    const originalModalActionsOpacity = modalActions ? window.getComputedStyle(modalActions).opacity : '1';
    
    // Temporarily hide actions smoothly
    if (modalActions) {
      modalActions.style.transition = 'opacity 0.2s ease';
      modalActions.style.opacity = '0';
    }
    
    // Wait for fade out, then modify body for capture
    setTimeout(() => {
      // Modify styles for full content capture
      modalBody.style.overflow = 'visible';
      modalBody.style.height = 'auto';
      modalBody.style.maxHeight = 'none';
      if (modalActions) {
        modalActions.style.display = 'none';
      }
      
      // Small delay to ensure DOM updates are complete
      setTimeout(() => {
        // Use html2canvas to capture the content
        html2canvas(hierarchyContent, {
          scale: 2,
          useCORS: true,
          allowTaint: true,
          backgroundColor: '#ffffff',
          scrollX: 0,
          scrollY: 0,
          windowWidth: hierarchyContent.scrollWidth,
          windowHeight: hierarchyContent.scrollHeight
        }).then(canvas => {
          // Restore original styles
          modalBody.style.overflow = originalModalBodyOverflow;
          modalBody.style.height = originalModalBodyHeight;
          modalBody.style.maxHeight = originalModalBodyMaxHeight;
          
          if (modalActions) {
            modalActions.style.display = '';
            // Restore with fade in
            setTimeout(() => {
              modalActions.style.transition = 'opacity 0.3s ease';
              modalActions.style.opacity = originalModalActionsOpacity;
            }, 50);
          }
          
          // Create PDF
          const imgData = canvas.toDataURL('image/png');
          const pdf = new jsPDF('p', 'mm', 'a4');
          
          // Calculate dimensions
          const imgWidth = 210; // A4 width in mm
          const pageHeight = 295; // A4 height in mm
          const imgHeight = (canvas.height * imgWidth) / canvas.width;
          let heightLeft = imgHeight;
          
          let position = 0;
          
          // Add image to PDF
          pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
          heightLeft -= pageHeight;
          
          // Add new page if content is longer than one page
          while (heightLeft >= 0) {
            position = heightLeft - imgHeight;
            pdf.addPage();
            pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
            heightLeft -= pageHeight;
          }
          
          // Save the PDF
          const currentDate = new Date().toISOString().split('T')[0];
          pdf.save(`role-hierarchy-${currentDate}.pdf`);
          
          this.notificationService.success('Success', 'PDF exported successfully');
        }).catch((error: any) => {
          // Restore original styles in case of error
          modalBody.style.overflow = originalModalBodyOverflow;
          modalBody.style.height = originalModalBodyHeight;
          modalBody.style.maxHeight = originalModalBodyMaxHeight;
          
          if (modalActions) {
            modalActions.style.display = '';
            setTimeout(() => {
              modalActions.style.transition = 'opacity 0.3s ease';
              modalActions.style.opacity = originalModalActionsOpacity;
            }, 50);
          }
          
          console.error('Error exporting hierarchy:', error);
          this.notificationService.error('Error', 'Failed to export hierarchy');
        });
      }, 100);
    }, 200);
  }

  getFilteredPermissions(): Permission[] {
    let sourcePermissions: Permission[] = [];
    
    // If filtering by "assigned", use the already loaded assigned permissions
    if (this.permissionStatusFilter === 'assigned') {
      if (!this.permissions || !Array.isArray(this.permissions)) {
        return [];
      }
      sourcePermissions = this.permissions;
    } else {
      // For "all" or "unassigned", use the paginated available permissions
      if (!this.allPermissions || !Array.isArray(this.allPermissions)) {
        return [];
      }
      sourcePermissions = this.allPermissions;
    }
    
    // Apply client-side filtering
    return sourcePermissions.filter(permission => {
      // Apply search filter
      if (this.permissionSearchQuery?.trim()) {
        const searchLower = this.permissionSearchQuery.trim().toLowerCase();
        const matchesSearch = 
          permission.name?.toLowerCase().includes(searchLower) ||
          permission.code?.toLowerCase().includes(searchLower) ||
          permission.description?.toLowerCase().includes(searchLower) ||
          permission.module?.toLowerCase().includes(searchLower) ||
          permission.action?.toLowerCase().includes(searchLower);
        
        if (!matchesSearch) {
          return false;
        }
      }
      
      // Apply module filter
      if (this.permissionModuleFilter && permission.module !== this.permissionModuleFilter) {
        return false;
      }
      
      // Apply action filter
      if (this.permissionActionFilter && permission.action !== this.permissionActionFilter) {
        return false;
      }
      
      // Apply status filter (for unassigned)
      if (this.permissionStatusFilter === 'unassigned') {
        return !this.isPermissionAssigned(permission.id);
      }
      
      // For "assigned" or "all", we've already handled the source, so return true
      return true;
    });
  }

  getUnassignedFilteredPermissions(): Permission[] {
    return this.getFilteredPermissions().filter(p => !this.isPermissionAssigned(p.id));
  }

  getAssignedFilteredPermissions(): Permission[] {
    return this.getFilteredPermissions().filter(p => this.isPermissionAssigned(p.id));
  }

  clearPermissionFilters(): void {
    this.permissionModuleFilter = '';
    this.permissionActionFilter = '';
    this.permissionStatusFilter = '';
    this.permissionSearchQuery = '';
    this.permissionCurrentPage = 1; // Reset to first page
    this.loadAvailablePermissions();
  }

  onPermissionSearch(): void {
    // Reset to first page when searching
    this.permissionCurrentPage = 1;
    this.loadAvailablePermissions();
  }

  onPermissionFilterChange(): void {
    // Reset to first page when filtering
    this.permissionCurrentPage = 1;
    
    // Always reload from backend when any filter changes (including "All" selections)
    // This ensures we get fresh data when filters are cleared
    this.loadAvailablePermissions();
  }


  isPermissionAssigned(permissionId: string): boolean {
    return this.assignedPermissionIds.has(permissionId);
  }

  togglePermission(permissionId: string): void {
    if (!this.selectedItem) return;
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
      return;
    }

    const isAssigned = this.isPermissionAssigned(permissionId);
    const action$ = isAssigned
      ? this.rolesService.removePermission(this.selectedItem.id, permissionId)
      : this.rolesService.assignPermission(this.selectedItem.id, permissionId);

    action$.subscribe({
      next: () => {
        // Update local state immediately for better UX
        if (isAssigned) {
          this.assignedPermissionIds.delete(permissionId);
          this.permissions = this.permissions.filter(p => p.id !== permissionId);
        } else {
          this.assignedPermissionIds.add(permissionId);
          const permission = this.allPermissions.find(p => p.id === permissionId);
          if (permission) {
            this.permissions.push(permission);
          }
        }
        
        this.notificationService.success('Success', `Permission ${isAssigned ? 'removed' : 'assigned'} successfully`);
      },
      error: (error) => {
        console.error('Error toggling permission:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
        } else {
          this.notificationService.error('Error', `Failed to ${isAssigned ? 'remove' : 'assign'} permission`);
        }
      }
    });
  }

  removeUserFromRole(userId: string, roleId: string): void {
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
      return;
    }
    this.rolesService.removeRoleFromUser(userId, roleId).subscribe({
      next: () => {
        this.loadRoleUsers(roleId);
        this.notificationService.success('Success', 'User removed from role successfully');
      },
      error: (error) => {
        console.error('Error removing user from role:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
        } else {
          this.notificationService.error('Error', 'Failed to remove user from role');
        }
      }
    });
  }

  // User assignment methods
  assignUserToRole(userId: string, roleId: string): void {
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
      return;
    }
    this.rolesService.assignRoleToUser(userId, roleId).subscribe({
      next: () => {
        this.loadRoleUsers(roleId);
        this.notificationService.success('Success', 'User assigned to role successfully');
      },
      error: (error: any) => {
        console.error('Error assigning user to role:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update roles.');
        } else {
          this.notificationService.error('Error', 'Failed to assign user to role');
        }
      }
    });
  }

  isUserAssignedToRole(userId: string): boolean {
    return this.users.some(user => user.id === userId);
  }

  getFilteredUsers(): User[] {
    // Since we're now doing backend filtering, just return the current page of users
    // The filtering is handled by loadAllUsers() method
    return this.allUsers || [];
  }

  getUnassignedUsers(): User[] {
    const assignedUserIds = new Set(this.users.map(u => u.id));
    return this.getFilteredUsers().filter(user => !assignedUserIds.has(user.id));
  }

  clearUserFilters(): void {
    this.userSearchQuery = '';
    this.userDepartmentFilter = '';
    this.userStatusFilter = '';
    this.userCurrentPage = 1; // Reset to first page
    this.loadAllUsers();
  }

  refreshAssignedUsers(): void {
    if (this.selectedRole) {
      this.loadRoleUsers(this.selectedRole.id);
    }
  }

  refreshAvailableUsers(): void {
    this.loadAllUsers();
  }

  // Permission Pagination Methods
  onPermissionPageChange(page: number): void {
    this.permissionCurrentPage = page;
    this.loadAvailablePermissions();
  }

  onPermissionPageSizeChange(pageSize: number): void {
    this.permissionPageSize = pageSize;
    this.permissionCurrentPage = 1; // Reset to first page
    this.loadAvailablePermissions();
  }

  getPermissionTotalPages(): number {
    return this.permissionTotalPages;
  }

  // Simple User Avatar Methods
  getUserAvatarUrl(user: User): string | null {
    if (!user.avatarUrl) return null;
    
    // Return full URLs as is
    if (user.avatarUrl.startsWith('http')) return user.avatarUrl;
    
    // Add API base URL for relative paths
    const path = user.avatarUrl.startsWith('/') ? user.avatarUrl : `/media/${user.avatarUrl}`;
    return `${environment.apiBaseUrl}${path}`;
  }

  getUserInitials(user: User): string {
    if (!user.fullName) return 'U';
    
    const names = user.fullName.trim().split(' ');
    if (names.length === 1) {
      return names[0].charAt(0).toUpperCase();
    }
    
    return (names[0].charAt(0) + names[names.length - 1].charAt(0)).toUpperCase();
  }

  onAvatarError(event: any, user: User): void {
    // Hide the broken image and show initials
    event.target.style.display = 'none';
    const avatarContainer = event.target.parentElement;
    if (avatarContainer) {
      avatarContainer.innerHTML = `<span class="avatar-initials">${this.getUserInitials(user)}</span>`;
    }
  }

  onUserPageChange(page: number): void {
    this.userCurrentPage = page;
    this.loadAllUsers();
  }

  onUserPageSizeChange(pageSize: number): void {
    this.userPageSize = pageSize;
    this.userCurrentPage = 1; // Reset to first page
    this.loadAllUsers();
  }

  getUserTotalPages(): number {
    return this.userTotalPages;
  }


  // Bulk user operations
  assignSelectedUsers(): void {
    if (!this.selectedItem || this.selectedUserIds.size === 0) return;

    // Only assign unassigned users
    const assignedUserIds = new Set(this.users.map(u => u.id));
    const unassignedUserIds = Array.from(this.selectedUserIds).filter(userId => !assignedUserIds.has(userId));
    
    if (unassignedUserIds.length === 0) {
      this.notificationService.warning('Warning', 'No unassigned users selected');
      return;
    }

    let completed = 0;
    let errors = 0;

    unassignedUserIds.forEach(userId => {
      this.rolesService.assignRoleToUser(userId, this.selectedItem!.id).subscribe({
        next: () => {
          completed++;
          if (completed + errors === unassignedUserIds.length) {
            if (errors === 0) {
              this.notificationService.success('Success', `All ${completed} users assigned successfully`);
            } else {
              this.notificationService.warning('Partial Success', `${completed} users assigned, ${errors} failed`);
            }
            this.selectedUserIds.clear();
            this.loadRoleUsers(this.selectedItem!.id);
          }
        },
        error: (error: any) => {
          errors++;
          console.error('Error assigning user:', error);
          if (completed + errors === unassignedUserIds.length) {
            if (errors === unassignedUserIds.length) {
              this.notificationService.error('Error', 'Failed to assign users');
            } else {
              this.notificationService.warning('Partial Success', `${completed} users assigned, ${errors} failed`);
            }
            this.selectedUserIds.clear();
            this.loadRoleUsers(this.selectedItem!.id);
          }
        }
      });
    });
  }

  removeSelectedUsers(): void {
    if (!this.selectedItem || this.selectedUserIds.size === 0) return;

    // Only remove assigned users
    const assignedUserIds = new Set(this.users.map(u => u.id));
    const assignedSelectedUserIds = Array.from(this.selectedUserIds).filter(userId => assignedUserIds.has(userId));
    
    if (assignedSelectedUserIds.length === 0) {
      this.notificationService.warning('Warning', 'No assigned users selected');
      return;
    }

    let completed = 0;
    let errors = 0;

    assignedSelectedUserIds.forEach(userId => {
      this.rolesService.removeRoleFromUser(userId, this.selectedItem!.id).subscribe({
        next: () => {
          completed++;
          if (completed + errors === assignedSelectedUserIds.length) {
            if (errors === 0) {
              this.notificationService.success('Success', `All ${completed} users removed successfully`);
            } else {
              this.notificationService.warning('Partial Success', `${completed} users removed, ${errors} failed`);
            }
            this.selectedUserIds.clear();
            this.loadRoleUsers(this.selectedItem!.id);
          }
        },
        error: (error) => {
          errors++;
          console.error('Error removing user:', error);
          if (completed + errors === assignedSelectedUserIds.length) {
            if (errors === assignedSelectedUserIds.length) {
              this.notificationService.error('Error', 'Failed to remove users');
            } else {
              this.notificationService.warning('Partial Success', `${completed} users removed, ${errors} failed`);
            }
            this.selectedUserIds.clear();
            this.loadRoleUsers(this.selectedItem!.id);
          }
        }
      });
    });
  }

  toggleUserSelection(userId: string): void {
    if (this.selectedUserIds.has(userId)) {
      this.selectedUserIds.delete(userId);
    } else {
      this.selectedUserIds.add(userId);
    }
  }

  selectAllUnassignedUsers(): void {
    const unassignedUsers = this.getUnassignedUsers();
    unassignedUsers.forEach(user => this.selectedUserIds.add(user.id));
  }

  clearUserSelection(): void {
    this.selectedUserIds.clear();
  }

  // Helper methods for button state logic
  hasUnassignedUsersSelected(): boolean {
    if (this.selectedUserIds.size === 0) return false;
    const assignedUserIds = new Set(this.users.map(u => u.id));
    return Array.from(this.selectedUserIds).some(userId => !assignedUserIds.has(userId));
  }

  hasAssignedUsersSelected(): boolean {
    if (this.selectedUserIds.size === 0) return false;
    const assignedUserIds = new Set(this.users.map(u => u.id));
    return Array.from(this.selectedUserIds).some(userId => assignedUserIds.has(userId));
  }

  hasOnlyUnassignedUsersSelected(): boolean {
    if (this.selectedUserIds.size === 0) return false;
    const assignedUserIds = new Set(this.users.map(u => u.id));
    return Array.from(this.selectedUserIds).every(userId => !assignedUserIds.has(userId));
  }

  hasOnlyAssignedUsersSelected(): boolean {
    if (this.selectedUserIds.size === 0) return false;
    const assignedUserIds = new Set(this.users.map(u => u.id));
    return Array.from(this.selectedUserIds).every(userId => assignedUserIds.has(userId));
  }

  onUserSearch(): void {
    // Reset to first page when searching
    this.userCurrentPage = 1;
    this.loadAllUsers();
  }

  onUserFilterChange(): void {
    // Reset to first page when filtering
    this.userCurrentPage = 1;
    this.loadAllUsers();
  }

  // Bulk permission operations
  assignAllPermissions(): void {
    if (!this.selectedItem || !this.allPermissions || this.allPermissions.length === 0) return;

    const filteredPermissions = this.getFilteredPermissions();
    const unassignedPermissions = filteredPermissions.filter(p => !this.isPermissionAssigned(p.id));
    
    if (unassignedPermissions.length === 0) {
      this.notificationService.info('Info', 'All filtered permissions are already assigned');
      return;
    }

    const permissionIds = unassignedPermissions.map(p => p.id);
    
    // Use bulk assign endpoint if available, otherwise assign one by one
    this.assignPermissionsBulk(permissionIds);
  }

  removeAllPermissions(): void {
    if (!this.selectedItem || !this.allPermissions || this.allPermissions.length === 0) return;

    const filteredPermissions = this.getFilteredPermissions();
    const assignedPermissions = filteredPermissions.filter(p => this.isPermissionAssigned(p.id));
    
    if (assignedPermissions.length === 0) {
      this.notificationService.info('Info', 'No assigned permissions found in current filter');
      return;
    }

    const permissionIds = assignedPermissions.map(p => p.id);
    
    // Use bulk remove endpoint if available, otherwise remove one by one
    this.removePermissionsBulk(permissionIds);
  }

  private assignPermissionsBulk(permissionIds: string[]): void {
    if (!this.selectedItem) return;

    // For now, assign one by one. In the future, we can implement bulk API endpoint
    let completed = 0;
    let errors = 0;

    permissionIds.forEach(permissionId => {
      this.rolesService.assignPermission(this.selectedItem!.id, permissionId).subscribe({
        next: () => {
          completed++;
          this.assignedPermissionIds.add(permissionId);
          const permission = this.allPermissions.find(p => p.id === permissionId);
          if (permission) {
            this.permissions.push(permission);
          }
          
          if (completed + errors === permissionIds.length) {
            if (errors === 0) {
              this.notificationService.success('Success', `All ${completed} permissions assigned successfully`);
            } else {
              this.notificationService.warning('Partial Success', `${completed} permissions assigned, ${errors} failed`);
            }
          }
        },
        error: (error) => {
          errors++;
          console.error('Error assigning permission:', error);
          
          if (completed + errors === permissionIds.length) {
            if (errors === permissionIds.length) {
              this.notificationService.error('Error', 'Failed to assign permissions');
            } else {
              this.notificationService.warning('Partial Success', `${completed} permissions assigned, ${errors} failed`);
            }
          }
        }
      });
    });
  }

  private removePermissionsBulk(permissionIds: string[]): void {
    if (!this.selectedItem) return;

    // For now, remove one by one. In the future, we can implement bulk API endpoint
    let completed = 0;
    let errors = 0;

    permissionIds.forEach(permissionId => {
      this.rolesService.removePermission(this.selectedItem!.id, permissionId).subscribe({
        next: () => {
          completed++;
          this.assignedPermissionIds.delete(permissionId);
          this.permissions = this.permissions.filter(p => p.id !== permissionId);
          
          if (completed + errors === permissionIds.length) {
            if (errors === 0) {
              this.notificationService.success('Success', `All ${completed} permissions removed successfully`);
            } else {
              this.notificationService.warning('Partial Success', `${completed} permissions removed, ${errors} failed`);
            }
          }
        },
        error: (error) => {
          errors++;
          console.error('Error removing permission:', error);
          
          if (completed + errors === permissionIds.length) {
            if (errors === permissionIds.length) {
              this.notificationService.error('Error', 'Failed to remove permissions');
            } else {
              this.notificationService.warning('Partial Success', `${completed} permissions removed, ${errors} failed`);
            }
          }
        }
      });
    });
  }

  // Additional helper methods
  getTotalPages(): number {
    return this.totalPages;
  }

  getActiveFilterCount(): number {
    return this.activeFilterCount;
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  onSort(field: string): void {
    this.sortData(field);
  }

  openRoleModal(role?: Role): void {
    if (role) {
      this.selectedRole = role;
      this.openEditDialog(role);
    } else {
      this.selectedRole = null;
      this.openAddDialog();
    }
    this.showRoleModal = true;
  }

  loadRoles(): void {
    this.loadData();
  }

  deleteRole(role: Role): void {
    this.openDeleteDialog([role]);
  }

  bulkDeleteRoles(): void {
    const selectedItems = this.getSelectedItems();
    if (selectedItems.length > 0) {
      this.openDeleteDialog(selectedItems);
    }
  }

  toggleRoleStatus(role: Role): void {
    this.toggleActiveStatus(role);
  }

  downloadImportTemplate(): void {
    this.downloadTemplate();
  }

  exportRoles(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
    this.startAsyncExport(format);
  }

  // Properties for template compatibility
  selectedRole: Role | null = null;
  roleForm!: FormGroup;
  showRoleModal = false;
  selectedRoles: string[] = [];
  searchTerm = '';
  roleColors: string[] = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#EC4899'];
  roleIcons: string[] = ['fas fa-user', 'fas fa-shield-alt', 'fas fa-crown', 'fas fa-star', 'fas fa-key'];
  loading = false;
  saving = false;
  totalCount = 0;
  pageSize = 10;

  // Filter properties
  roleTypeFilter = '';
  isActiveFilter: boolean | null = null;
  createdFromFilter = '';
  createdToFilter = '';

  // Alias for items
  get roles(): Role[] {
    return this.items;
  }

  // Utility Methods
  formatDate(date: Date | string): string {
    return this.commonUtility.formatDate(date);
  }

  formatDateTime(date: Date | string): string {
    return this.commonUtility.formatDateTime(date);
  }
}
