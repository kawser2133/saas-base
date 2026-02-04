import { Component, HostListener, OnInit, ViewChild, ElementRef, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { HeaderComponent } from '../header/header.component';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { FooterComponent } from '../footer/footer.component';
import { NotificationContainerComponent } from '../../components/notification-container/notification-container.component';
import { ThemeService } from '../../../core/services/theme.service';
import { OrganizationService } from '../../../core/services/organization.service';
import { NotificationService } from '../../services/notification.service';
import { filter, Subscription } from 'rxjs';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, HeaderComponent, SidebarComponent, FooterComponent, NotificationContainerComponent],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  sidebarCollapsed = false;
  mobileSidebarOpen = false;
  forceExpanded = false; // For 1025px-1140px range to temporarily show full menu
  organizationInactive = false;
  private subscriptions = new Subscription();
  private isInitialized = false; // Track if component has been initialized

  @ViewChild('contentWrapper', { read: ElementRef }) contentWrapper?: ElementRef;

  constructor(
    private router: Router,
    private themeService: ThemeService,
    private organizationService: OrganizationService,
    private notificationService: NotificationService
  ) {}

  toggleSidebar() {
    const width = window.innerWidth;
    if (width <= 1024) {
      this.mobileSidebarOpen = !this.mobileSidebarOpen;
    } else if (width > 1024 && width <= 1140) {
      // 1025px-1140px: Toggle between icon-only and full menu
      this.forceExpanded = !this.forceExpanded;
      this.sidebarCollapsed = this.forceExpanded ? false : true;
    } else {
      // ≥1141px: Normal toggle
      this.sidebarCollapsed = !this.sidebarCollapsed;
      this.forceExpanded = false;
    }
  }

  @HostListener('window:resize')
  onResize() {
    const width = window.innerWidth;
    if (width > 1024 && this.mobileSidebarOpen) {
      this.mobileSidebarOpen = false;
    }
    
    // Tablet/Small Desktop (1025px - 1140px): default to icon-only (collapsed)
    if (width > 1024 && width <= 1140) {
      if (!this.forceExpanded) {
        this.sidebarCollapsed = true; // Icon-only view
      }
    } else if (width > 1140) {
      // Desktop (≥1141px): default to expanded full sidebar
      this.sidebarCollapsed = false;
      this.forceExpanded = false;
    } else if (width <= 1024) {
      // Mobile: reset states
      this.forceExpanded = false;
    }
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape' && this.mobileSidebarOpen) {
      this.mobileSidebarOpen = false;
    }
  }

  ngOnInit(): void {
    // Only initialize once - prevent reload on route changes within same layout
    if (this.isInitialized) {
      return;
    }

    this.isInitialized = true;

    // Load and apply organization theme colors
    this.themeService.loadOrganizationTheme();
    
    // Check organization status
    this.checkOrganizationStatus();
    
    const width = window.innerWidth;
    // Tablet/Small Desktop (1025px - 1140px): default to icon-only (collapsed)
    if (width > 1024 && width <= 1140) {
      this.sidebarCollapsed = true; // Icon-only view
      this.forceExpanded = false;
      this.mobileSidebarOpen = false;
    } else if (width > 1140) {
      // Desktop (≥1141px): default to expanded full sidebar
      this.sidebarCollapsed = false;
      this.forceExpanded = false;
      this.mobileSidebarOpen = false;
    }

    // Scroll to top on route change
    const navSub = this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.scrollToTop();
    });
    this.subscriptions.add(navSub);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private checkOrganizationStatus(): void {
    const orgId = localStorage.getItem('organizationId');
    if (orgId) {
      const sub = this.organizationService.getOrganization(orgId).subscribe({
        next: (org) => {
          if (!org.isActive) {
            this.organizationInactive = true;
            this.notificationService.warning(
              'Organization Inactive',
              'Your organization account is currently inactive. Some features may be limited. Please contact your administrator.'
            );
          } else {
            this.organizationInactive = false;
          }
        },
        error: () => {
          // Silently fail - don't block the app if check fails
        }
      });
      this.subscriptions.add(sub);
    }
  }

  private scrollToTop(): void {
    // Try to scroll the content wrapper
    const contentWrapper = document.querySelector('.content-wrapper');
    if (contentWrapper) {
      contentWrapper.scrollTo({
        top: 0,
        behavior: 'smooth' // Smooth scroll behavior
      });
    }

    // Also scroll window as fallback
    window.scrollTo({
      top: 0,
      behavior: 'auto' as ScrollBehavior // Use 'smooth' for smooth scroll
    });
  }
}