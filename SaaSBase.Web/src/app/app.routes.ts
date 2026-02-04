import { Routes } from '@angular/router';
import { authGuard, authMatchGuard } from './core/guards/auth.guard';
import { loginGuard, loginMatchGuard } from './core/guards/login.guard';
import { roleGuard } from './core/guards/role.guard';
import { permissionGuard } from './core/guards/permission.guard';

export const routes: Routes = [
  // Landing page (home) - accessible to all, redirects authenticated users
  {
    path: '',
    loadComponent: () => import('./features/landing/landing.component').then(m => m.LandingComponent)
  },

  // Organization setup wizard
  {
    path: 'setup',
    loadComponent: () => import('./features/setup/organization-setup.component').then(m => m.OrganizationSetupComponent)
  },

  // Public route (blocked for authenticated users)
  { 
    path: 'login', 
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent),
    canMatch: [loginMatchGuard],
    canActivate: [loginGuard]
  },

  // Public route: email verification
  {
    path: 'verify-email',
    loadComponent: () => import('./features/auth/verify-email/verify-email.component').then(m => m.VerifyEmailComponent)
  },

  // Public route: forgot password
  {
    path: 'forgot-password',
    loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },

  // Public route: password reset
  {
    path: 'reset-password',
    loadComponent: () => import('./features/auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent)
  },

  // Dashboard - default authenticated route
  {
    path: 'dashboard',
    loadComponent: () => import('./shared/layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
    canMatch: [authMatchGuard],
    canActivate: [authGuard],
    children: [
      { path: '', loadComponent: () => import('./features/dashboard/dashboard-home/dashboard-home.component').then(m => m.DashboardHomeComponent) },
    ]
  },

  // System Dashboard - for System Administrator
  {
    path: 'system',
    loadComponent: () => import('./shared/layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
    canMatch: [authMatchGuard],
    canActivate: [authGuard, roleGuard(['System Administrator'])],
    children: [
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/system-dashboard/system-dashboard.component').then(m => m.SystemDashboardComponent) },
      { path: 'organizations', loadComponent: () => import('./features/organization/organization.component').then(m => m.OrganizationComponent), canActivate: [permissionGuard(['System.Organizations.Read'])] },
    ]
  },

  // Other authenticated routes with layout
  {
    path: '',
    loadComponent: () => import('./shared/layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
    canMatch: [authMatchGuard],
    canActivate: [authGuard],
    children: [
      // Auth module pages inside layout - Admin only
      { path: 'auth/users', loadComponent: () => import('./features/auth/users/users.component').then(m => m.UsersComponent), canActivate: [permissionGuard(['Users.Read'])] },

      // Roles & Permissions - Admin only
      { path: 'auth/roles', loadComponent: () => import('./features/auth/roles/roles.component').then(m => m.RolesComponent), canActivate: [permissionGuard(['Roles.Read'])] },
      { path: 'auth/permissions', loadComponent: () => import('./features/auth/permissions/permissions.component').then(m => m.PermissionsComponent), canActivate: [authGuard, roleGuard(['System Administrator'])] },
      { path: 'auth/menus', loadComponent: () => import('./features/auth/menus/menus.component').then(m => m.MenusComponent), canActivate: [authGuard, roleGuard(['System Administrator'])] },

      // Security: Sessions, MFA, Password Policy
      { path: 'auth/sessions', loadComponent: () => import('./features/auth/sessions/sessions.component').then(m => m.SessionsComponent), canActivate: [permissionGuard(['Sessions.Read'])] },
      { path: 'auth/mfa', loadComponent: () => import('./features/auth/mfa/mfa.component').then(m => m.MfaComponent), canActivate: [permissionGuard(['Mfa.Read'])] },
      { path: 'auth/password-policy', loadComponent: () => import('./features/auth/password-policy/password-policy.component').then(m => m.PasswordPolicyComponent), canActivate: [permissionGuard(['PasswordPolicy.Read'])] },

      // Profile
      { path: 'profile', loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent) },

      // Organization Management - Single unified tabbed view (System Admin sees all, Company Admin sees own)
      { path: 'organizations', loadComponent: () => import('./features/organization/organization.component').then(m => m.OrganizationComponent), canActivate: [permissionGuard(['Organizations.Read'])] },

      // Master Data Management
      { path: 'master-data/departments', loadComponent: () => import('./features/master-data/departments/departments.component').then(m => m.DepartmentsComponent), canActivate: [permissionGuard(['Departments.Read'])] },
      { path: 'master-data/positions', loadComponent: () => import('./features/master-data/positions/positions.component').then(m => m.PositionsComponent), canActivate: [permissionGuard(['Positions.Read'])] },

      // Settings
      { path: 'settings', loadComponent: () => import('./features/settings/settings.component').then(m => m.SettingsComponent) },
    ]
  },

  // Fallback - redirect to landing page
  { path: '**', redirectTo: '' }
];
