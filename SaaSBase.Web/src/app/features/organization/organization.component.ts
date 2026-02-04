import { Component, OnInit, OnDestroy, ChangeDetectorRef, ViewChild, ElementRef, AfterViewInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { Organization, OrganizationService } from '../../core/services/organization.service';
import { NotificationService } from '../../shared/services/notification.service';
import { NotificationContainerComponent } from '../../shared/components/notification-container/notification-container.component';
import { HasPermissionDirective } from '../../core/directives/has-permission.directive';
import { ThemeService } from '../../core/services/theme.service';
import { UserContextService } from '../../core/services/user-context.service';
import { AuthorizationService } from '../../core/services/authorization.service';
import { LocationsComponent } from './locations/locations.component';
import { BusinessSettingsComponent } from './business-settings/business-settings.component';
import { CurrenciesComponent } from './currencies/currencies.component';
import { TaxRatesComponent } from './tax-rates/tax-rates.component';
import { IntegrationSettingsComponent } from './integration-settings/integration-settings.component';
import { NotificationTemplatesComponent } from './notification-templates/notification-templates.component';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-organization',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    ReactiveFormsModule,
    RouterModule,
    NotificationContainerComponent, 
    HasPermissionDirective,
    LocationsComponent,
    BusinessSettingsComponent,
    CurrenciesComponent,
    TaxRatesComponent,
    IntegrationSettingsComponent,
    NotificationTemplatesComponent
  ],
  templateUrl: './organization.component.html',
  styleUrls: ['./organization.component.scss']
})
export class OrganizationComponent implements OnInit, OnDestroy, AfterViewInit {
  private subscriptions = new Subscription();
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;

  // Current organization
  organization: Organization | null = null;
  organizationId: string = '';
  isLoading = false;
  isSaving = false;

  // List view for system admin
  isSystemAdmin = false;
  isSystemRoute = false;
  showListView = false;
  organizationsList: any[] = [];
  searchTerm = '';
  selectedOrganizationId: string | null = null;

  // Active tab
  activeTab: 'profile' | 'locations' | 'business-settings' | 'currencies' | 'tax-rates' | 'integrations' | 'notifications' = 'profile';

  // Profile form
  profileForm!: FormGroup;

  // Logo upload
  uploadingLogo = false;
  @ViewChild('logoFileInput') logoFileInput!: ElementRef<HTMLInputElement>;

  // Tabs scroll arrows
  @ViewChild('tabsNav') tabsNav!: ElementRef<HTMLDivElement>;
  showLeftArrow = false;
  showRightArrow = false;

  // Tabs data
  showLocationsTab = false;
  showBusinessSettingsTab = false;
  showCurrenciesTab = false;
  showTaxRatesTab = false;
  showIntegrationsTab = false;
  showNotificationsTab = false;

  // Permission flags
  canUpdate = false;

  constructor(
    private organizationService: OrganizationService,
    private notificationService: NotificationService,
    private themeService: ThemeService,
    private userContextService: UserContextService,
    private authorizationService: AuthorizationService,
    private router: Router,
    private route: ActivatedRoute,
    private http: HttpClient,
    private fb: FormBuilder,
    private cdr: ChangeDetectorRef
  ) {
    this.initializeProfileForm();
  }

  ngOnInit(): void {
    // Check if user is system admin
    this.isSystemAdmin = this.userContextService.isSystemAdmin();
    
    // Load permissions first, then check tab visibility
    const userId = localStorage.getItem('userId');
    if (userId) {
      this.authorizationService.loadUserPermissionCodes(userId).subscribe({
        next: () => {
          // Load permission flags
          this.canUpdate = this.authorizationService.hasPermission('Organizations.Update');
          
          // Check permissions for Organization Settings tabs (Company Admin should have access)
          this.showLocationsTab = this.authorizationService.hasPermission('Organizations.Locations.Read');
          this.showBusinessSettingsTab = this.authorizationService.hasPermission('Organizations.BusinessSettings.Read');
          this.showCurrenciesTab = this.authorizationService.hasPermission('Organizations.Currencies.Read');
          this.showTaxRatesTab = this.authorizationService.hasPermission('Organizations.TaxRates.Read');
          this.showIntegrationsTab = this.authorizationService.hasPermission('Organizations.IntegrationSettings.Read');
          this.showNotificationsTab = this.authorizationService.hasPermission('Organizations.NotificationTemplates.Read');
          
          // Check if on system route
          this.isSystemRoute = this.router.url.includes('/system/organizations');
          
          // If system admin on system route, show list view
          if (this.isSystemAdmin && this.isSystemRoute) {
            this.showListView = true;
            this.loadOrganizationsList();
          } else {
            // Regular flow - load current organization (works for both System Admin and Company Admin)
            this.loadCurrentOrganization();
          }
        },
        error: (error) => {
          console.error('Error loading permissions:', error);
          // Fallback - load organization anyway
          this.isSystemRoute = this.router.url.includes('/system/organizations');
          if (this.isSystemAdmin && this.isSystemRoute) {
            this.showListView = true;
            this.loadOrganizationsList();
          } else {
            this.loadCurrentOrganization();
          }
        }
      });
    } else {
      // No userId - fallback
      this.isSystemRoute = this.router.url.includes('/system/organizations');
      if (this.isSystemAdmin && this.isSystemRoute) {
        this.showListView = true;
        this.loadOrganizationsList();
      } else {
        this.loadCurrentOrganization();
      }
    }
  }

