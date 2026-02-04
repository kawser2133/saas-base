import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { OrganizationService } from '../../core/services/organization.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../shared/services/notification.service';
import { NotificationContainerComponent } from '../../shared/components/notification-container/notification-container.component';
import { PublicHeaderComponent } from '../../shared/components/public-header/public-header.component';
import { PublicFooterComponent } from '../../shared/components/public-footer/public-footer.component';
import { BrandLogoComponent } from '../../shared/components/brand-logo/brand-logo.component';

interface SetupStep {
  title: string;
  description: string;
  icon: string;
}

@Component({
  selector: 'app-organization-setup',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, NotificationContainerComponent, PublicHeaderComponent, PublicFooterComponent, BrandLogoComponent],
  templateUrl: './organization-setup.component.html',
  styleUrls: ['./organization-setup.component.scss']
})
export class OrganizationSetupComponent implements OnInit {
  currentStep = 1;
  totalSteps = 4;
  isSubmitting = false;
  showPassword = false;
  showConfirmPassword = false;
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;

  steps: SetupStep[] = [
    { title: 'Organization Info', description: 'Basic organization details', icon: 'fa-building' },
    { title: 'Admin Account', description: 'Create administrator account', icon: 'fa-user-shield' },
    { title: 'Contact Details', description: 'Organization contact information', icon: 'fa-address-card' },
    { title: 'Review & Complete', description: 'Review and finalize setup', icon: 'fa-check-circle' }
  ];

  organizationForm: FormGroup;
  adminForm: FormGroup;
  contactForm: FormGroup;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private organizationService: OrganizationService,
    private authService: AuthService,
    private router: Router,
    private notifications: NotificationService
  ) {
    this.organizationForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      description: ['']
    });

    this.adminForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]]
    }, { validators: this.passwordMatchValidator });

    this.contactForm = this.fb.group({
      email: ['', [Validators.email]],
      phone: [''],
      address: [''],
      city: [''],
      state: [''],
      country: [''],
      postalCode: ['']
    });
  }

  ngOnInit(): void {
    // Check if user is already authenticated
    // If yes, redirect to dashboard
  }

  passwordMatchValidator(form: FormGroup) {
    const password = form.get('password');
    const confirmPassword = form.get('confirmPassword');
    
    if (password && confirmPassword && password.value !== confirmPassword.value) {
      confirmPassword.setErrors({ passwordMismatch: true });
      return { passwordMismatch: true };
    }
    
    if (confirmPassword && confirmPassword.hasError('passwordMismatch')) {
      confirmPassword.setErrors(null);
    }
    
    return null;
  }

  nextStep(): void {
    if (this.currentStep === 1 && this.organizationForm.invalid) {
      this.organizationForm.markAllAsTouched();
      return;
    }
    if (this.currentStep === 2 && this.adminForm.invalid) {
      this.adminForm.markAllAsTouched();
      return;
    }
    // Step 4 (demo data) doesn't require validation
    if (this.currentStep < this.totalSteps) {
      this.currentStep++;
      this.scrollToTop();
    }
  }

  previousStep(): void {
    if (this.currentStep > 1) {
      this.currentStep--;
      this.scrollToTop();
    }
  }

  goToStep(step: number): void {
    if (step >= 1 && step <= this.totalSteps) {
      this.currentStep = step;
      this.scrollToTop();
    }
  }

  private scrollToTop(): void {
    // Scroll window to top
    window.scrollTo({
      top: 0,
      behavior: 'smooth'
    });

    // Also try to scroll any content wrapper
    const contentWrapper = document.querySelector('.content-wrapper');
    if (contentWrapper) {
      contentWrapper.scrollTo({
        top: 0,
        behavior: 'smooth'
      });
    }
  }

  getStepStatus(step: number): string {
    if (step < this.currentStep) return 'completed';
    if (step === this.currentStep) return 'active';
    return 'pending';
  }

  async submit(): Promise<void> {
    if (this.isSubmitting) return;

    // Validate all forms
    if (this.organizationForm.invalid || this.adminForm.invalid) {
      this.organizationForm.markAllAsTouched();
      this.adminForm.markAllAsTouched();
      this.notifications.error('Validation Error', 'Please fill in all required fields');
      return;
    }

    this.isSubmitting = true;

    try {
      // Prepare organization data
      const orgData = {
        name: this.organizationForm.value.name,
        description: this.organizationForm.value.description || null,
        email: this.contactForm.value.email || this.adminForm.value.email || null,
        phone: this.contactForm.value.phone || null,
        address: this.contactForm.value.address || null,
        city: this.contactForm.value.city || null,
        state: this.contactForm.value.state || null,
        country: this.contactForm.value.country || null,
        postalCode: this.contactForm.value.postalCode || null
      };

      // Prepare admin user data
      const adminData = {
        email: this.adminForm.value.email,
        password: this.adminForm.value.password,
        firstName: this.adminForm.value.firstName,
        lastName: this.adminForm.value.lastName,
        fullName: `${this.adminForm.value.firstName} ${this.adminForm.value.lastName}`,
        isActive: true
      };

      // Call registration endpoint (combines org + user creation)
      // Note: This assumes you have a public registration endpoint
      // If not, you'll need to create one in the backend: POST /api/v1/auth/register
      // Default access data (menus, permissions, Sessions, MFA) will be created automatically
      this.http.post(`${this.api}/auth/register`, {
        organization: orgData,
        adminUser: adminData,
        createDemoData: false // Always false - default access data is created automatically
      }).subscribe({
        next: (response: any) => {
          this.notifications.success('Setup Complete', 'Your organization has been set up successfully! Please sign in to continue.');
          
          // Redirect to login
          setTimeout(() => {
            this.router.navigate(['/login'], { 
              queryParams: { 
                email: this.adminForm.value.email,
                setup: 'complete'
              } 
            });
          }, 2000);
        },
        error: (error: any) => {
          this.isSubmitting = false;
          let errorMessage = 'Failed to complete setup. Please try again.';
          
          if (error?.error?.detail) {
            errorMessage = error.error.detail;
          } else if (error?.error?.message) {
            errorMessage = error.error.message;
          } else if (error?.status === 401 || error?.status === 403) {
            errorMessage = 'Registration requires authentication. Please contact support or use an existing account.';
          } else if (error?.status === 409) {
            errorMessage = 'An organization or user with this email already exists. Please use a different email.';
          }
          
          this.notifications.error('Setup Failed', errorMessage);
        }
      });

    } catch (error: any) {
      this.isSubmitting = false;
      const errorMessage = error?.error?.message || 'Failed to complete setup. Please try again.';
      this.notifications.error('Setup Failed', errorMessage);
    }
  }

  getFieldError(form: FormGroup, fieldName: string): string {
    const field = form.get(fieldName);
    if (field && field.invalid && field.touched) {
      if (field.errors?.['required']) return `${fieldName} is required`;
      if (field.errors?.['email']) return 'Invalid email address';
      if (field.errors?.['minlength']) return `${fieldName} is too short`;
      if (field.errors?.['passwordMismatch']) return 'Passwords do not match';
    }
    return '';
  }
}

