import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface PasswordPolicyDto {
  id?: string;
  minLength: number;
  maxLength: number;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireNumbers: boolean;
  requireSpecialCharacters: boolean;
  minSpecialCharacters?: number;
  maxConsecutiveCharacters?: number;
  preventCommonPasswords?: boolean;
  preventUserInfoInPassword?: boolean;
  passwordHistoryCount: number;
  maxFailedAttempts: number;
  lockoutDurationMinutes: number;
  passwordExpiryDays: number;
  requirePasswordChangeOnFirstLogin?: boolean;
  allowedSpecialCharacters?: string;
  disallowedPasswords?: string;
  isActive?: boolean;
  createdAtUtc?: string;
  lastModifiedAtUtc?: string;
  
  // Legacy field names for backward compatibility
  requireSpecial?: boolean;
  expiryDays?: number;
  lockoutThreshold?: number;
}

@Injectable({ providedIn: 'root' })
export class PasswordPolicyService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}/password-policy`;

  constructor(private http: HttpClient) {}

  getPolicy(): Observable<PasswordPolicyDto> {
    return this.http.get<any>(`${this.api}`).pipe(
      map((response) => {
        // Map backend PascalCase to frontend camelCase (handle both formats)
        return {
          id: response.id ?? response.Id,
          minLength: response.minLength ?? response.MinLength,
          maxLength: response.maxLength ?? response.MaxLength,
          requireUppercase: response.requireUppercase ?? response.RequireUppercase,
          requireLowercase: response.requireLowercase ?? response.RequireLowercase,
          requireNumbers: response.requireNumbers ?? response.RequireNumbers,
          requireSpecialCharacters: response.requireSpecialCharacters ?? response.RequireSpecialCharacters,
          minSpecialCharacters: response.minSpecialCharacters ?? response.MinSpecialCharacters ?? 1,
          maxConsecutiveCharacters: response.maxConsecutiveCharacters ?? response.MaxConsecutiveCharacters ?? 0,
          preventCommonPasswords: response.preventCommonPasswords ?? response.PreventCommonPasswords ?? false,
          preventUserInfoInPassword: response.preventUserInfoInPassword ?? response.PreventUserInfoInPassword ?? false,
          passwordHistoryCount: response.passwordHistoryCount ?? response.PasswordHistoryCount,
          maxFailedAttempts: response.maxFailedAttempts ?? response.MaxFailedAttempts,
          lockoutDurationMinutes: response.lockoutDurationMinutes ?? response.LockoutDurationMinutes,
          passwordExpiryDays: response.passwordExpiryDays ?? response.PasswordExpiryDays,
          requirePasswordChangeOnFirstLogin: response.requirePasswordChangeOnFirstLogin ?? response.RequirePasswordChangeOnFirstLogin ?? false,
          allowedSpecialCharacters: response.allowedSpecialCharacters ?? response.AllowedSpecialCharacters,
          disallowedPasswords: response.disallowedPasswords ?? response.DisallowedPasswords,
          isActive: response.isActive ?? response.IsActive ?? true,
          createdAtUtc: response.createdAtUtc ?? response.CreatedAtUtc,
          lastModifiedAtUtc: response.lastModifiedAtUtc ?? response.LastModifiedAtUtc,
          // Legacy mappings for backward compatibility
          requireSpecial: response.requireSpecialCharacters ?? response.RequireSpecialCharacters,
          expiryDays: response.passwordExpiryDays ?? response.PasswordExpiryDays,
          lockoutThreshold: response.maxFailedAttempts ?? response.MaxFailedAttempts
        } as PasswordPolicyDto;
      })
    );
  }

  updatePolicy(policy: PasswordPolicyDto): Observable<PasswordPolicyDto> {
    // Map frontend DTO to backend DTO format (camelCase - .NET Core default)
    const updateDto = {
      minLength: policy.minLength,
      maxLength: policy.maxLength,
      requireUppercase: policy.requireUppercase,
      requireLowercase: policy.requireLowercase,
      requireNumbers: policy.requireNumbers,
      requireSpecialCharacters: policy.requireSpecialCharacters ?? policy.requireSpecial ?? false,
      minSpecialCharacters: policy.minSpecialCharacters ?? 1,
      maxConsecutiveCharacters: policy.maxConsecutiveCharacters ?? 0,
      preventCommonPasswords: policy.preventCommonPasswords ?? false,
      preventUserInfoInPassword: policy.preventUserInfoInPassword ?? false,
      passwordHistoryCount: policy.passwordHistoryCount,
      maxFailedAttempts: policy.maxFailedAttempts ?? policy.lockoutThreshold ?? 5,
      lockoutDurationMinutes: policy.lockoutDurationMinutes,
      passwordExpiryDays: policy.passwordExpiryDays ?? policy.expiryDays ?? 0,
      requirePasswordChangeOnFirstLogin: policy.requirePasswordChangeOnFirstLogin ?? false,
      allowedSpecialCharacters: policy.allowedSpecialCharacters,
      disallowedPasswords: policy.disallowedPasswords,
      isActive: policy.isActive ?? true
    };
    
    return this.http.put<any>(`${this.api}`, updateDto).pipe(
      map((response) => {
        // Map backend response to frontend camelCase (handle both PascalCase and camelCase)
        return {
          id: response.id ?? response.Id,
          minLength: response.minLength ?? response.MinLength,
          maxLength: response.maxLength ?? response.MaxLength,
          requireUppercase: response.requireUppercase ?? response.RequireUppercase,
          requireLowercase: response.requireLowercase ?? response.RequireLowercase,
          requireNumbers: response.requireNumbers ?? response.RequireNumbers,
          requireSpecialCharacters: response.requireSpecialCharacters ?? response.RequireSpecialCharacters,
          minSpecialCharacters: response.minSpecialCharacters ?? response.MinSpecialCharacters ?? 1,
          maxConsecutiveCharacters: response.maxConsecutiveCharacters ?? response.MaxConsecutiveCharacters ?? 0,
          preventCommonPasswords: response.preventCommonPasswords ?? response.PreventCommonPasswords ?? false,
          preventUserInfoInPassword: response.preventUserInfoInPassword ?? response.PreventUserInfoInPassword ?? false,
          passwordHistoryCount: response.passwordHistoryCount ?? response.PasswordHistoryCount,
          maxFailedAttempts: response.maxFailedAttempts ?? response.MaxFailedAttempts,
          lockoutDurationMinutes: response.lockoutDurationMinutes ?? response.LockoutDurationMinutes,
          passwordExpiryDays: response.passwordExpiryDays ?? response.PasswordExpiryDays,
          requirePasswordChangeOnFirstLogin: response.requirePasswordChangeOnFirstLogin ?? response.RequirePasswordChangeOnFirstLogin ?? false,
          allowedSpecialCharacters: response.allowedSpecialCharacters ?? response.AllowedSpecialCharacters,
          disallowedPasswords: response.disallowedPasswords ?? response.DisallowedPasswords,
          isActive: response.isActive ?? response.IsActive ?? true,
          createdAtUtc: response.createdAtUtc ?? response.CreatedAtUtc,
          lastModifiedAtUtc: response.lastModifiedAtUtc ?? response.LastModifiedAtUtc,
          // Legacy mappings
          requireSpecial: response.requireSpecialCharacters ?? response.RequireSpecialCharacters,
          expiryDays: response.passwordExpiryDays ?? response.PasswordExpiryDays,
          lockoutThreshold: response.maxFailedAttempts ?? response.MaxFailedAttempts
        } as PasswordPolicyDto;
      })
    );
  }

  validate(password: string, userId?: string): Observable<any> {
    return this.http.post<any>(`${this.api}/validate`, { password, userId });
  }

  checkComplexity(password: string): Observable<{ isValid: boolean }> {
    return this.http.post<{ isValid: boolean }>(`${this.api}/check-complexity`, { password });
  }

  checkCommon(password: string): Observable<{ isCommon: boolean }> {
    return this.http.post<{ isCommon: boolean }>(`${this.api}/check-common`, { password });
  }

  checkUserInfo(userId: string, password: string): Observable<{ containsUserInfo: boolean }> {
    return this.http.post<{ containsUserInfo: boolean }>(`${this.api}/check-user-info`, { userId, password });
  }
}