  ngAfterViewInit(): void {
    // Check scroll arrows after view init
    setTimeout(() => {
      this.checkScrollArrows();
    }, 100);
  }

  @HostListener('window:resize', ['$event'])
  onResize(): void {
    this.checkScrollArrows();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private initializeProfileForm(): void {
    this.profileForm = this.fb.group({
      name: ['', Validators.required],
      description: [''],
      website: [''],
      email: ['', [Validators.email]],
      phone: [''],
      address: [''],
      city: [''],
      state: [''],
      country: [''],
      postalCode: [''],
      logoUrl: [''],
      primaryColor: ['#0d6efd'],
      secondaryColor: ['#6c757d'],
      isActive: [true]
    });
  }

  loadCurrentOrganization(): void {
    // Get organization ID from localStorage (set during login) or from user profile
    const orgId = localStorage.getItem('organizationId');
    
    if (orgId) {
      this.organizationId = orgId;
      this.loadOrganization(orgId);
    } else {
      // Try to get from organizations list (first one)
      this.isLoading = true;
      const sub = this.organizationService.getOrganizations().subscribe({
        next: (res: any) => {
          const orgs = Array.isArray(res?.items) ? res.items : (Array.isArray(res) ? res : []);
          if (orgs.length > 0) {
            this.organization = orgs[0];
            this.organizationId = orgs[0].id;
            localStorage.setItem('organizationId', this.organizationId);
            this.populateProfileForm();
          } else {
            this.notificationService.warning('Warning', 'No organization found. Please contact administrator.');
          }
          this.isLoading = false;
          this.cdr.detectChanges();
        },
        error: (error) => {
          console.error('Error loading organization:', error);
          this.notificationService.error('Error', 'Failed to load organization');
          this.isLoading = false;
          this.cdr.detectChanges();
        }
      });
      this.subscriptions.add(sub);
    }
  }

  loadOrganization(id: string): void {
    this.isLoading = true;
    const sub = this.organizationService.getOrganization(id).subscribe({
      next: (org: Organization) => {
        this.organization = org;
        this.organizationId = org.id;
        this.populateProfileForm();
        this.isLoading = false;
        this.cdr.detectChanges();
        // Check scroll arrows after organization loads
        setTimeout(() => {
          this.checkScrollArrows();
        }, 100);
      },
      error: (error: any) => {
        if (error?.status !== 403) {
          this.notificationService.error('Error', 'Failed to load organization');
        }
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
    this.subscriptions.add(sub);
  }

  private populateProfileForm(): void {
    if (this.organization) {
      this.profileForm.patchValue({
        name: this.organization.name || '',
        description: this.organization.description || '',
        website: this.organization.website || '',
        email: this.organization.email || '',
        phone: this.organization.phone || '',
        address: this.organization.address || '',
        city: this.organization.city || '',
        state: this.organization.state || '',
        country: this.organization.country || '',
        postalCode: this.organization.postalCode || '',
        logoUrl: this.organization.logoUrl || '',
        primaryColor: this.organization.primaryColor || '#0d6efd',
        secondaryColor: this.organization.secondaryColor || '#6c757d',
        isActive: this.organization.isActive !== undefined ? this.organization.isActive : true
      });
    }
  }

  async saveProfile(): Promise<void> {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      this.notificationService.warning('Validation', 'Please fill in all required fields');
      return;
    }

    if (!this.organizationId) {
      this.notificationService.error('Error', 'Organization ID not found');
      return;
    }

    this.isSaving = true;

    const formValue = this.profileForm.value;
      
    const sub = this.organizationService.updateOrganization(this.organizationId, formValue).subscribe({
        next: (updatedOrg: Organization) => {
          this.organization = updatedOrg;
          // Reload theme if colors were updated
          if (formValue.primaryColor || formValue.secondaryColor) {
            this.themeService.loadOrganizationTheme();
          }
        this.notificationService.success('Success', 'Organization profile updated successfully');
        this.isSaving = false;
        this.cdr.detectChanges();
      },
      error: (error: any) => {
        if (error?.status !== 403) {
          this.notificationService.error('Error', error.error?.message || 'Failed to update organization');
        }
        this.isSaving = false;
        this.cdr.detectChanges();
      }
    });
    this.subscriptions.add(sub);
  }

  setActiveTab(tab: 'profile' | 'locations' | 'business-settings' | 'currencies' | 'tax-rates' | 'integrations' | 'notifications'): void {
    this.activeTab = tab;
    
    // Load tab data when switching
    switch (tab) {
      case 'locations':
        this.showLocationsTab = true;
        break;
      case 'business-settings':
        this.showBusinessSettingsTab = true;
        break;
      case 'currencies':
        this.showCurrenciesTab = true;
        break;
      case 'tax-rates':
        this.showTaxRatesTab = true;
        break;
      case 'integrations':
        this.showIntegrationsTab = true;
        break;
      case 'notifications':
        this.showNotificationsTab = true;
        break;
    }
    
    // Check scroll arrows after tab change
    setTimeout(() => {
      this.checkScrollArrows();
    }, 50);
  }

  getTabClass(tab: string): string {
    return this.activeTab === tab ? 'active' : '';
  }

  formatDate(date: string): string {
    if (!date) return '-';
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  onLogoFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      const file = input.files[0];
      this.uploadLogo(file);
    }
  }

  uploadLogo(file: File): void {
    if (!this.organizationId) return;
    
    // Validate file type
    if (!file.type.startsWith('image/')) {
      this.notificationService.error('Invalid File', 'Please select an image file.');
      return;
    }

    // Validate file size (5MB limit)
    if (file.size > 5 * 1024 * 1024) {
      this.notificationService.error('File Too Large', 'Please select an image smaller than 5MB.');
      return;
    }

    this.uploadingLogo = true;

    this.organizationService.uploadLogo(this.organizationId, file).subscribe({
        next: (response) => {
          this.notificationService.success('Logo Updated', 'Company logo has been updated successfully.');
        // Refresh organization data
        this.loadOrganization(this.organizationId);
        this.uploadingLogo = false;
      },
      error: (error: any) => {
        if (error?.status !== 403) {
          this.notificationService.error('Upload Failed', 'Could not update company logo.');
        }
        this.uploadingLogo = false;
      }
    });
  }

  removeLogo(): void {
    if (!this.organizationId) return;

    this.uploadingLogo = true;

    this.organizationService.removeLogo(this.organizationId).subscribe({
      next: (response) => {
        this.notificationService.success('Logo Removed', 'Company logo has been removed successfully.');
        // Refresh organization data
        this.loadOrganization(this.organizationId);
        this.uploadingLogo = false;
      },
      error: (error: any) => {
        if (error?.status !== 403) {
          this.notificationService.error('Remove Failed', 'Could not remove company logo.');
        }
        this.uploadingLogo = false;
      }
    });
  }

  onLogoImageError(event: Event): void {
    // If image fails to load, clear the logo URL
    const target = event.target as HTMLImageElement;
    if (this.organization && this.organization.logoUrl) {
      this.organization.logoUrl = '';
    }
  }

  onTabsScroll(): void {
    this.checkScrollArrows();
  }

  checkScrollArrows(): void {
    if (!this.tabsNav?.nativeElement) {
      return;
    }

    const element = this.tabsNav.nativeElement;
    const scrollLeft = element.scrollLeft;
    const scrollWidth = element.scrollWidth;
    const clientWidth = element.clientWidth;

    // Show left arrow if scrolled from start
    this.showLeftArrow = scrollLeft > 0;

    // Show right arrow if there's more content to scroll
    this.showRightArrow = scrollLeft < (scrollWidth - clientWidth - 1); // -1 for rounding issues

    this.cdr.detectChanges();
  }

  scrollTabs(direction: 'left' | 'right'): void {
    if (!this.tabsNav?.nativeElement) {
      return;
    }

    const element = this.tabsNav.nativeElement;
    const scrollAmount = 200; // Pixels to scroll

    if (direction === 'left') {
      element.scrollBy({ left: -scrollAmount, behavior: 'smooth' });
    } else {
      element.scrollBy({ left: scrollAmount, behavior: 'smooth' });
    }

    // Update arrows after scroll animation
    setTimeout(() => {
      this.checkScrollArrows();
    }, 300);
  }

  // Load organizations list for system admin
  loadOrganizationsList(): void {
    this.isLoading = true;
    this.http.get<any[]>(`${this.api}/system/organizations`).subscribe({
      next: (orgs) => {
        this.organizationsList = orgs || [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Error loading organizations:', error);
        this.notificationService.error('Error', 'Failed to load organizations');
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // Select organization to view details
  selectOrganization(org: any): void {
    this.selectedOrganizationId = org.id;
    this.showListView = false;
    this.loadOrganization(org.id);
  }

  // Go back to list view
  backToList(): void {
    this.showListView = true;
    this.selectedOrganizationId = null;
    this.organization = null;
    this.organizationId = '';
    this.activeTab = 'profile';
  }

  // Filter organizations by search term
  get filteredOrganizations(): any[] {
    if (!this.searchTerm) {
      return this.organizationsList;
    }
    const term = this.searchTerm.toLowerCase();
    return this.organizationsList.filter(org => 
      org.name?.toLowerCase().includes(term) ||
      org.id?.toLowerCase().includes(term)
    );
  }
}
