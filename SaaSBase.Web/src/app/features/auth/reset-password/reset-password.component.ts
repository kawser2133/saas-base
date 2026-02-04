import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { PasswordPolicyService } from '../../../core/services/password-policy.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { NotificationService } from '../../../shared/services/notification.service';
import { RouterModule } from '@angular/router';
import { BrandLogoComponent } from '../../../shared/components/brand-logo/brand-logo.component';
import { Subject, debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, NotificationContainerComponent, BrandLogoComponent],
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.scss']
})
export class ResetPasswordComponent implements OnInit, OnDestroy {
  resetForm: FormGroup;
  token: string | null = null;
  isResetting = false;
  isSuccess: boolean | null = null;
  message = '';
  showNewPassword = false;
  showConfirmPassword = false;
  passwordValidationErrors: string[] = [];
  isValidatingPassword = false;
  
  private destroy$ = new Subject<void>();
  private passwordSubject$ = new Subject<string>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService,
    private passwordPolicyService: PasswordPolicyService,
    private notifications: NotificationService,
    private fb: FormBuilder
  ) {
    this.resetForm = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token');

    if (!this.token) {
      this.isSuccess = false;
      this.message = 'Invalid reset link';
      this.notifications.error('Reset Failed', this.message);
      return;
    }

    // Setup real-time password validation with debounce
    this.passwordSubject$.pipe(
      debounceTime(500), // Wait 500ms after user stops typing
      distinctUntilChanged(), // Only validate if password changed
      switchMap(password => {
        this.isValidatingPassword = true;
        this.passwordValidationErrors = [];
        // For reset password, we don't have userId yet, so pass null
        return this.passwordPolicyService.validate(password);
      }),
      takeUntil(this.destroy$)
    ).subscribe({
      next: (result) => {
        this.isValidatingPassword = false;
        if (!result.isValid && result.errors && result.errors.length > 0) {
          this.passwordValidationErrors = result.errors;
          // Set custom validation error on form control
          const passwordControl = this.resetForm.get('newPassword');
          if (passwordControl) {
            passwordControl.setErrors({ 
              ...passwordControl.errors,
              policyValidation: true 
            });
          }
        } else {
          this.passwordValidationErrors = [];
          // Clear policy validation error if password is valid
          const passwordControl = this.resetForm.get('newPassword');
          if (passwordControl && passwordControl.errors?.['policyValidation']) {
            const errors = { ...passwordControl.errors };
            delete errors['policyValidation'];
            passwordControl.setErrors(Object.keys(errors).length > 0 ? errors : null);
          }
        }
      },
      error: (err) => {
        this.isValidatingPassword = false;
        // Don't show error notification for validation API failures
        // Just log it silently
        console.error('Password validation error:', err);
      }
    });

    // Subscribe to password field changes
    this.resetForm.get('newPassword')?.valueChanges.pipe(
      takeUntil(this.destroy$)
    ).subscribe(password => {
      if (password && password.length >= 8) {
        this.passwordSubject$.next(password);
      } else {
        this.passwordValidationErrors = [];
        this.isValidatingPassword = false;
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  passwordMatchValidator(form: FormGroup) {
    const password = form.get('newPassword');
    const confirmPassword = form.get('confirmPassword');
    
    if (password && confirmPassword && password.value !== confirmPassword.value) {
      confirmPassword.setErrors({ passwordMismatch: true });
    } else {
      confirmPassword?.setErrors(null);
    }
    
    return null;
  }

  onSubmit(): void {
    if (this.resetForm.valid && this.token) {
      this.isResetting = true;
      
      const { newPassword } = this.resetForm.value;
      
      this.authService.resetPassword({ token: this.token, newPassword }).subscribe({
        next: () => {
          this.isResetting = false;
          this.isSuccess = true;
          this.message = 'Password reset successfully. You can now sign in.';
          this.notifications.success('Password Reset', this.message);
        },
        error: (err) => {
          this.isResetting = false;
          this.isSuccess = false;
          
          // Extract error message from various possible locations
          // Backend uses ProblemDetails middleware which returns errors in different formats
          // ProblemDetails format: { type, title, status, detail, instance }
          // ArgumentException message goes in 'detail' field
          const errorObj = err?.error || {};
          
          let errorMessage = 'Invalid or expired token';
          
          // Try multiple locations for error message (priority order)
          // ProblemDetails middleware puts exception message in 'detail' field
          if (errorObj.detail) {
            errorMessage = errorObj.detail;
          } else if (errorObj.message) {
            errorMessage = errorObj.message;
          } else if (err?.message) {
            errorMessage = err.message;
          } else if (typeof err?.error === 'string') {
            errorMessage = err.error;
          } else if (errorObj.title && errorObj.title !== 'Bad Request') {
            // Use title only if it's not the generic "Bad Request"
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
          
          this.message = errorMessage;
          this.notifications.error('Reset Failed', errorMessage);
        }
      });
    } else {
      this.markFormGroupTouched();
    }
  }

  private markFormGroupTouched(): void {
    Object.keys(this.resetForm.controls).forEach(key => {
      const control = this.resetForm.get(key);
      control?.markAsTouched();
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  getFieldError(fieldName: string): string {
    const field = this.resetForm.get(fieldName);
    if (field?.errors && field.touched) {
      if (field.errors['required']) return `${fieldName} is required`;
      if (field.errors['minlength']) return `${fieldName} must be at least 8 characters`;
      if (field.errors['passwordMismatch']) return 'Passwords do not match';
      if (field.errors['policyValidation'] && this.passwordValidationErrors.length > 0) {
        return this.passwordValidationErrors[0]; // Show first error
      }
    }
    return '';
  }
}
