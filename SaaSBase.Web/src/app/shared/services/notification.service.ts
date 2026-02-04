import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface Notification {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info' | 'unauthorized';
  title: string;
  message: string;
  duration?: number;
  dismissible?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private notifications = new BehaviorSubject<Notification[]>([]);
  public notifications$ = this.notifications.asObservable();

  private generateId(): string {
    return Math.random().toString(36).substr(2, 9);
  }

  show(notification: Omit<Notification, 'id'>): string {
    const id = this.generateId();
    const newNotification: Notification = {
      id,
      duration: 5000, // Default 5 seconds auto-close for all notifications
      dismissible: true,
      ...notification
    };

    const current = this.notifications.value;
    this.notifications.next([...current, newNotification]);

    // Auto-dismiss after duration
    if (newNotification.duration && newNotification.duration > 0) {
      setTimeout(() => {
        this.dismiss(id);
      }, newNotification.duration);
    }

    return id;
  }

  success(title: string, message: string, duration?: number): string {
    return this.show({ type: 'success', title, message, duration: duration || 3000 });
  }

  error(title: string, message: string, duration?: number): string {
    return this.show({ type: 'error', title, message, duration: duration || 20000 }); // Auto-dismiss errors after 20 seconds
  }

  warning(title: string, message: string, duration?: number): string {
    return this.show({ type: 'warning', title, message, duration: duration || 3000 });
  }

  info(title: string, message: string, duration?: number): string {
    return this.show({ type: 'info', title, message, duration: duration || 3000 });
  }

  unauthorized(title: string, message: string, duration?: number): string {
    return this.show({ type: 'unauthorized', title, message, duration: duration || 8000 });
  }

  dismiss(id: string): void {
    const current = this.notifications.value;
    this.notifications.next(current.filter(n => n.id !== id));
  }

  clear(): void {
    this.notifications.next([]);
  }
}
