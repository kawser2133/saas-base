import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsersService, User, CreateUserRequest, UpdateUserRequest, UserStatistics, RoleOption, ImportJobStatus, ImportExportHistory, ImportExportHistoryResponse, ExportJobStatus } from '../../../core/services/users.service';
import { RolesService } from '../../../core/services/roles.service';
import { CommonUtilityService } from '../../../shared/services/common-utility.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { AuthorizationService } from '../../../core/services/authorization.service';
import { UserContextService } from '../../../core/services/user-context.service';
import { HttpClient } from '@angular/common/http';
import { Subscription, timer, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule, NotificationContainerComponent, BreadcrumbComponent, HasPermissionDirective],
  templateUrl: './users.component.html',
  styleUrls: ['./users.component.scss']
})
export class UsersComponent implements OnInit, OnDestroy {
  // Utility references
  Math = Math;
  document = document;

  // Subscriptions
  private subscriptions = new Subscription();

  // Data properties
  items: User[] = [];
  filteredItems: User[] = [];
  paginatedData: User[] = [];
  selectedItems = new Set<string>();

  // Dialog states
  showAddEditDialog = false;
  showViewDialog = false;
  showDeleteDialog = false;
  showImportDialog = false;
  showImportHistoryDialog = false;

  // Form data
  formData: CreateUserRequest & UpdateUserRequest & { id?: string; roleId: string; roleIds: string[] } = {
    email: '',
    fullName: '',
    firstName: '',
    lastName: '',
    phoneNumber: '',
    isActive: true,
    department: '',
    jobTitle: '',
    location: '',
    employeeId: '',
    roleId: '',
    roleIds: []
  };
  dialogMode: 'add' | 'edit' = 'add';
  selectedItem: User | null = null;

  // Filters
  filters: any = { createdFrom: '', createdTo: '', roleId: '', organizationId: '' };
  filterFormData: any = { createdFrom: '', createdTo: '', roleId: '', organizationId: '' };
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
  locations: string[] = [];
  departments: string[] = [];
  positions: string[] = [];
  roles: RoleOption[] = [];
  
  // Role cache for icon and color mapping
  roleCache: Map<string, { id: string; name: string; icon?: string; color?: string }> = new Map();

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
  statistics: UserStatistics = {
    total: 0,
    active: 0,
    inactive: 0,
    emailVerifiedUsers: 0,
    emailUnverifiedUsers: 0,
    recentlyCreatedUsers: 0
  };

  // Available options
  statusOptions: ('active' | 'inactive')[] = ['active', 'inactive'];
  verificationOptions: ('verified' | 'unverified')[] = ['verified', 'unverified'];

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
    private usersService: UsersService,
    private rolesService: RolesService,
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
    
    // Load critical data first (permissions and main data)
    this.loadPermissions();
    this.loadRoleCache(); // Load roles for icon/color mapping
    
