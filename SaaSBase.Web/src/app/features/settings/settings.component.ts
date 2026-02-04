import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { UserProfileService } from '../../core/services/user-profile.service';
import { PasswordPolicyService } from '../../core/services/password-policy.service';
import { NotificationService } from '../../shared/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { SessionsService, UserSessionDto } from '../../core/services/sessions.service';
import { MfaService, UserMfaSettingsDto, MfaSettingsDto } from '../../core/services/mfa.service';
import { Subject, debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs';
import { clearAuthStorage, decodeJwt, getAccessToken } from '../../core/auth/auth.utils';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent implements OnInit, OnDestroy {
  // Utility references
  Math = Math;

  prefs: { theme: 'light' | 'dark'; language: string; notifications: { email: boolean; push: boolean; sms: boolean } } | null = null;
  saving = false;
  pw = { currentPassword: '', newPassword: '', confirmPassword: '' };
  changingPw = false;
  showCurrentPassword = false;
  showNewPassword = false;
  showConfirmPassword = false;
  passwordValidationErrors: string[] = [];
  isValidatingPassword = false;
  passwordPolicy: any = null;
  passwordRequirements: string[] = [];
  
  // Tab management
  activeTab: 'appearance' | 'security' | 'sessions' | 'mfa' = 'appearance';
  
  // Sessions tab data
  sessions: UserSessionDto[] = [];
  isLoadingSessions = false;
  sessionsPage = 1;
  sessionsPageSize = 10;
  sessionsTotalCount = 0;
  
  // MFA tab data
  mfaSettings: UserMfaSettingsDto[] = [];
  isLoadingMfa = false;
  mfaPage = 1;
  mfaPageSize = 10;
  mfaTotalCount = 0;
  currentUserMfaSettings: MfaSettingsDto | null = null;
  
  // For MFA setup/verification
  smsCode = '';
  emailCode = '';
  selectedDefaultMfaMethod: string = '';
  
  private destroy$ = new Subject<void>();
  private passwordSubject$ = new Subject<string>();

  constructor(
    private userProfileService: UserProfileService,
    private passwordPolicyService: PasswordPolicyService,
    private notifications: NotificationService,
    private auth: AuthService,
    private sessionsService: SessionsService,
    private mfaService: MfaService
  ) {}

  ngOnInit(): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    
    // Load password policy to display requirements
    this.passwordPolicyService.getPolicy().subscribe({
      next: (policy) => {
        this.passwordPolicy = policy;
        this.buildPasswordRequirements(policy);
      },
      error: () => {
        // Fallback to default requirements if policy fails to load
        this.buildPasswordRequirements(null);
      }
    });
    
    // Load user profile with integrated notification preferences
    this.userProfileService.getProfile(userId).subscribe({
      next: (profile) => {
        const defaults = { theme: 'light' as 'light' | 'dark', language: 'en', notifications: { email: true, push: true, sms: false } };
        
        // Map backend data to frontend structure using integrated response
        this.prefs = {
          theme: (profile.theme as 'light' | 'dark') || defaults.theme,
          language: profile.language || defaults.language,
          notifications: {
            email: profile.notificationPreferences?.emailNotifications ?? defaults.notifications.email,
            push: profile.notificationPreferences?.pushNotifications ?? defaults.notifications.push,
            sms: profile.notificationPreferences?.smsNotifications ?? defaults.notifications.sms
          }
        };
      },
      error: () => {
        // Fallback to defaults if profile fails to load
        const defaults = { theme: 'light' as 'light' | 'dark', language: 'en', notifications: { email: true, push: true, sms: false } };
        this.prefs = defaults;
      }
    });

    // Setup real-time password validation with debounce
    this.passwordSubject$.pipe(
      debounceTime(500), // Wait 500ms after user stops typing
      distinctUntilChanged(), // Only validate if password changed
      switchMap(password => {
        this.isValidatingPassword = true;
        this.passwordValidationErrors = [];
        const userId = localStorage.getItem('userId');
        return this.passwordPolicyService.validate(password, userId || undefined);
      }),
      takeUntil(this.destroy$)
    ).subscribe({
      next: (result) => {
        this.isValidatingPassword = false;
        if (!result.isValid && result.errors && result.errors.length > 0) {
          this.passwordValidationErrors = result.errors;
        } else {
          this.passwordValidationErrors = [];
        }
      },
      error: (err) => {
        this.isValidatingPassword = false;
        // Don't show error notification for validation API failures
        console.error('Password validation error:', err);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // Tab management
  setActiveTab(tab: 'appearance' | 'security' | 'sessions' | 'mfa'): void {
    this.activeTab = tab;
    if (tab === 'sessions') {
      this.loadUserSessions();
    } else if (tab === 'mfa') {
      this.loadMfaSettings();
    }
  }

  // Sessions management
  loadUserSessions(): void {
    this.isLoadingSessions = true;
    // Use my-sessions endpoint which always returns current user's sessions
    this.sessionsService.getMySessions(this.sessionsPage, this.sessionsPageSize).subscribe({
      next: (response) => {
        this.sessions = response.items || [];
        this.sessionsTotalCount = response.totalCount || 0;
        this.isLoadingSessions = false;
      },
      error: () => {
        this.notifications.error('Error', 'Failed to load sessions');
        this.isLoadingSessions = false;
      }
    });
  }

  onSessionsPageChange(page: number): void {
    if (page < 1) return;
    this.sessionsPage = page;
    this.loadUserSessions();
  }

  onSessionsPageSizeChange(newSize: number): void {
    this.sessionsPageSize = newSize;
    this.sessionsPage = 1;
    this.loadUserSessions();
  }

  get sessionsTotalPages(): number {
    return Math.max(1, Math.ceil((this.sessionsTotalCount || 0) / (this.sessionsPageSize || 10)));
  }

  // MFA management - Get current user's own MFA settings
  loadMfaSettings(): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    
    this.isLoadingMfa = true;
    this.mfaService.getSettings(userId).subscribe({
      next: (settings) => {
        this.currentUserMfaSettings = settings;
        this.mfaSettings = settings.enabledMethods?.map((method, index) => ({
          id: `mfa-${index}`,
          userId: userId,
          userEmail: '', // Not needed for own settings
          mfaType: method,
          isActive: true,
          isDefault: method === settings.defaultMethod,
          lastUsedAt: undefined,
          createdAtUtc: new Date().toISOString()
        })) || [];
        this.mfaTotalCount = this.mfaSettings.length;
        // Set the selected default method, or first filtered enabled method if no default is set
        const filteredMethods = this.getFilteredMfaMethods();
        if (settings.defaultMethod && filteredMethods.includes(settings.defaultMethod)) {
          this.selectedDefaultMfaMethod = settings.defaultMethod;
        } else {
          this.selectedDefaultMfaMethod = filteredMethods[0] || '';
        }
        this.isLoadingMfa = false;
      },
      error: () => {
        this.notifications.error('Error', 'Failed to load MFA settings');
        this.isLoadingMfa = false;
      }
    });
  }

  disableMfa(method: 'SMS'|'EMAIL'): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    this.mfaService.disable(userId, method).subscribe({
      next: () => {
        this.notifications.success('Disabled', `${method} disabled successfully`);
        this.loadMfaSettings();
      },
      error: () => this.notifications.error('Error', `Failed to disable ${method}`)
    });
  }

  sendMfaCode(method: 'SMS'|'EMAIL'): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    this.mfaService.sendCode(userId, method).subscribe({
      next: (res) => {
        if (res?.success === true) {
          this.notifications.success('Sent', `${method} code sent successfully`);
        } else {
          this.notifications.error('Error', `Failed to send ${method} code`);
        }
      },
      error: (error) => {
        const errorMessage = error?.error?.detail || error?.error?.message || `Failed to send ${method} code`;
        this.notifications.error('Error', errorMessage);
      }
    });
  }

  verifyMfaCode(method: 'SMS'|'EMAIL'): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    const code = method === 'SMS' ? this.smsCode : this.emailCode;
    if (!code) {
      this.notifications.error('Error', 'Please enter the code');
      return;
    }
    this.mfaService.enable(userId, method, code).subscribe({
      next: (r) => {
        if (r.success) {
          this.notifications.success('Enabled', `${method} enabled successfully`);
          this.smsCode = '';
          this.emailCode = '';
          this.loadMfaSettings();
        } else {
          this.notifications.error('Error', 'Invalid code');
        }
      },
      error: () => this.notifications.error('Error', `Failed to enable ${method}`)
    });
  }

  getFilteredMfaMethods(): string[] {
    if (!this.currentUserMfaSettings?.enabledMethods) return [];
    // Only show SMS, EMAIL, and BACKUPCODE (exclude TOTP and others)
    return this.currentUserMfaSettings.enabledMethods.filter(m => 
      m.toUpperCase() === 'SMS' || m.toUpperCase() === 'EMAIL' || m.toUpperCase() === 'BACKUPCODE'
    );
  }

  setDefaultMfaMethod(): void {
    const userId = localStorage.getItem('userId');
    if (!userId || !this.selectedDefaultMfaMethod) return;
    
    this.mfaService.setDefault(userId, this.selectedDefaultMfaMethod).subscribe({
      next: (res) => {
        if (res.success) {
          this.notifications.success('Updated', 'Default MFA method updated successfully');
          this.loadMfaSettings(); // Refresh to show updated default
        } else {
          this.notifications.error('Error', 'Failed to set default MFA method');
        }
      },
      error: () => this.notifications.error('Error', 'Failed to set default MFA method')
    });
  }

  // Backup Codes Modal
  showBackupCodesModal = false;
  backupCodes: string[] = [];
  backupCodesCopied = false;

  generateBackupCodes(): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    this.mfaService.generateBackupCodes(userId).subscribe({
      next: (r) => {
        this.backupCodes = r.codes || [];
        this.showBackupCodesModal = true;
        this.backupCodesCopied = false;
        this.loadMfaSettings(); // Refresh to show backup codes are enabled
      },
      error: () => this.notifications.error('Error', 'Failed to generate backup codes')
    });
  }

  closeBackupCodesModal(): void {
    this.showBackupCodesModal = false;
    this.backupCodes = [];
    this.backupCodesCopied = false;
  }

  copyBackupCodes(): void {
    if (this.backupCodes.length === 0) return;
    const codesText = this.backupCodes.join('\n');
    navigator.clipboard.writeText(codesText).then(() => {
      this.backupCodesCopied = true;
      this.notifications.success('Copied', 'Backup codes copied to clipboard');
      setTimeout(() => {
        this.backupCodesCopied = false;
      }, 2000);
    }).catch(() => {
      this.notifications.error('Error', 'Failed to copy codes');
    });
  }

  downloadBackupCodes(): void {
    if (this.backupCodes.length === 0) return;
    const codesText = this.backupCodes.join('\n');
    const blob = new Blob([codesText], { type: 'text/plain' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `backup-codes-${new Date().toISOString().split('T')[0]}.txt`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
    this.notifications.success('Downloaded', 'Backup codes downloaded successfully');
  }

  copySingleCode(code: string): void {
    navigator.clipboard.writeText(code).then(() => {
      this.notifications.success('Copied', 'Code copied to clipboard');
    }).catch(() => {
      this.notifications.error('Error', 'Failed to copy code');
    });
  }

  onMfaPageChange(page: number): void {
    if (page < 1) return;
    this.mfaPage = page;
    this.loadMfaSettings();
  }

  onMfaPageSizeChange(newSize: number): void {
    this.mfaPageSize = newSize;
    this.mfaPage = 1;
    this.loadMfaSettings();
  }

  get mfaTotalPages(): number {
    return Math.max(1, Math.ceil((this.mfaTotalCount || 0) / (this.mfaPageSize || 10)));
  }

  revokeSession(session: UserSessionDto): void {
    const currentSessionId = this.getCurrentSessionId();
    const isCurrentSession = currentSessionId && session.sessionId === currentSessionId;

    this.sessionsService.revokeSession(session.sessionId).subscribe({
      next: () => {
        this.notifications.success('Success', 'Session revoked');
        
        // If current session is revoked, logout
        if (isCurrentSession) {
          this.notifications.warning('Session Revoked', 'Your session has been revoked. Please login again.');
          setTimeout(() => this.logout(), 1000);
        } else {
          // If current page becomes empty after revoke, go to previous page
          if (this.sessions.length === 1 && this.sessionsPage > 1) {
            this.sessionsPage--;
          }
          this.loadUserSessions();
        }
      },
      error: () => this.notifications.error('Error', 'Failed to revoke session')
    });
  }

  getCurrentSessionId(): string | null {
    const token = getAccessToken();
    if (!token) return null;
    const decoded = decodeJwt(token);
    return decoded?.sessionId || decoded?.sid || null;
  }

  private logout(): void {
    // Call logout API to mark session as inactive in backend
    this.auth.logout().subscribe({
      next: () => {
        clearAuthStorage();
        window.location.href = '/login';
      },
      error: () => {
        // Even if API call fails, clear local storage and navigate to login
        clearAuthStorage();
        window.location.href = '/login';
      }
    });
  }

  onNewPasswordChange(): void {
    const password = this.pw.newPassword;
    if (password && password.length >= 8) {
      this.passwordSubject$.next(password);
    } else {
      this.passwordValidationErrors = [];
      this.isValidatingPassword = false;
    }
  }

  buildPasswordRequirements(policy: any): void {
    const requirements: string[] = [];
    
    if (policy) {
      // Minimum length
      if (policy.minLength) {
        requirements.push(`At least ${policy.minLength} characters long`);
      }
      
      // Maximum length
      if (policy.maxLength && policy.maxLength < 128) {
        requirements.push(`No more than ${policy.maxLength} characters`);
      }
      
      // Uppercase
      if (policy.requireUppercase) {
        requirements.push('Contains at least one uppercase letter');
      }
      
      // Lowercase
      if (policy.requireLowercase) {
        requirements.push('Contains at least one lowercase letter');
      }
      
      // Numbers
      if (policy.requireNumbers) {
        requirements.push('Includes at least one number');
      }
      
      // Special characters
      if (policy.requireSpecialCharacters) {
        const minSpecial = policy.minSpecialCharacters || 1;
        requirements.push(`Contains at least ${minSpecial} special character${minSpecial > 1 ? 's' : ''}`);
      }
      
      // Consecutive characters
      if (policy.maxConsecutiveCharacters && policy.maxConsecutiveCharacters > 0) {
        requirements.push(`No more than ${policy.maxConsecutiveCharacters} consecutive identical characters`);
      }
      
      // Common passwords
      if (policy.preventCommonPasswords) {
        requirements.push('Cannot be a common password');
      }
    } else {
      // Default requirements if policy not loaded
      requirements.push('At least 8 characters long');
      requirements.push('Contains uppercase and lowercase letters');
      requirements.push('Includes at least one number');
      requirements.push('Contains at least one special character');
    }
    
    this.passwordRequirements = requirements;
  }

  save(): void {
    const userId = localStorage.getItem('userId');
    if (!userId || !this.prefs) return;
    this.saving = true;
    
    // Check if theme changed and trigger theme toggle if needed
    const currentTheme = localStorage.getItem('theme') || 'light';
    const newTheme = this.prefs.theme;
    if (currentTheme !== newTheme) {
      // Trigger theme toggle by dispatching a custom event
      window.dispatchEvent(new CustomEvent('theme-change', { detail: { theme: newTheme } }));
    }
    
    // Map preferences to backend structure
    const profilePayload = {
      firstName: '', // Will be loaded from current profile
      lastName: '', // Will be loaded from current profile
      email: '', // Will be loaded from current profile
      language: this.prefs.language,
      theme: this.prefs.theme,
    };

    // Load current profile and update with preferences
    this.userProfileService.getProfile(userId).subscribe({
      next: (currentProfile) => {
        const payload = {
          ...currentProfile,
          language: this.prefs!.language,
          theme: this.prefs!.theme,
          // Map notification preferences separately
        };
        
        // Update profile with language preference
        this.userProfileService.updateProfile(userId, payload).subscribe({
          next: () => {
            // Update notification preferences separately
            this.updateNotificationPreferences(userId);
          },
          error: () => {
            this.notifications.error('Failed', 'Could not update preferences');
            this.saving = false;
          }
        });
      },
      error: () => {
        this.notifications.error('Failed', 'Could not load current profile');
        this.saving = false;
      }
    });
  }

  private updateNotificationPreferences(userId: string): void {
    if (!this.prefs) return;
    
    // Map frontend notification structure to backend structure
    const notificationPayload = {
      emailNotifications: this.prefs.notifications.email,
      smsNotifications: this.prefs.notifications.sms,
      pushNotifications: this.prefs.notifications.push,
      marketingEmails: false, // Default value
      securityAlerts: true, // Default value
      systemUpdates: true, // Default value
      preferredNotificationTime: "09:00" // Default value
    };

    // Update notification preferences
    this.userProfileService.updateNotificationPreferences(userId, notificationPayload).subscribe({
      next: () => {
        this.notifications.success('Saved', 'Preferences updated successfully');
        this.saving = false;
      },
      error: () => {
        this.notifications.error('Failed', 'Could not update notification preferences');
        this.saving = false;
      }
    });
  }

  onThemeChange(): void {
    // Apply theme change immediately when user selects a different theme
    if (this.prefs?.theme) {
      const currentTheme = localStorage.getItem('theme') || 'light';
      if (currentTheme !== this.prefs.theme) {
        window.dispatchEvent(new CustomEvent('theme-change', { detail: { theme: this.prefs.theme } }));
      }
    }
  }

  changePassword(): void {
    if (this.changingPw) return;
    if (!this.pw.currentPassword || !this.pw.newPassword || !this.pw.confirmPassword) {
      this.notifications.error('Validation', 'All password fields are required');
      return;
    }
    if (this.pw.newPassword !== this.pw.confirmPassword) {
      this.notifications.error('Validation', 'New password and confirm password do not match');
      return;
    }
    // Check if password has validation errors
    if (this.passwordValidationErrors.length > 0) {
      this.notifications.error('Validation', 'Password does not meet the policy requirements. Please check the errors below.');
      return;
    }
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    this.changingPw = true;
    this.auth.changePassword({ userId, currentPassword: this.pw.currentPassword, newPassword: this.pw.newPassword }).subscribe({
      next: () => {
        this.notifications.success('Password Updated', 'Your password has been changed successfully.');
        this.pw = { currentPassword: '', newPassword: '', confirmPassword: '' };
        this.passwordValidationErrors = []; // Clear validation errors on success
        this.changingPw = false; // Reset button state
      },
      error: (err) => {
        // Extract error message from various possible locations
        // Backend uses ProblemDetails middleware which returns errors in different formats
        // ProblemDetails format: { type, title, status, detail, instance }
        // ArgumentException message goes in 'detail' field
        const errorObj = err?.error || {};
        
        let errorMessage = 'Unable to change password';
        
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
        
        this.notifications.error('Update Failed', errorMessage);
        this.changingPw = false; // Reset button state on error - IMPORTANT: Enable button again
      }
    });
  }
}


