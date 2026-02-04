import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router, UrlSegment, Route, UrlTree } from '@angular/router';
import { isAuthenticated } from '../auth/auth.utils';

export const authGuard: CanActivateFn = (): boolean | UrlTree => {
  const router = inject(Router);
  if (isAuthenticated()) {
    return true;
  }
  // Redirect to login if not authenticated
  return router.parseUrl('/login');
};

export const authMatchGuard: CanMatchFn = (
  route: Route,
  segments: UrlSegment[]
): boolean | UrlTree => {
  const router = inject(Router);
  if (isAuthenticated()) {
    return true;
  }
  // Redirect to login if not authenticated
  return router.parseUrl('/login');
};


