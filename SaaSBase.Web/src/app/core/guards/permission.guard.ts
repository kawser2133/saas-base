import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of, switchMap } from 'rxjs';
import { AuthorizationService } from '../services/authorization.service';

export const permissionGuard = (requiredCodes: string[]): CanActivateFn => () => {
  const router = inject(Router);
  const authz = inject(AuthorizationService);
  const userId = localStorage.getItem('userId');

  // Asynchronous: wait for permission codes to load, then evaluate
  return authz.loadUserPermissionCodes(userId).pipe(
    switchMap(() => {
      if (requiredCodes.length === 0 || authz.requirePermissions(requiredCodes)) {
        return of(true);
      }
      router.navigate(['/dashboard']);
      return of(false);
    })
  );
};


