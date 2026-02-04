import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthorizationService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;
  private permissionCodes$ = new BehaviorSubject<Set<string>>(new Set<string>());
  private loadedForUserId: string | null = null;

  constructor(private http: HttpClient) {}

  loadUserPermissionCodes(userId: string | null): Observable<string[]> {
    if (!userId) return of([]);
    if (this.loadedForUserId === userId && this.permissionCodes$.value.size > 0) {
      return of(Array.from(this.permissionCodes$.value));
    }
    return this.http
      .get<string[]>(`${this.api}/permissions/user/${userId}/codes`)
      .pipe(
        tap(codes => {
          this.loadedForUserId = userId;
          this.permissionCodes$.next(new Set<string>(codes));
        })
      );
  }

  hasPermission(code: string): boolean {
    return this.permissionCodes$.value.has(code);
  }

  requirePermissions(codes: string[]): boolean {
    const set = this.permissionCodes$.value;
    return codes.every(c => set.has(c));
  }
}


