import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router, UrlTree, Route, UrlSegment } from '@angular/router';
import { isAuthenticated } from '../auth/auth.utils';

export const loginGuard: CanActivateFn = (): boolean | UrlTree => {
  const router = inject(Router);
  return isAuthenticated() ? router.parseUrl('/dashboard') : true;
};

export const loginMatchGuard: CanMatchFn = (
  route: Route,
  segments: UrlSegment[]
): boolean | UrlTree => {
  const router = inject(Router);
  return isAuthenticated() ? router.parseUrl('/dashboard') : true;
};


