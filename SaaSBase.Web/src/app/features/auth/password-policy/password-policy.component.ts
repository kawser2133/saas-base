import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PasswordPolicyDto, PasswordPolicyService } from '../../../core/services/password-policy.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { NotificationService } from '../../../shared/services/notification.service';

@Component({
  selector: 'app-password-policy',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NotificationContainerComponent, BreadcrumbComponent],
  templateUrl: './password-policy.component.html',
  styleUrls: ['./password-policy.component.scss']
})
export class PasswordPolicyComponent implements OnInit {
  policy: PasswordPolicyDto | null = null;
  isLoading = false;
  saving = false;
  testPassword = '';
  testResult: any = null;
  showTestPassword = false;

  constructor(private policyService: PasswordPolicyService, private notifications: NotificationService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.policyService.getPolicy().subscribe({
      next: p => { this.policy = p; this.isLoading = false; },
      error: () => { this.notifications.error('Error', 'Failed to load password policy'); this.isLoading = false; }
    });
  }

  save(): void {
    if (!this.policy) return;
    this.saving = true;
    this.policyService.updatePolicy(this.policy).subscribe({
      next: (p) => { 
        this.policy = p; 
        this.saving = false; 
        this.notifications.success('Saved', 'Password policy updated successfully');
        // Reload to ensure we have the latest from backend
        this.load();
      },
      error: (err) => { 
        this.saving = false; 
        const errorMessage = err?.error?.message || err?.message || 'Failed to update password policy';
        this.notifications.error('Error', errorMessage);
        console.error('Password policy update error:', err);
      }
    });
  }

  runValidation(): void {
    if (!this.testPassword) { 
      this.testResult = { isValid: false, message: 'Enter a password to test.' }; 
      return; 
    }
    
    if (!this.policy) {
      this.testResult = { isValid: false, message: 'Policy not loaded. Please refresh.' };
      return;
    }

    // Validate against current policy (including unsaved changes)
    const result = this.validatePasswordAgainstPolicy(this.testPassword, this.policy);
    this.testResult = result;
  }

  resetTester(): void {
    this.testResult = null;
    this.testPassword = '';
    this.showTestPassword = false;
  }

  private validatePasswordAgainstPolicy(password: string, policy: PasswordPolicyDto): any {
    const errors: string[] = [];
    const warnings: string[] = [];
    let isValid = true;

    // Check minimum length
    if (password.length < policy.minLength) {
      isValid = false;
      errors.push(`Password must be at least ${policy.minLength} characters long`);
    }

    // Check maximum length
    if (password.length > policy.maxLength) {
      isValid = false;
      errors.push(`Password must be no more than ${policy.maxLength} characters long`);
    }

    // Check uppercase
    if (policy.requireUppercase && !/[A-Z]/.test(password)) {
      isValid = false;
      errors.push('Password must contain at least one uppercase letter');
    }

    // Check lowercase
    if (policy.requireLowercase && !/[a-z]/.test(password)) {
      isValid = false;
      errors.push('Password must contain at least one lowercase letter');
    }

    // Check numbers
    if (policy.requireNumbers && !/[0-9]/.test(password)) {
      isValid = false;
      errors.push('Password must contain at least one number');
    }

    // Check special characters
    const requireSpecial = policy.requireSpecialCharacters ?? policy.requireSpecial ?? false;
    if (requireSpecial && !/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) {
      isValid = false;
      const minSpecial = policy.minSpecialCharacters ?? 1;
      errors.push(`Password must contain at least ${minSpecial} special character${minSpecial > 1 ? 's' : ''}`);
    }

    // Calculate strength score
    let strengthScore = 0;
    let strengthLevel = '';

    if (password.length >= policy.minLength) strengthScore += 20;
    if (password.length >= policy.minLength + 4) strengthScore += 10;
    if (password.length >= policy.minLength + 8) strengthScore += 10;
    if (/[A-Z]/.test(password)) strengthScore += 15;
    if (/[a-z]/.test(password)) strengthScore += 15;
    if (/[0-9]/.test(password)) strengthScore += 15;
    if (/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) strengthScore += 15;

    if (strengthScore < 40) {
      strengthLevel = 'Weak';
    } else if (strengthScore < 60) {
      strengthLevel = 'Medium';
    } else if (strengthScore < 80) {
      strengthLevel = 'Strong';
    } else {
      strengthLevel = 'Very Strong';
    }

    return {
      isValid,
      errors,
      warnings,
      strengthScore: Math.min(strengthScore, 100),
      strengthLevel
    };
  }

}


