import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationContainerComponent } from '../../../shared/components/notification-container/notification-container.component';
import { NotificationService } from '../../../shared/services/notification.service';
import { RouterModule } from '@angular/router';
import { BrandLogoComponent } from '../../../shared/components/brand-logo/brand-logo.component';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationContainerComponent, BrandLogoComponent],
  templateUrl: './verify-email.component.html',
  styleUrls: ['./verify-email.component.scss']
})
export class VerifyEmailComponent implements OnInit {
  isVerifying = true;
  isSuccess: boolean | null = null;
  message = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService,
    private notifications: NotificationService
  ) {}

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.isVerifying = false;
      this.isSuccess = false;
      this.message = 'Invalid verification link';
      this.notifications.error('Verification Failed', this.message);
      return;
    }

    this.authService.verifyEmail(token).subscribe({
      next: () => {
        this.isVerifying = false;
        this.isSuccess = true;
        this.message = 'Email verified successfully';
        this.notifications.success('Verified', this.message);
      },
      error: (err) => {
        this.isVerifying = false;
        this.isSuccess = false;
        this.message = err?.error?.message || 'Invalid or expired token';
        this.notifications.error('Verification Failed', this.message);
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