    // Load organizations if System Admin
    if (this.isSystemAdmin) {
      this.loadOrganizations();
    }
    
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
          this.canCreate = this.authorizationService.hasPermission('Users.Create');
          this.canUpdate = this.authorizationService.hasPermission('Users.Update');
          this.canDelete = this.authorizationService.hasPermission('Users.Delete');
          this.canExport = this.authorizationService.hasPermission('Users.Export');
          this.canImport = this.authorizationService.hasPermission('Users.Import');
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
    const fields = ['department', 'isActive', 'jobTitle', 'isEmailVerified', 'roleId', 'filter-department', 'filter-jobTitle', 'filter-roleId', 'filter-status', 'filter-verification', 'items-per-page'];
    if (this.isSystemAdmin) {
      fields.push('filter-organizationId');
    }
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
      case 'department': return this.departments;
      case 'jobTitle': return this.positions;
      case 'location': return this.locations;
      case 'roleId': return this.roles.map(r => r.id);
      case 'isActive': return ['true', 'false'];
      case 'isEmailVerified': return ['true', 'false'];
      case 'filter-department': return this.departments;
      case 'filter-jobTitle': return this.positions;
      case 'filter-location': return this.locations;
      case 'filter-roleId': return this.roles.map(r => r.id);
      case 'filter-status': return ['', 'true', 'false'];
      case 'filter-verification': return ['', 'true', 'false'];
      case 'items-per-page': return ['10', '25', '50', '100'];
      default: return [];
    }
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

  // Data loading
  loadData(): void {
    this.isLoading = true;

    const params: any = {
      page: this.currentPage,
      pageSize: this.itemsPerPage,
      search: this.searchQuery,
      sortField: this.sortField,
      sortDirection: this.sortDirection,
      department: this.filters.department,
      jobTitle: this.filters.jobTitle,
      location: this.filters.location,
      isActive: this.filters.isActive,
      isEmailVerified: this.filters.isEmailVerified,
      roleId: this.filters.roleId,
      createdFrom: this.filters.createdFrom || undefined,
      createdTo: this.filters.createdTo || undefined
    };

    // Add organizationId filter for System Admin
    if (this.isSystemAdmin && this.filters.organizationId) {
      params.organizationId = this.filters.organizationId;
    }

    const sub = this.usersService.getData(params).subscribe({
      next: (response: any) => {
        // Roles are now included in the response from backend - no need for separate API calls
        // Handle both PascalCase (from backend) and camelCase property names
        this.items = response.items?.map((item: any) => {
          // Backend sends RoleIds/RoleNames (PascalCase), check both formats
          let roleIds: string[] = [];
          let roleNames: string[] = [];
          
          // Check PascalCase first (default C# JSON serialization)
          if (item.RoleIds && Array.isArray(item.RoleIds) && item.RoleIds.length > 0) {
            roleIds = item.RoleIds.map((id: any) => id?.toString() || '');
          } else if (item.roleIds && Array.isArray(item.roleIds) && item.roleIds.length > 0) {
            roleIds = item.roleIds.map((id: any) => id?.toString() || '');
          } else if (item.roleId || item.RoleId) {
            roleIds = [item.roleId || item.RoleId].map((id: any) => id?.toString() || '');
          }
          
          if (item.RoleNames && Array.isArray(item.RoleNames) && item.RoleNames.length > 0) {
            roleNames = item.RoleNames.map((name: any) => name?.toString() || '');
          } else if (item.roleNames && Array.isArray(item.roleNames) && item.roleNames.length > 0) {
            roleNames = item.roleNames.map((name: any) => name?.toString() || '');
          } else if (item.roleName || item.RoleName) {
            roleNames = [item.roleName || item.RoleName].map((name: any) => name?.toString() || '');
          }
          
          // Map roleIds to roleDetails with icon and color
          const roleDetails = roleIds.map(roleId => {
            const roleInfo = this.roleCache.get(roleId);
            if (roleInfo) {
              return {
                id: roleInfo.id,
                name: roleInfo.name,
                icon: roleInfo.icon,
                color: roleInfo.color
              };
            }
            // Fallback to roleName if role not found in cache
            const roleName = roleNames.find((_, index) => roleIds[index] === roleId) || '';
            return {
              id: roleId,
              name: roleName,
              icon: undefined,
              color: undefined
            };
          });
          
          return {
            ...item,
            organizationId: item.organizationId || item.OrganizationId,
            organizationName: item.organizationName || item.OrganizationName,
            roleIds: roleIds,
            roleNames: roleNames,
            roleDetails: roleDetails,
            // Map metadata fields (handle both PascalCase and camelCase)
            createdAtUtc: item.CreatedAtUtc || item.createdAtUtc,
            createdBy: item.CreatedBy || item.createdBy,
            modifiedAtUtc: item.ModifiedAtUtc || item.modifiedAtUtc,
            modifiedBy: item.ModifiedBy || item.modifiedBy,
            showDropdown: false,
            dropdownUp: false
          } as User;
        }) || [];
        
        this.totalItems = response.totalCount || 0;
        this.totalPages = response.totalPages || Math.ceil(this.totalItems / this.itemsPerPage);
        this.filteredItems = [...this.items];
        this.paginatedData = [...this.items];
        
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading users:', error);
        this.notificationService.error('Error', 'Failed to load users. Please try again.');
        this.isLoading = false;
      }
    });
    this.subscriptions.add(sub);
  }


  loadStatistics(): void {
    const sub = this.usersService.getStatistics().subscribe({
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
    const sub = this.usersService.getDropdownOptions().subscribe({
      next: (options) => {
        this.locations = options.locations;
        this.departments = options.departments;
        this.positions = options.positions;
        this.roles = options.roles;
      },
      error: (error: any) => {
        console.error('Error loading dropdown options:', error);
        this.notificationService.error('Error', 'Failed to load dropdown options');
      }
    });
    this.subscriptions.add(sub);
  }

  loadRoleCache(): void {
    // Load all roles to cache icon and color for mapping
    const sub = this.rolesService.getData({ page: 1, pageSize: 1000 }).subscribe({
      next: (response: any) => {
        if (response.items) {
          response.items.forEach((role: any) => {
            this.roleCache.set(role.id, {
              id: role.id,
              name: role.name,
              icon: role.icon,
              color: role.color
            });
          });
        }
      },
      error: (error: any) => {
        console.error('Error loading role cache:', error);
        // Don't show error notification as this is non-critical
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
    this.filters = { createdFrom: '', createdTo: '', roleId: '', organizationId: '' };
    this.filterFormData = { createdFrom: '', createdTo: '', roleId: '', organizationId: '' };
    this.applyFilters();
  }

  refreshData(): void {
    
    this.loadData();
    this.loadStatistics();
  }

  get activeFilterCount(): number {
    let count = 0;
    if (this.searchQuery) count++;
    if (this.filters.department) count++;
    if (this.filters.jobTitle) count++;
    if (this.filters.location) count++;
    if (this.filters.roleId) count++;
    if (this.filters.isActive !== undefined) count++;
    if (this.filters.isEmailVerified !== undefined) count++;
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
      this.notificationService.error('Access Denied', 'You do not have permission to create users.');
      return;
    }
    this.dialogMode = 'add';
    this.formData = {
      email: '',
      fullName: '',
      firstName: '',
      lastName: '',
      phoneNumber: '',
      isActive: true,
      department: '',
      jobTitle: '',
      location: '',
      employeeId: '',
      roleId: '',
      roleIds: []
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  // Check if user can edit/delete this item (System Admin can only edit their own organization's data)
  canEditItem(item: User): boolean {
    if (!this.canUpdate) return false;
    if (!this.isSystemAdmin) return true; // Regular users can edit their org's data
    // System Admin can only edit their own organization's data
    const currentOrgId = this.userContextService.getCurrentOrganizationId();
    return item.organizationId === currentOrgId;
  }

  canDeleteItem(item: User): boolean {
    if (!this.canDelete) return false;
    if (!this.isSystemAdmin) return true; // Regular users can delete their org's data
    // System Admin can only delete their own organization's data
    const currentOrgId = this.userContextService.getCurrentOrganizationId();
    return item.organizationId === currentOrgId;
  }

  openEditDialog(item: User): void {
    if (!this.canEditItem(item)) {
      if (!this.canUpdate) {
        this.notificationService.error('Access Denied', 'You do not have permission to update users.');
      } else {
        this.notificationService.error('Access Denied', 'You can only edit users from your own organization.');
      }
      return;
    }
    this.dialogMode = 'edit';
    this.selectedItem = item;
    
    // Roles are now included in the response from backend
    // Ensure roleIds and roleNames are initialized
    if (!item.roleIds || item.roleIds.length === 0) {
      item.roleIds = item.roleId ? [item.roleId] : [];
      item.roleNames = item.roleName ? [item.roleName] : [];
    }
    
    this.initializeEditFormData(item);
  }

  private initializeEditFormData(item: User): void {
    this.formData = {
      id: item.id,
      email: item.email,
      fullName: item.fullName,
      firstName: item.firstName || '',
      lastName: item.lastName || '',
      phoneNumber: item.phoneNumber || '',
      isActive: item.isActive,
      department: item.department || '',
      jobTitle: item.jobTitle || '',
      location: item.location || '',
      employeeId: item.employeeId || '',
      roleId: item.roleId || (item.roleIds && item.roleIds.length > 0 ? item.roleIds[0] : ''),
      roleIds: item.roleIds || (item.roleId ? [item.roleId] : [])
    };
    this.errors = {};
    this.closeAllSearchableDropdowns();
    this.showAddEditDialog = true;
    this.disableBodyScroll();
  }

  openViewDialog(item: User): void {
    // Fetch fresh detail data to ensure metadata is up-to-date
    const sub = this.usersService.getUserById(item.id).subscribe({
      next: (userDetail: User) => {
        // Extract roleIds and roleNames from response (handle both PascalCase and camelCase)
        let roleIds: string[] = [];
        let roleNames: string[] = [];
        
        if ((userDetail as any).RoleIds && Array.isArray((userDetail as any).RoleIds) && (userDetail as any).RoleIds.length > 0) {
          roleIds = (userDetail as any).RoleIds.map((id: any) => id?.toString() || '');
        } else if (userDetail.roleIds && Array.isArray(userDetail.roleIds) && userDetail.roleIds.length > 0) {
          roleIds = userDetail.roleIds.map((id: any) => id?.toString() || '');
        } else if (userDetail.roleId || (userDetail as any).RoleId) {
          roleIds = [userDetail.roleId || (userDetail as any).RoleId].map((id: any) => id?.toString() || '');
        }
        
        if ((userDetail as any).RoleNames && Array.isArray((userDetail as any).RoleNames) && (userDetail as any).RoleNames.length > 0) {
          roleNames = (userDetail as any).RoleNames.map((name: any) => name?.toString() || '');
        } else if (userDetail.roleNames && Array.isArray(userDetail.roleNames) && userDetail.roleNames.length > 0) {
          roleNames = userDetail.roleNames.map((name: any) => name?.toString() || '');
        } else if (userDetail.roleName || (userDetail as any).RoleName) {
          roleNames = [userDetail.roleName || (userDetail as any).RoleName].map((name: any) => name?.toString() || '');
        }
        
        // Map roleIds to roleDetails with icon and color
        const roleDetails = roleIds.map(roleId => {
          const roleInfo = this.roleCache.get(roleId);
          if (roleInfo) {
            return {
              id: roleInfo.id,
              name: roleInfo.name,
              icon: roleInfo.icon,
              color: roleInfo.color
            };
          }
          // Fallback to roleName if role not found in cache
          const roleName = roleNames.find((_, index) => roleIds[index] === roleId) || '';
          return {
            id: roleId,
            name: roleName,
            icon: undefined,
            color: undefined
          };
        });
        
        this.selectedItem = {
          ...userDetail,
          roleIds: roleIds,
          roleNames: roleNames,
          roleDetails: roleDetails,
          // Map metadata fields (handle both PascalCase and camelCase)
          createdAtUtc: (userDetail as any).CreatedAtUtc || userDetail.createdAtUtc,
          createdBy: (userDetail as any).CreatedBy || userDetail.createdBy,
          modifiedAtUtc: (userDetail as any).ModifiedAtUtc || userDetail.modifiedAtUtc,
          modifiedBy: (userDetail as any).ModifiedBy || userDetail.modifiedBy
        };
        
        this.showViewDialog = true;
        this.disableBodyScroll();
      },
      error: (error) => {
        console.error('Error loading user details:', error);
        // Fallback to list item if detail fetch fails
        this.selectedItem = item;
        if (!item.roleIds || item.roleIds.length === 0) {
          item.roleIds = item.roleId ? [item.roleId] : [];
          item.roleNames = item.roleName ? [item.roleName] : [];
        }
        this.showViewDialog = true;
        this.disableBodyScroll();
      }
    });
    this.subscriptions.add(sub);
  }

  openDeleteDialog(items: User[]): void {
    // Clear current selection and add the items to be deleted
    this.selectedItems.clear();
    items.forEach(item => this.selectedItems.add(item.id));
    this.showDeleteDialog = true;
    this.disableBodyScroll();
  }

  cloneSelected(): void {
    const selectedIds = Array.from(this.selectedItems);
    if (selectedIds.length === 0) {
      this.notificationService.warning('Warning', 'Please select at least one user to clone');
      return;
    }

    this.notificationService.info('Info', 'Cloning users...');
    this.usersService.cloneMultiple(selectedIds).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || `${selectedIds.length} user(s) cloned successfully`);
        this.selectedItems.clear();
        this.loadData();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone users');
      }
    });
  }

  cloneUser(user: any): void {
    this.notificationService.info('Info', 'Cloning user...');
    this.usersService.cloneMultiple([user.id]).subscribe({
      next: (response) => {
        this.notificationService.success('Success', response.message || 'User cloned successfully');
        this.loadData();
      },
      error: (error) => {
        this.notificationService.error('Error', error.error?.message || 'Failed to clone user');
      }
    });
  }

  openImportDialog(): void {
    if (!this.canImport) {
      this.notificationService.error('Access Denied', 'You do not have permission to import users.');
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
      const createRequest: CreateUserRequest = {
        email: this.formData.email,
        fullName: `${this.formData.firstName?.trim() || ''} ${this.formData.lastName?.trim() || ''}`.trim(),
        firstName: this.formData.firstName,
        lastName: this.formData.lastName,
        phoneNumber: this.formData.phoneNumber,
        isActive: this.formData.isActive,
        department: this.formData.department,
        jobTitle: this.formData.jobTitle,
        location: this.formData.location,
        employeeId: this.formData.employeeId,
        roleId: this.formData.roleIds && this.formData.roleIds.length > 0 ? this.formData.roleIds[0] : this.formData.roleId
      };

      this.usersService.create(createRequest).subscribe({
        next: (newUser: User) => {
          // Assign additional roles if any
          const roleIdsToAssign = this.formData.roleIds || [];
          if (roleIdsToAssign.length > 0) {
            // Assign all roles (including the first one if it was only used for creation)
            const assignRequests = roleIdsToAssign.map(roleId => 
              this.rolesService.assignRoleToUser(newUser.id, roleId).pipe(
                catchError(error => {
                  console.error(`Error assigning role ${roleId}:`, error);
                  return of(null);
                })
              )
            );
            
            forkJoin(assignRequests).subscribe({
              next: () => {
                this.closeDialogs();
                this.notificationService.success('Success', 'User created successfully!');
                this.isSubmitting = false;
                this.loadData();
                this.loadStatistics();
              },
              error: () => {
                // Even if role assignment fails, user is created
                this.closeDialogs();
                this.notificationService.success('Success', 'User created successfully, but some roles may not have been assigned.');
                this.isSubmitting = false;
                this.loadData();
                this.loadStatistics();
              }
            });
          } else {
            this.closeDialogs();
            this.notificationService.success('Success', 'User created successfully!');
            this.isSubmitting = false;
            this.loadData();
            this.loadStatistics();
          }
        },
        error: (error) => {
          console.error('Error creating user:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to create user. Please try again.');
          this.isSubmitting = false;
        }
      });
    } else {
      const updateRequest: UpdateUserRequest = {
        fullName: `${this.formData.firstName?.trim() || ''} ${this.formData.lastName?.trim() || ''}`.trim(),
        firstName: this.formData.firstName,
        lastName: this.formData.lastName,
        phoneNumber: this.formData.phoneNumber,
        isActive: this.formData.isActive,
        department: this.formData.department,
        jobTitle: this.formData.jobTitle,
        location: this.formData.location,
        employeeId: this.formData.employeeId,
        roleId: this.formData.roleIds && this.formData.roleIds.length > 0 ? this.formData.roleIds[0] : this.formData.roleId,
        roleIds: this.formData.roleIds && this.formData.roleIds.length > 0 ? this.formData.roleIds : undefined
      };

      // Backend now handles multi-role update in a single call
      this.usersService.update(this.formData.id!, updateRequest).subscribe({
        next: (updatedUser: User) => {
          this.closeDialogs();
          this.notificationService.success('Success', 'User updated successfully!');
          this.isSubmitting = false;
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error updating user:', error);
          this.notificationService.error('Error', error.error?.message || 'Failed to update user. Please try again.');
          this.isSubmitting = false;
        }
      });
    }
  }

  confirmDelete(): void {
    if (!this.canDelete) {
      this.notificationService.error('Access Denied', 'You do not have permission to delete users.');
      return;
    }
    
    // Filter out items that cannot be deleted (System Admin can only delete their own org's users)
    const allItems = this.items.filter(item => this.selectedItems.has(item.id));
    const deletableItems = allItems.filter(item => this.canDeleteItem(item));
    
    if (deletableItems.length === 0) {
      this.notificationService.error('Access Denied', 'You can only delete users from your own organization.');
      this.closeDialogs();
      return;
    }
    
    if (deletableItems.length < allItems.length) {
      this.notificationService.warning('Partial Selection', `Only ${deletableItems.length} of ${allItems.length} selected users can be deleted. Users from other organizations will be skipped.`);
    }
    
    const itemsToDelete = deletableItems.map(item => item.id);

    if (itemsToDelete.length === 1) {
      this.usersService.delete(itemsToDelete[0]).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', 'User deleted successfully!');
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting user:', error);
          if (error.status === 403) {
            this.notificationService.error('Access Denied', 'You do not have permission to delete users.');
          } else {
            const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete user. Please try again.';
            this.notificationService.error('Error', errorMessage);
          }
        }
      });
    } else {
      const ids = itemsToDelete.map((id: string) => id);
      this.usersService.deleteMultiple(ids).subscribe({
        next: () => {
          this.selectedItems.clear();
          this.closeDialogs();
          this.notificationService.success('Success', `${itemsToDelete.length} users deleted successfully`);
          this.loadData();
          this.loadStatistics();
        },
        error: (error) => {
          console.error('Error deleting users:', error);
          if (error.status === 403) {
            this.notificationService.error('Access Denied', 'You do not have permission to delete users.');
          } else {
            const errorMessage = error?.error?.detail || error?.error?.message || error?.message || 'Failed to delete users. Please try again.';
            this.notificationService.error('Error', errorMessage);
          }
        }
      });
    }
  }

  // Async export with progress tracking
  startAsyncExport(format: 'excel' | 'csv' | 'pdf' | 'json'): void {
    if (!this.canExport) {
      this.notificationService.error('Access Denied', 'You do not have permission to export users.');
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
      search: this.searchQuery,
      department: this.filters.department,
      jobTitle: this.filters.jobTitle,
      location: this.filters.location,
      isActive: this.filters.isActive,
      isEmailVerified: this.filters.isEmailVerified,
      roleId: this.filters.roleId && this.filters.roleId !== 'all' && this.filters.roleId !== '' ? this.filters.roleId : undefined,
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

    this.usersService.startExportAsync(params).subscribe({
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

      this.usersService.getExportJobStatus(this.exportJobId).subscribe({
        next: (status: ExportJobStatus) => {
          this.exportStatus = status.status;
          this.exportProgress = status.progressPercent;
          this.exportFormat = status.format; // Store format for later use

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
    this.usersService.downloadExport(jobId).subscribe({
      next: (blob: Blob) => {
        // Determine file extension based on format
        const extension = this.getFileExtension(format);

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `users_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.usersService.getTemplate().subscribe({
      next: (response: Blob) => {
        const blob = new Blob([response], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'users_import_template.xlsx';
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
    this.usersService.startImportAsync(formData).subscribe({
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

      this.usersService.getImportJobStatus(this.importJobId).subscribe({
        next: (status: ImportJobStatus) => {
          console.log('Import job status:', status); // Debug log
          this.importStatus = status.status;
          this.importProgress = status.progressPercent ?? 0;

          if (status.status === 'Completed' || status.status === 'Failed') {
            clearInterval(intervalId);
            this.isImporting = false;
            this.importJobId = null;

            if (status.status === 'Completed') {
              this.notificationService.success('Import Completed', `Imported ${status.successCount} users${status.skippedCount ? `, skipped ${status.skippedCount}` : ''}${status.errorCount ? `, errors ${status.errorCount}` : ''}.`);
            } else if (status.status === 'Failed') {
              let errorMessage = status.message || 'Import failed.';
              if (status.totalRows === 0) {
                errorMessage = 'File parsing failed. Please check that your file has the correct format and required headers (First Name, Last Name, Email). Download the template for reference.';
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

    this.usersService.getImportErrorReport(this.errorReportId).subscribe({
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

    this.usersService.getImportErrorReport(history.errorReportId).subscribe({
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
    this.usersService.downloadExport(history.jobId).subscribe({
      next: (blob: Blob) => {
        // Determine file extension based on format
        const extension = this.getFileExtension(history.format);

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = history.fileName || `users_export_${new Date().toISOString().split('T')[0]}.${extension}`;
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
    this.usersService.getHistory(this.historyType, this.historyPage, this.historyPageSize).subscribe({
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

  // User-specific actions
  sendEmailVerification(user: User): void {
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update users.');
      return;
    }
    this.usersService.sendEmailVerification(user.id).subscribe({
      next: () => {
        this.notificationService.success('Success', 'Email verification sent successfully!');
      },
      error: (error) => {
        console.error('Error sending email verification:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update users.');
        } else {
          this.notificationService.error('Error', 'Failed to send email verification.');
        }
      }
    });
  }

  generatePasswordReset(user: User): void {
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update users.');
      return;
    }
    // Check if user's email is verified before allowing password reset
    if (!user.isEmailVerified) {
      this.notificationService.warning('Email Not Verified', 'User must verify their email before password reset can be generated.');
      return;
    }

    this.usersService.generatePasswordReset(user.id).subscribe({
      next: () => {
        this.notificationService.success('Success', 'Password reset link generated successfully!');
      },
      error: (error) => {
        console.error('Error generating password reset:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update users.');
        } else {
          this.notificationService.error('Error', 'Failed to generate password reset link.');
        }
      }
    });
  }

  resendInvitation(user: User): void {
    this.usersService.resendInvitation(user.id).subscribe({
      next: () => {
        this.notificationService.success('Success', 'Invitation resent successfully!');
      },
      error: (error) => {
        console.error('Error resending invitation:', error);
        this.notificationService.error('Error', 'Failed to resend invitation.');
      }
    });
  }

  isUserLocked(user: User): boolean {
    if (!user.lockedUntil) return false;
    const lockedUntil = new Date(user.lockedUntil);
    return lockedUntil > new Date();
  }

  unlockUser(user: User): void {
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update users.');
      return;
    }
    this.usersService.unlockUser(user.id).subscribe({
      next: () => {
        this.notificationService.success('Success', 'User unlocked successfully!');
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        console.error('Error unlocking user:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update users.');
        } else {
          this.notificationService.error('Error', 'Failed to unlock user.');
        }
      }
    });
  }

  toggleActiveStatus(user: User): void {
    if (!this.canUpdate) {
      this.notificationService.error('Access Denied', 'You do not have permission to update users.');
      return;
    }
    const newStatus = !user.isActive;
    this.usersService.setActive(user.id, newStatus).subscribe({
      next: () => {
        this.notificationService.success('Success', `User ${newStatus ? 'activated' : 'deactivated'} successfully!`);
        this.loadData();
        this.loadStatistics();
      },
      error: (error) => {
        console.error('Error toggling active status:', error);
        if (error.status === 403) {
          this.notificationService.error('Access Denied', 'You do not have permission to update users.');
        } else {
          this.notificationService.error('Error', 'Failed to update user status.');
        }
      }
    });
  }

  // Utility Methods
  formatDate(date: string): string {
    return this.commonUtility.formatDate(date);
  }

  formatDateTime(date: Date | string): string {
    return this.commonUtility.formatDateTime(date);
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

  // Helper methods for role icon and color
  getRoleColor(color?: string): string {
    return color || '#3B82F6'; // Default blue color
  }

  getRoleIcon(icon?: string): string {
    return icon || 'fas fa-user'; // Default user icon
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

  // Avatar helper method - simplified
  getUserAvatarUrl(user: User): string | null {
    if (!user.avatarUrl) return null;
    
    // Return full URLs as is
    if (user.avatarUrl.startsWith('http')) return user.avatarUrl;
    
    // Add API base URL for relative paths
    const path = user.avatarUrl.startsWith('/') ? user.avatarUrl : `/media/${user.avatarUrl}`;
    return `${environment.apiBaseUrl}${path}`;
  }

  getUserInitials(user: User): string {
    // Try firstName + lastName first
    if (user.firstName && user.lastName) {
      return `${user.firstName[0]}${user.lastName[0]}`.toUpperCase();
    }
    
    // Fallback to fullName
    if (user.fullName) {
      const names = user.fullName.split(' ');
      return names.length >= 2 
        ? `${names[0][0]}${names[names.length - 1][0]}`.toUpperCase()
        : user.fullName[0].toUpperCase();
    }
    
    return 'U';
  }

  getPasswordStrengthClass(): string {
    return '';
  }

  getPasswordStrengthText(): string {
    return '';
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
  toggleDropdown(item: User, event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }
    
    this.exportDropdownOpen = false;

    this.items.forEach((dataItem: User) => {
      if (dataItem.id !== item.id) {
        dataItem.showDropdown = false;
      }
    });

    item.showDropdown = !item.showDropdown;
  }


  closeAllDropdowns(): void {
    this.items.forEach((item: User) => {
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

    // Special handling for roleId - filter by role name
    if (fieldName === 'roleId' || fieldName === 'filter-roleId') {
      const filteredRoles = this.roles.filter(role =>
        role.name.toLowerCase().includes(searchTerm.toLowerCase())
      );
      this.dropdownStates[fieldName].filteredOptions = filteredRoles.map(r => r.id);
    } else {
      const allOptions = this.getOptionsForField(fieldName);
      this.dropdownStates[fieldName].filteredOptions = this.commonUtility.filterDropdownOptions(allOptions, searchTerm);
    }
  }

  selectOption(fieldName: string, option: string): void {
    this.commonUtility.selectFormOption(this.formData, fieldName, option);
    this.closeAllSearchableDropdowns();
    this.validateField(fieldName);
  }

  selectFilterOption(fieldName: string, option: string | any): void {
    // Handle organizationId filter which uses object with id property
    if (fieldName === 'organizationId' && typeof option === 'object' && option.id) {
      option = option.id;
    }
    // Special handling for status and verification filters - convert string to boolean
    if (fieldName === 'status') {
      if (option === '') {
        this.filterFormData.status = '';
        this.filters.isActive = undefined;
      } else {
        this.filterFormData.status = option;
        this.filters.isActive = option === 'true';
      }
    } else if (fieldName === 'verification') {
      if (option === '') {
        this.filterFormData.verification = '';
        this.filters.isEmailVerified = undefined;
      } else {
        this.filterFormData.verification = option;
        this.filters.isEmailVerified = option === 'true';
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

  /**
   * Clear dropdown value - Can be used for any dropdown field
   * @param fieldName - Field name (e.g., 'filter-department', 'roleId', etc.)
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
      } else if (actualField === 'verification') {
        this.filterFormData.verification = '';
        this.filters.isEmailVerified = undefined;
      } else {
        this.filterFormData[actualField] = '';
        this.filters[actualField] = '';
      }
      this.onFilterChange();
    } else {
      // Form dropdowns
      (this.formData as any)[fieldName] = '';
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
      if (actualField === 'status' || actualField === 'verification') {
        return !!this.filterFormData[actualField];
      }
      return !!this.filterFormData[actualField];
    } else {
      // Form dropdowns
      const value = (this.formData as any)[fieldName];
      return !!value;
    }
  }

  onSearchInput(fieldName: string, event: Event): void {
    this.commonUtility.onDropdownSearchInput(this.dropdownStates, fieldName, event, (fieldName: string) => this.filterOptions(fieldName));
  }

  getDisplayValue(fieldName: string): string {
    const value = (this.formData as any)[fieldName];

    // Special handling for roleId - display role names for multi-select
    if (fieldName === 'roleId') {
      const roleIds = this.formData.roleIds || [];
      if (roleIds.length === 0) {
        return 'Select roles';
      }
      if (roleIds.length === 1) {
        const role = this.roles.find(r => r.id === roleIds[0]);
        return role ? role.name : '';
      }
      return `${roleIds.length} roles selected`;
    }

    return this.commonUtility.getDropdownDisplayValue(value);
  }

  // Multi-select role methods
  isRoleSelected(roleId: string): boolean {
    return (this.formData.roleIds || []).includes(roleId);
  }

  toggleRoleSelection(roleId: string): void {
    if (!this.formData.roleIds) {
      this.formData.roleIds = [];
    }
    
    const index = this.formData.roleIds.indexOf(roleId);
    if (index > -1) {
      this.formData.roleIds.splice(index, 1);
    } else {
      this.formData.roleIds.push(roleId);
    }
    
    // Update roleId for backward compatibility
    this.formData.roleId = this.formData.roleIds.length > 0 ? this.formData.roleIds[0] : '';
    
    this.validateField('roleId');
  }

  getRoleName(roleId: string): string {
    const role = this.roles.find(r => r.id === roleId);
    return role ? role.name : '';
  }

  getRoleDescription(roleId?: string): string {
    if (!roleId) return '';
    const role = this.roles.find(r => r.id === roleId);
    return role?.description || '';
  }

  getFilterDisplayValue(fieldName: string): string {
    if (fieldName === 'organizationId' && this.filterFormData.organizationId) {
      const org = this.organizations.find(o => o.id === this.filterFormData.organizationId);
      return org ? org.name : '';
    }
    // Special handling for roleId - display role name instead of ID
    if (fieldName === 'roleId') {
      if (!this.filterFormData.roleId || this.filterFormData.roleId === '' || this.filterFormData.roleId === 'all') {
        return '';
      }
      const role = this.roles.find(r => r.id === this.filterFormData.roleId);
      return role ? role.name : this.filterFormData.roleId;
    }
    return this.commonUtility.getFilterDisplayValue(this.filterFormData, fieldName);
  }

  closeAllSearchableDropdowns(): void {
    Object.keys(this.dropdownStates).forEach((key: string) => {
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

  getSelectedItems(): User[] {
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

    if (!this.formData.email || this.formData.email.trim() === '') {
      this.errors['email'] = 'Email is required';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.formData.email)) {
      this.errors['email'] = 'Please enter a valid email address';
    }

    // password is generated server-side; no validation here

    // Full name is optional; backend composes from first/last if absent
    if (!this.formData.firstName || this.formData.firstName.trim() === '') {
      this.errors['firstName'] = 'First name is required';
    }
    if (!this.formData.lastName || this.formData.lastName.trim() === '') {
      this.errors['lastName'] = 'Last name is required';
    }
    if (!this.formData.roleIds || this.formData.roleIds.length === 0) {
      this.errors['roleId'] = 'At least one role is required';
    }
  }

  validateField(fieldName: string): void {
    switch (fieldName) {
      case 'email':
        if (!this.formData.email || this.formData.email.trim() === '') {
          this.errors['email'] = 'Email is required';
        } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.formData.email)) {
          this.errors['email'] = 'Please enter a valid email address';
        } else {
          delete this.errors['email'];
        }
        break;

      case 'password':
        delete this.errors['password'];
        break;

      case 'fullName':
        delete this.errors['fullName'];
        break;

      case 'firstName':
        if (!this.formData.firstName || this.formData.firstName.trim() === '') {
          this.errors['firstName'] = 'First name is required';
        } else {
          delete this.errors['firstName'];
        }
        break;

      case 'lastName':
        if (!this.formData.lastName || this.formData.lastName.trim() === '') {
          this.errors['lastName'] = 'Last name is required';
        } else {
          delete this.errors['lastName'];
        }
        break;

      case 'roleId':
        if (!this.formData.roleIds || this.formData.roleIds.length === 0) {
          this.errors['roleId'] = 'At least one role is required';
        } else {
          delete this.errors['roleId'];
        }
        break;
    }
  }

  private markAllFieldsAsTouched(): void {
    const fields = ['email', 'fullName', 'firstName', 'lastName', 'phoneNumber', 'department', 'jobTitle', 'employeeId', 'roleId'];
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
  exportUserDetailsAsPDF(): void {
    if (!this.selectedItem) {
      this.notificationService.warning('Warning', 'No user selected to export');
      return;
    }

    // Get the modal content - we'll capture just the content, not the header/buttons
    const modalBody = document.querySelector('.modal-overlay .modal .modal-body') as HTMLElement;
    const userDetailsCards = document.querySelector('.modal-overlay .modal .profile-form') as HTMLElement;

    if (!modalBody || !userDetailsCards) {
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
      html2canvas(userDetailsCards, {
        scale: 2,
        useCORS: true,
        allowTaint: false,
        backgroundColor: '#ffffff',
        scrollX: 0,
        scrollY: 0,
        windowWidth: userDetailsCards.scrollWidth,
        windowHeight: userDetailsCards.scrollHeight,
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
      pdf.save(`user-details-${this.selectedItem?.fullName.replace(/\s+/g, '-').toLowerCase()}.pdf`);

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
}