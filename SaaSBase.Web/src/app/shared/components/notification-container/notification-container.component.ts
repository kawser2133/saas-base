import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, Notification } from '../../services/notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-notification-container',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="notification-container">
      <div 
        *ngFor="let notification of notifications; trackBy: trackByFn"
        class="notification"
        [class]="'notification-' + notification.type"
      >
        <div class="notification-icon">
          <i [class]="getIconClass(notification.type)"></i>
        </div>
        <div class="notification-content">
          <div class="notification-title">{{ notification.title }}</div>
          <div class="notification-message">{{ notification.message }}</div>
        </div>
        <button 
          *ngIf="notification.dismissible"
          class="notification-close"
          (click)="dismiss(notification.id)"
          type="button"
        >
          <i class="fas fa-times"></i>
        </button>
      </div>
    </div>
  `,
  styles: [`
    .notification-container {
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 9999;
      max-width: 400px;
      width: 100%;
    }

    .notification {
      display: flex;
      align-items: flex-start;
      padding: 16px;
      margin-bottom: 12px;
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      background: white;
      border-left: 4px solid;
      animation: slideIn 0.3s ease-out;
    }

    .notification-success {
      border-left-color: #28a745;
    }

    .notification-error {
      border-left-color: #dc3545;
    }

    .notification-warning {
      border-left-color: #ffc107;
    }

    .notification-info {
      border-left-color: #17a2b8;
    }

    .notification-unauthorized {
      border-left-color: #fd7e14;
      background: linear-gradient(135deg, #fff7ed 0%, #ffedd5 100%);
    }

    .notification-icon {
      margin-right: 12px;
      margin-top: 2px;
    }

    .notification-icon i {
      font-size: 18px;
    }

    .notification-success .notification-icon i {
      color: #28a745;
    }

    .notification-error .notification-icon i {
      color: #dc3545;
    }

    .notification-warning .notification-icon i {
      color: #ffc107;
    }

    .notification-info .notification-icon i {
      color: #17a2b8;
    }

    .notification-unauthorized .notification-icon i {
      color: #fd7e14;
    }

    .notification-content {
      flex: 1;
      min-width: 0;
    }

    .notification-title {
      font-weight: 600;
      font-size: 14px;
      margin-bottom: 4px;
      color: #333;
    }

    .notification-message {
      font-size: 13px;
      color: #666;
      line-height: 1.4;
    }

    .notification-close {
      background: none;
      border: none;
      color: #999;
      cursor: pointer;
      padding: 4px;
      margin-left: 8px;
      border-radius: 4px;
      transition: all 0.2s;
    }

    .notification-close:hover {
      background: #f5f5f5;
      color: #666;
    }

    @keyframes slideIn {
      from {
        transform: translateX(100%);
        opacity: 0;
      }
      to {
        transform: translateX(0);
        opacity: 1;
      }
    }

    @media (max-width: 480px) {
      .notification-container {
        left: 10px;
        right: 10px;
        max-width: none;
      }
    }
  `]
})
export class NotificationContainerComponent implements OnInit, OnDestroy {
  notifications: Notification[] = [];
  private subscription: Subscription = new Subscription();

  constructor(private notificationService: NotificationService) {}

  ngOnInit(): void {
    this.subscription.add(
      this.notificationService.notifications$.subscribe(
        notifications => this.notifications = notifications
      )
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  trackByFn(index: number, notification: Notification): string {
    return notification.id;
  }

  getIconClass(type: string): string {
    const icons = {
      success: 'fas fa-check-circle',
      error: 'fas fa-exclamation-circle',
      warning: 'fas fa-exclamation-triangle',
      info: 'fas fa-info-circle',
      unauthorized: 'fas fa-ban'
    };
    return icons[type as keyof typeof icons] || 'fas fa-info-circle';
  }

  dismiss(id: string): void {
    this.notificationService.dismiss(id);
  }
}
