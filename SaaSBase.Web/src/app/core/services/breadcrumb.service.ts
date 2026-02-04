import { Injectable } from '@angular/core';
import { Router, NavigationEnd, ActivatedRoute } from '@angular/router';
import { BehaviorSubject, filter } from 'rxjs';
import { MenuService, UserMenuDto, MenuSectionDto } from './menu.service';

export interface BreadcrumbItem {
  label: string;
  url: string;
  icon?: string;
}

@Injectable({
  providedIn: 'root'
})
export class BreadcrumbService {
  private breadcrumbsSubject = new BehaviorSubject<BreadcrumbItem[]>([]);
  public breadcrumbs$ = this.breadcrumbsSubject.asObservable();
  private menuCache: MenuSectionDto[] = [];

  // Fallback route to label mapping (if menu not available)
  private routeLabels: { [key: string]: { label: string; icon?: string } } = {
    'dashboard': { label: 'Dashboard', icon: 'fa-home' },
    'auth/users': { label: 'Users', icon: 'fa-users' },
    'auth/roles': { label: 'Roles', icon: 'fa-user-tag' },
    'auth/permissions': { label: 'Permissions', icon: 'fa-key' },
    'auth/sessions': { label: 'Sessions', icon: 'fa-laptop-code' },
    'auth/mfa': { label: 'Multi-Factor Authentication', icon: 'fa-shield-alt' },
    'auth/password-policy': { label: 'Password Policy', icon: 'fa-lock' },
    'profile': { label: 'Profile', icon: 'fa-user' },
    'organizations': { label: 'Organization Settings', icon: 'fa-building' },
    'master-data/departments': { label: 'Departments', icon: 'fa-building' },
    'master-data/positions': { label: 'Positions', icon: 'fa-briefcase' },
    'auth/menus': { label: 'Menus', icon: 'fa-bars' },
    'settings': { label: 'Settings', icon: 'fa-cog' },
  };

  constructor(
    private router: Router, 
    private activatedRoute: ActivatedRoute,
    private menuService: MenuService
  ) {
    // Load menu structure
    this.loadMenuStructure();
    
    // Initialize breadcrumbs on service creation
    this.updateBreadcrumbs();
    
    // Update breadcrumbs on navigation
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.updateBreadcrumbs();
      });
  }

  private loadMenuStructure(): void {
    const userId = localStorage.getItem('userId');
    if (userId) {
      this.menuService.getUserMenus(userId).subscribe(response => {
        this.menuCache = response.sections || [];
        // Update breadcrumbs after menu is loaded
        this.updateBreadcrumbs();
      });
    }
  }

  private updateBreadcrumbs(): void {
    const breadcrumbs: BreadcrumbItem[] = [];
    const currentUrl = this.router.url;
    const normalizedUrl = currentUrl.startsWith('/') ? currentUrl : '/' + currentUrl;

    // Always start with Dashboard
    if (normalizedUrl !== '/dashboard') {
      breadcrumbs.push({
        label: 'Dashboard',
        url: '/dashboard',
        icon: 'fa-home'
      });
    }

    // Find menu item for current route
    const menuResult = this.findMenuItemByRoute(normalizedUrl, this.menuCache);
    
    if (menuResult) {
      // Build breadcrumb hierarchy from menu structure
      this.buildBreadcrumbFromMenu(menuResult, this.menuCache, breadcrumbs, normalizedUrl);
    } else {
      // Fallback: use route-based breadcrumbs
      this.buildBreadcrumbFromRoute(normalizedUrl, breadcrumbs);
    }

    // Remove duplicates while preserving order
    const uniqueBreadcrumbs: BreadcrumbItem[] = [];
    const seenUrls = new Set<string>();
    
    breadcrumbs.forEach(crumb => {
      if (!seenUrls.has(crumb.url)) {
        seenUrls.add(crumb.url);
        uniqueBreadcrumbs.push(crumb);
      }
    });

    this.breadcrumbsSubject.next(uniqueBreadcrumbs);
  }

  private findMenuItemByRoute(route: string, sections: MenuSectionDto[]): { item: UserMenuDto; parent: UserMenuDto | null } | null {
    const normalizedRoute = this.normalizeRoute(route);
    
    for (const section of sections) {
      for (const item of section.items) {
        // Check if this item matches
        if (this.normalizeRoute(item.route) === normalizedRoute) {
          return { item, parent: null };
        }
        // Check submenu
        if (item.submenu && item.submenu.length > 0) {
          for (const subItem of item.submenu) {
            if (this.normalizeRoute(subItem.route) === normalizedRoute) {
              return { item: subItem, parent: item };
            }
          }
        }
      }
    }
    return null;
  }

  private buildBreadcrumbFromMenu(
    menuResult: { item: UserMenuDto; parent: UserMenuDto | null }, 
    sections: MenuSectionDto[], 
    breadcrumbs: BreadcrumbItem[],
    currentUrl: string
  ): void {
    const { item, parent } = menuResult;
    
    // Add parent first if exists
    if (parent) {
      // Parent menu might not have a route (just a category)
      // If it has a route, make it clickable, otherwise just show label
      const parentUrl = parent.route ? this.normalizeRoute(parent.route) : null;
      
      if (parentUrl && parentUrl !== '/dashboard') {
        const parentExists = breadcrumbs.some(b => b.url === parentUrl);
        if (!parentExists) {
          breadcrumbs.push({
            label: parent.label,
            url: parentUrl,
            icon: parent.icon
          });
        }
      } else if (!parentUrl) {
        // Parent has no route - add as non-clickable breadcrumb
        // We'll handle this in the component by checking if url is empty
        breadcrumbs.push({
          label: parent.label,
          url: '', // Empty URL means non-clickable
          icon: parent.icon
        });
      }
    }
    
    // Add current menu item
    const itemUrl = this.normalizeRoute(item.route);
    if (itemUrl && itemUrl !== '/dashboard') {
      const itemExists = breadcrumbs.some(b => b.url === itemUrl);
      if (!itemExists) {
        breadcrumbs.push({
          label: item.label,
          url: itemUrl,
          icon: item.icon
        });
      }
    }
  }

  private buildBreadcrumbFromRoute(route: string, breadcrumbs: BreadcrumbItem[]): void {
    const segments = route.split('/').filter(s => s.length > 0);
    const urlSegments: string[] = [];

    segments.forEach((segment, index) => {
      urlSegments.push(segment);
      const url = '/' + urlSegments.join('/');
      const routeKey = urlSegments.join('/');
      
      // Check for exact match in fallback labels
      const routeInfo = this.routeLabels[routeKey];
      
      if (routeInfo) {
        const exists = breadcrumbs.some(b => b.url === url);
        if (!exists && url !== '/dashboard') {
          breadcrumbs.push({
            label: routeInfo.label,
            url: url,
            icon: routeInfo.icon
          });
        }
      } else if (index === segments.length - 1) {
        // Last segment - add with formatted label
        const formattedLabel = this.formatLabel(segment);
        breadcrumbs.push({
          label: formattedLabel,
          url: url
        });
      }
    });
  }

  private normalizeRoute(route: string): string {
    if (!route) return '';
    return route.startsWith('/') ? route : '/' + route;
  }

  private formatLabel(segment: string): string {
    return segment
      .split('-')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }

  // Method to manually set breadcrumbs if needed
  setBreadcrumbs(breadcrumbs: BreadcrumbItem[]): void {
    this.breadcrumbsSubject.next(breadcrumbs);
  }
}

