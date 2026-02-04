import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { NotificationService } from '../../../shared/services/notification.service';
import { RouterModule } from '@angular/router';
import { BrandLogoComponent } from '../../../shared/components/brand-logo/brand-logo.component';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, NotificationContainerComponent, BrandLogoComponent],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.scss']
})
export class ForgotPasswordComponent {
  form: FormGroup;
  isSubmitting = false;
  submitted = false;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private notifications: NotificationService
  ) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  submit(): void {
    if (this.isSubmitting || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    const email = this.form.value.email as string;

    this.auth.forgotPassword({ email }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.submitted = true;
        this.notifications.success('Email Sent', 'Reset link sent to your email');
      },
      error: (err) => {
        this.isSubmitting = false;
        const message = err?.error?.message || 'Unable to send reset email';
        this.notifications.error('Request Failed', message);
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}


