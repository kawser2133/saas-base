import { Component, EventEmitter, HostListener, Input, Output, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { MenuService } from '../../../core/services/menu.service';
import { UserContextService } from '../../../core/services/user-context.service';
import { filter } from 'rxjs/operators';

interface MenuItem {
  label: string;
  icon: string;
  route: string;
  submenu?: MenuItem[];
  expanded?: boolean;
}

interface MenuSection {
  title: string;
  items: MenuItem[];
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})

export class SidebarComponent implements OnInit {
  @Input() isCollapsed = false;
  @Input() mobileOpen = false;
  @Output() toggleSidebar = new EventEmitter<void>();
  trayItem: MenuItem | null = null;
  currentRoute: string = '';

  constructor(
    private readonly router: Router, 
    private readonly menuService: MenuService,
    private readonly userContextService: UserContextService
  ) {
    // Track current route for active menu highlighting
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: any) => {
        this.currentRoute = event.urlAfterRedirects || event.url;
        this.updateActiveMenuItems();
      });
  }

  // Dynamic menu from backend based on permissions
  menu: MenuSection[] = [];

  ngOnInit(): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    const isSystemAdmin = this.userContextService.isSystemAdmin();
    
    // Set initial route
    this.currentRoute = this.router.url;
    
    this.menuService.getUserMenus(userId).subscribe(res => {
      this.menu = (res.sections || []).map(s => ({
        title: s.title,
        items: s.items
          .map(i => ({
            label: i.label,
            icon: i.icon,
            route: i.route,
            submenu: (i.submenu || [])
              .filter(si => {
                // Filter out Permissions menu for Company Admin
                if (!isSystemAdmin && si.route === '/auth/permissions') {
                  return false;
                }
                // Filter out Menus menu for Company Admin
                if (!isSystemAdmin && si.route === '/auth/menus') {
                  return false;
                }
                return true;
              })
              .map(ci => ({
                label: ci.label,
                icon: ci.icon,
                route: ci.route
              }))
          }))
          .filter(i => {
            // Filter out menu items for Company Admin
            if (!isSystemAdmin) {
              // Filter out Permissions route
              if (i.route === '/auth/permissions') {
                return false;
              }
              // Filter out Menus route
              if (i.route === '/auth/menus') {
                return false;
              }
              // For parent menus with submenu, check if they have valid children after filtering
              if (i.submenu && i.submenu.length === 0) {
                return false;
              }
            }
            return true;
          })
      })).filter(s => s.items.length > 0); // Remove empty sections
      
      // Update active menu items after menu is loaded
      this.updateActiveMenuItems();
    });
  }

  /**
   * Check if a menu item or submenu item is active based on current route
   */
  isMenuItemActive(item: MenuItem): boolean {
    if (!item.route) return false;
    
    // Exact match
    if (this.currentRoute === item.route) {
      return true;
    }
    
    // Check if current route starts with item route (for nested routes)
    // But avoid matching parent routes too broadly
    if (this.currentRoute.startsWith(item.route + '/') || 
        (item.route !== '/' && this.currentRoute.startsWith(item.route))) {
      return true;
    }
    
    // Check submenu items
    if (item.submenu) {
      return item.submenu.some(subItem => this.isMenuItemActive(subItem));
    }
    
    return false;
  }

  /**
   * Check if a submenu item is active
   */
  isSubmenuItemActive(subItemRoute: string): boolean {
    if (!subItemRoute) return false;
    return this.currentRoute === subItemRoute || 
           (subItemRoute !== '/' && this.currentRoute.startsWith(subItemRoute + '/'));
  }

  /**
   * Update active menu items and expand parent menus if needed
   */
  private updateActiveMenuItems(): void {
    this.menu.forEach(section => {
      section.items.forEach(item => {
        // Expand parent menu if any submenu item is active
        if (item.submenu && item.submenu.some(subItem => this.isSubmenuItemActive(subItem.route))) {
          item.expanded = true;
        }
      });
    });
  }

  onToggleSidebar() {
    this.toggleSidebar.emit();
  }

  onItemClick(event: Event, item: MenuItem) {
    // Always prevent default to avoid full page reload
    event.preventDefault();
    event.stopPropagation();

    // CRITICAL: If item has submenu, only toggle submenu - NEVER navigate to parent route
    if (item.submenu && item.submenu.length > 0) {
      // If sidebar is collapsed (icon menu), open tray at the side
      if (this.isCollapsed) {
        this.trayItem = this.trayItem === item ? null : item;
        return;
      }
      
      // For expanded sidebar, toggle submenu expansion only - no navigation
      item.expanded = !item.expanded;
      return;
    }

    // For items WITHOUT submenu, navigate directly
    if (item.route) {
      // If sidebar is collapsed (icon menu), navigate and close tray
      if (this.isCollapsed) {
        this.router.navigate([item.route]).then(() => {
          // Close tray on navigation
          this.trayItem = null;
        });
        return;
      }

      // For expanded sidebar, navigate directly
      this.router.navigate([item.route]).then(() => {
        // Close mobile sidebar on navigation
        if (this.mobileOpen) {
          this.toggleSidebar.emit();
        }
      });
    }
  }

  onItemEnter(item: MenuItem) { /* no-op for tray flow */ }
  onItemLeave() { /* no-op for tray flow */ }

  onSubmenuClick(event: Event, subItemRoute?: string) {
    // Prevent default to avoid full page reload
    event.preventDefault();
    event.stopPropagation();
    
    // Navigate if route is provided
    if (subItemRoute) {
      this.router.navigate([subItemRoute]).then(() => {
        // Close mobile sidebar when submenu item is clicked
        if (this.mobileOpen) {
          this.toggleSidebar.emit();
        }
        // Update active state
        this.updateActiveMenuItems();
      });
    } else {
      // Close mobile sidebar when submenu item is clicked
      if (this.mobileOpen) {
        this.toggleSidebar.emit();
      }
    }
  }

  onTrayItemClick(event: Event, subItemRoute?: string) {
    // Prevent default to avoid full page reload
    event.preventDefault();
    event.stopPropagation();
    
    // Navigate if route is provided
    if (subItemRoute) {
      this.router.navigate([subItemRoute]).then(() => {
        // Close tray when item is clicked
        this.trayItem = null;
        // Update active state
        this.updateActiveMenuItems();
      });
    } else {
      // Close tray when item is clicked
      this.trayItem = null;
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    if (!this.isCollapsed) return;
    const target = event.target as HTMLElement;
    if (!target.closest('.nav-link-container') && !target.closest('.submenu-tray')) {
      this.trayItem = null;
    }
  }
}