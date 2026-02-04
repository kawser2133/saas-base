import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class UserContextService {
  /**
   * Check if current user is System Administrator
   */
  isSystemAdmin(): boolean {
    const roles = (localStorage.getItem('roles') || '').split(',').filter(Boolean);
    return roles.includes('System Administrator');
  }

  /**
   * Get current user's organization ID
   */
  getCurrentOrganizationId(): string | null {
    return localStorage.getItem('organizationId');
  }

  /**
   * Get current user ID
   */
  getCurrentUserId(): string | null {
    return localStorage.getItem('userId');
  }

  /**
   * Check if user can edit data from another organization
   * System Admin can only view, not edit other organizations' data
   */
  canEditOrganizationData(dataOrganizationId: string): boolean {
    const currentOrgId = this.getCurrentOrganizationId();
    if (!currentOrgId) return false;
    
    // If System Admin, can only edit their own organization's data
    if (this.isSystemAdmin()) {
      return currentOrgId === dataOrganizationId;
    }
    
    // Regular users can only edit their own organization's data
    return currentOrgId === dataOrganizationId;
  }
}
