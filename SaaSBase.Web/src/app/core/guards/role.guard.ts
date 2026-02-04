import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const roleGuard = (required: string[]): CanActivateFn => () => {
  const router = inject(Router);
  const roles = (localStorage.getItem('roles') || '').split(',').filter(Boolean);
  if (required.some(r => roles.includes(r))) return true;
  router.navigate(['/dashboard']);
  return false;
};


