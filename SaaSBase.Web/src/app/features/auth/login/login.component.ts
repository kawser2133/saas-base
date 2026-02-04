import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { isAuthenticated, setAuthData } from '../../../core/auth/auth.utils';
import { NotificationService } from '../../../shared/services/notification.service';
import { ValidationService } from '../../../shared/services/validation.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BrandLogoComponent } from '../../../shared/components/brand-logo/brand-logo.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NotificationContainerComponent, BrandLogoComponent],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  credentials = { email: '', password: '' };
  isSubmitting = false;
  formErrors: { [key: string]: string } = {};
  showPassword = false;
  requiresMfa = false;
  enabledMfaMethods: string[] = [];
  tempUserId: string | null = null;
  selectedMfaMethod: string = '';
  mfaCode: string = '';
  isSendingMfaCode = false;
  isVerifyingMfa = false;
  phoneNumberMasked: string | null = null;
  emailMasked: string | null = null;

  constructor(
    private auth: AuthService, 
    private router: Router,
    private notificationService: NotificationService,
    private validationService: ValidationService
  ) {}

  ngOnInit(): void {
    if (isAuthenticated()) {
      this.router.navigate(['/app/dashboard'], { replaceUrl: true });
    }
  }

  validateForm(): boolean {
    this.formErrors = {};
    let isValid = true;

    const emailErrors = this.validationService.validate(this.credentials.email, [
      ValidationService.RULES.required('Email is required'),
      ValidationService.RULES.email('Please enter a valid email address')
    ]);
    if (emailErrors.length > 0) {
      this.formErrors['email'] = emailErrors[0];
      isValid = false;
    }

    const passwordErrors = this.validationService.validate(this.credentials.password, [
      ValidationService.RULES.required('Password is required'),
      ValidationService.RULES.minLength(6, 'Password must be at least 6 characters')
    ]);
    if (passwordErrors.length > 0) {
      this.formErrors['password'] = passwordErrors[0];
      isValid = false;
    }

    return isValid;
  }

  goToForgotPassword(): void {
    this.router.navigate(['/forgot-password']);
  }

  submit(form: NgForm, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    
    if (this.isSubmitting) {
      return;
    }
    
    if (!this.validateForm()) {
      this.notificationService.error('Validation Error', 'Please fix the errors below');
      return;
    }

    this.isSubmitting = true;
    this.formErrors = {};

    this.auth.login(this.credentials).subscribe({
      next: (res) => {
        // Check if MFA is required
        if (res?.requiresMfa === true && res?.enabledMfaMethods && res?.enabledMfaMethods.length > 0) {
          this.requiresMfa = true;
          // Filter to only include SMS, EMAIL, and BACKUPCODE (exclude TOTP and others)
          const filteredMethods = res.enabledMfaMethods.filter((m: string) => 
            m.toUpperCase() === 'SMS' || m.toUpperCase() === 'EMAIL' || m.toUpperCase() === 'BACKUPCODE'
          );
          // Sort methods: SMS and Email first, BackupCode always last
          this.enabledMfaMethods = this.sortMfaMethods(filteredMethods);
          this.tempUserId = res?.tempUserId ?? res?.user?.id ?? null;
          // Use default method from response, or fallback to first method
          const defaultMethod = res?.defaultMfaMethod;
          if (defaultMethod && this.enabledMfaMethods.includes(defaultMethod)) {
            this.selectedMfaMethod = defaultMethod;
          } else {
            this.selectedMfaMethod = this.enabledMfaMethods[0]; // Default to first method
          }
          // Store masked values for display
          this.phoneNumberMasked = res?.phoneNumberMasked ?? null;
          this.emailMasked = res?.emailMasked ?? null;
          this.isSubmitting = false;
          
          // Automatically send code for the selected method (only for SMS/Email, not backup codes)
          const isBackupCode = this.selectedMfaMethod?.toUpperCase() === 'BACKUPCODE';
          if (this.tempUserId && this.selectedMfaMethod && !isBackupCode) {
            this.sendMfaCode();
          } else if (isBackupCode) {
            // Show info message for BackupCode with longer duration
            this.notificationService.info('Backup Code', 'Backup codes are pre-generated. Please enter a code you saved earlier.', 8000);
          }
          return;
        }
        
        // Normal login flow (no MFA)
        const accessToken = res?.token ?? res?.accessToken ?? res?.access_token ?? '';
        const refreshToken = res?.refreshToken ?? res?.refresh_token ?? '';
        const expiresAtUtc = res?.expiresAt ?? res?.expiresAtUtc ?? res?.expires_at ?? null;
        const roles = res?.roles ?? res?.user?.roles ?? [];
        const userId = res?.user?.id ?? res?.userId ?? null;
        
        if (accessToken) {
          setAuthData({ accessToken, refreshToken, expiresAtUtc });
        }
        if (Array.isArray(roles)) localStorage.setItem('roles', roles.join(','));
        if (userId) localStorage.setItem('userId', String(userId));
        
        this.notificationService.success('Login Successful', 'Welcome back!');
        this.router.navigate(['/dashboard'], { replaceUrl: true });
      },
      error: (error) => { 
        this.isSubmitting = false;
        console.error('Login error:', error);
        
        let errorMessage = 'Login failed. Please try again.';
        let errorTitle = 'Login Failed';
        
        // Extract error message from various possible locations
        // Backend uses ProblemDetails middleware which returns errors in different formats
        // ProblemDetails format: { type, title, status, detail, instance }
        // UnauthorizedAccessException message goes in 'detail' field
        const errorObj = error?.error || {};
        
        // Try multiple locations for error message (priority order)
        // ProblemDetails middleware puts exception message in 'detail' field
        if (errorObj.detail) {
          errorMessage = errorObj.detail;
        } else if (errorObj.message) {
          errorMessage = errorObj.message;
        } else if (error?.message) {
          errorMessage = error.message;
        } else if (typeof error?.error === 'string') {
          errorMessage = error.error;
        } else if (errorObj.title && errorObj.title !== 'Unauthorized') {
          // Use title only if it's not the generic "Unauthorized"
          errorMessage = errorObj.title;
        } else if (Array.isArray(errorObj.errors)) {
          // Handle validation errors array
          const firstError = errorObj.errors[0];
          if (typeof firstError === 'string') {
            errorMessage = firstError;
          } else if (firstError?.message) {
            errorMessage = firstError.message;
          }
        }
        
        // Check for account locked message (case insensitive)
        // Backend message: "Account is locked. Try again in X minutes."
        const lowerMessage = errorMessage.toLowerCase();
        if (lowerMessage.includes('locked') || lowerMessage.includes('lock')) {
          errorTitle = 'Account Locked';
          // Show longer duration for lock messages so user can read it
          this.notificationService.error(errorTitle, errorMessage, 10000); // 10 seconds
          // Also show in password field
          this.formErrors['password'] = errorMessage;
          return; // Early return for lock messages
        }
        
        // Check for other specific messages
        if (lowerMessage.includes('deactivated') || lowerMessage.includes('inactive')) {
          errorTitle = 'Account Disabled';
          this.notificationService.error(errorTitle, errorMessage, 5000);
          this.formErrors['password'] = errorMessage;
          return;
        }
        
        // Handle other error types
        if (error?.status === 401) {
          // Only show generic message if it's not already a specific message
          if (errorMessage === 'Login failed. Please try again.' || 
              errorMessage === 'Unauthorized' ||
              errorMessage.toLowerCase().includes('invalid credentials')) {
            errorMessage = 'Invalid email or password';
          }
          this.formErrors['password'] = errorMessage;
        } else if (error?.status === 0) {
          errorMessage = 'Unable to connect to server. Please check your connection.';
        } else if (error?.status === 403) {
          errorTitle = 'Access Denied';
        }
        
        // Show notification with proper title and message
        this.notificationService.error(errorTitle, errorMessage, 5000);
      },
      complete: () => { this.isSubmitting = false; }
    });
  }

  sortMfaMethods(methods: string[]): string[] {
    // Sort: SMS and Email first, BackupCode always last
    if (!methods || methods.length === 0) return methods;
    
    const smsMethods: string[] = [];
    const emailMethods: string[] = [];
    const backupCodeMethods: string[] = [];
    const otherMethods: string[] = [];
    
    // Separate methods by type
    methods.forEach(method => {
      const upper = method.toUpperCase();
      if (upper === 'SMS') {
        smsMethods.push(method);
      } else if (upper === 'EMAIL') {
        emailMethods.push(method);
      } else if (upper === 'BACKUPCODE') {
        backupCodeMethods.push(method);
      } else {
        otherMethods.push(method);
      }
    });
    
    // Return in order: SMS, Email, Others, BackupCode (always last)
    return [...smsMethods, ...emailMethods, ...otherMethods, ...backupCodeMethods];
  }

  sendMfaCode(): void {
    if (!this.tempUserId || !this.selectedMfaMethod || this.isSendingMfaCode) {
      return;
    }

    // Backup codes don't need to be sent - they're pre-generated
    const isBackupCode = this.selectedMfaMethod?.toUpperCase() === 'BACKUPCODE';
    if (isBackupCode) {
      // Show info message with longer duration (8 seconds)
      this.notificationService.info('Backup Code', 'Backup codes are pre-generated. Please enter a code you saved earlier.', 8000);
      return; // Don't call backend
    }

    this.isSendingMfaCode = true;
    this.auth.sendMfaCodeLogin({ userId: this.tempUserId, mfaType: this.selectedMfaMethod }).subscribe({
      next: (res) => {
        if (res?.success) {
          const methodName = this.selectedMfaMethod === 'SMS' ? 'SMS' : this.selectedMfaMethod === 'EMAIL' ? 'Email' : this.selectedMfaMethod;
          this.notificationService.success('Code Sent', `Verification code sent via ${methodName}`);
        } else {
          this.notificationService.error('Error', 'Failed to send verification code');
        }
        this.isSendingMfaCode = false;
      },
      error: (error) => {
        this.isSendingMfaCode = false;
        // Check if it's a BackupCode error (shouldn't happen, but handle gracefully)
        const errorDetail = error?.error?.detail || error?.error?.message || '';
        if (errorDetail.toLowerCase().includes('backup code') || errorDetail.toLowerCase().includes('pre-generated')) {
          this.notificationService.info('Backup Code', 'Backup codes are pre-generated. Please enter a code you saved earlier.', 8000);
        } else {
          const errorMessage = errorDetail || 'Failed to send verification code';
          this.notificationService.error('Error', errorMessage);
        }
      }
    });
  }

  verifyMfaCode(): void {
    if (!this.tempUserId || !this.selectedMfaMethod || !this.mfaCode || this.isVerifyingMfa) {
      if (!this.mfaCode) {
        this.notificationService.error('Error', 'Please enter the verification code');
      }
      return;
    }

    this.isVerifyingMfa = true;
    this.auth.verifyMfaLogin({ 
      userId: this.tempUserId, 
      mfaType: this.selectedMfaMethod, 
      code: this.mfaCode 
    }).subscribe({
      next: (res) => {
        const accessToken = res?.token ?? res?.accessToken ?? res?.access_token ?? '';
        const refreshToken = res?.refreshToken ?? res?.refresh_token ?? '';
        const expiresAtUtc = res?.expiresAt ?? res?.expiresAtUtc ?? res?.expires_at ?? null;
        const roles = res?.roles ?? res?.user?.roles ?? [];
        const userId = res?.user?.id ?? res?.userId ?? null;
        
        if (accessToken) {
          setAuthData({ accessToken, refreshToken, expiresAtUtc });
        }
        if (Array.isArray(roles)) localStorage.setItem('roles', roles.join(','));
        if (userId) localStorage.setItem('userId', String(userId));
        
        this.notificationService.success('Login Successful', 'Welcome back!');
        this.router.navigate(['/dashboard'], { replaceUrl: true });
      },
      error: (error) => {
        this.isVerifyingMfa = false;
        const errorMessage = error?.error?.detail || error?.error?.message || 'Invalid verification code';
        this.notificationService.error('Error', errorMessage);
        this.mfaCode = '';
      }
    });
  }

  onMfaMethodChange(): void {
    this.mfaCode = ''; // Clear code when method changes
    
    // Check if BackupCode (case-insensitive)
    const isBackupCode = this.selectedMfaMethod?.toUpperCase() === 'BACKUPCODE';
    
    if (isBackupCode) {
      // Show info message with longer duration (8 seconds)
      this.notificationService.info('Backup Code', 'Backup codes are pre-generated. Please enter a code you saved earlier.', 8000);
      return; // Don't call backend
    }
    
    // Only send code for SMS/Email
    this.sendMfaCode();
  }

  goBackToLogin(): void {
    this.requiresMfa = false;
    this.enabledMfaMethods = [];
    this.tempUserId = null;
    this.selectedMfaMethod = '';
    this.mfaCode = '';
    this.phoneNumberMasked = null;
    this.emailMasked = null;
  }
}


