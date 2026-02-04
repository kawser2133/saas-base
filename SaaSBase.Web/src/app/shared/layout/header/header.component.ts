import { Component, EventEmitter, Input, Output, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { clearAuthStorage } from '../../../core/auth/auth.utils';
import { UserProfileService, UserProfile } from '../../../core/services/user-profile.service';
import { AuthService } from '../../../core/services/auth.service';
import { BrandLogoComponent } from '../../components/brand-logo/brand-logo.component';

interface User {
  name: string;
  role: string;
  email?: string;
  avatar?: string;
}

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, FormsModule, BrandLogoComponent],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  @Input() user: User | null = null;
  @Output() toggleSidebar = new EventEmitter<void>();
  
  searchQuery = '';
  notificationCount = 3;
  isDarkMode = false;
  showUserMenu = false;
  showNotifications = false;
  showSearchSuggestions = false;
  currentUserName: string = '';
  currentUserAvatar: string = '';
  currentUserEmail: string = '';

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event) {
    const target = event.target as HTMLElement;
    if (!target.closest('.user-menu-container')) {
      this.showUserMenu = false;
    }
    if (!target.closest('.notification-container')) {
      this.showNotifications = false;
    }
    if (!target.closest('.search-container')) {
      this.showSearchSuggestions = false;
    }
  }

  @HostListener('document:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent) {
    // Ctrl/Cmd + K for search focus
    if ((event.ctrlKey || event.metaKey) && event.key === 'k') {
      event.preventDefault();
      const searchInput = document.querySelector('.search-input') as HTMLInputElement;
      if (searchInput) {
        searchInput.focus();
      }
    }
    
    // Escape key to close dropdowns
    if (event.key === 'Escape') {
      this.showUserMenu = false;
      this.showNotifications = false;
      this.showSearchSuggestions = false;
    }
  }

  onToggleSidebar() {
    this.toggleSidebar.emit();
  }

  onSearchFocus() {
    this.showSearchSuggestions = true;
  }

  onSearchBlur() {
    // Delay hiding to allow clicking on suggestions
    setTimeout(() => {
      this.showSearchSuggestions = false;
    }, 200);
  }

  toggleNotifications() {
    this.showNotifications = !this.showNotifications;
    this.showUserMenu = false; // Close user menu if open
  }

  toggleTheme() {
    this.isDarkMode = !this.isDarkMode;
    document.documentElement.setAttribute('data-theme', this.isDarkMode ? 'dark' : 'light');
    localStorage.setItem('theme', this.isDarkMode ? 'dark' : 'light');
  }

  toggleUserMenu() {
    this.showUserMenu = !this.showUserMenu;
    this.showNotifications = false; // Close notifications if open
  }

  constructor(
    private router: Router, 
    private userProfileService: UserProfileService,
    private authService: AuthService
  ) {}

  ngOnInit() {
    // Load saved theme
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme) {
      this.isDarkMode = savedTheme === 'dark';
      document.documentElement.setAttribute('data-theme', savedTheme);
    }

    // Listen for theme changes from settings
    window.addEventListener('theme-change', (event: any) => {
      const newTheme = event.detail.theme;
      this.isDarkMode = newTheme === 'dark';
      document.documentElement.setAttribute('data-theme', newTheme);
      localStorage.setItem('theme', newTheme);
    });

    // Listen for avatar changes from profile
    window.addEventListener('avatar-updated', (event: any) => {
      const { avatarUrl, avatar } = event.detail;
      this.currentUserAvatar = avatarUrl || avatar || '';
    });

    // Load current user name from profile service using userId from storage
    const userId = localStorage.getItem('userId');
    if (userId) {
      this.userProfileService.getProfile(userId).subscribe({
        next: (profile: UserProfile) => {
          const fullName = `${profile.firstName ?? ''} ${profile.lastName ?? ''}`.trim();
          this.currentUserName = fullName || profile.email || 'User';
          this.currentUserAvatar = profile.avatarUrl || profile.avatar || '';
          this.currentUserEmail = profile.email || '';
        },
        error: () => {
          this.currentUserName = 'User';
          this.currentUserAvatar = '';
          this.currentUserEmail = '';
        }
      });
    } else if (this.user?.name) {
      this.currentUserName = this.user.name;
    } else {
      this.currentUserName = 'User';
    }
  }

  onSignOut() {
    // Call logout API to mark session as inactive in backend
    this.authService.logout().subscribe({
      next: () => {
        clearAuthStorage();
        this.showUserMenu = false;
        this.router.navigate(['/login'], { replaceUrl: true });
      },
      error: () => {
        // Even if API call fails, clear local storage and navigate to login
        clearAuthStorage();
        this.showUserMenu = false;
        this.router.navigate(['/login'], { replaceUrl: true });
      }
    });
  }

  goToProfile() {
    this.showUserMenu = false;
    this.router.navigate(['/profile']);
  }

  goToSettings() {
    this.showUserMenu = false;
    this.router.navigate(['/settings']);
  }
}