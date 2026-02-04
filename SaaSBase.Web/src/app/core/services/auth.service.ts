import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;

  constructor(private http: HttpClient) { }

  login(payload: { email: string; password: string }): Observable<any> {
    return this.http.post(`${this.api}/auth/login`, payload);
  }

  sendMfaCodeLogin(payload: { userId: string; mfaType: string }): Observable<any> {
    return this.http.post(`${this.api}/auth/send-mfa-code-login`, payload);
  }

  verifyMfaLogin(payload: { userId: string; mfaType: string; code: string }): Observable<any> {
    return this.http.post(`${this.api}/auth/verify-mfa-login`, payload);
  }

  refreshToken(payload: { refreshToken: string }): Observable<any> {
    return this.http.post(`${this.api}/auth/refresh`, payload);
  }

  forgotPassword(payload: { email: string }): Observable<any> {
    return this.http.post(`${this.api}/auth/forgot-password`, payload);
  }

  resetPassword(payload: { token: string; newPassword: string }): Observable<any> {
    return this.http.post(`${this.api}/auth/reset-password`, payload);
  }

  verifyEmail(token: string): Observable<any> {
    return this.http.post(`${this.api}/auth/verify-email`, { token });
  }

  changePassword(payload: { userId: string; currentPassword: string; newPassword: string }): Observable<any> {
    return this.http.post(`${this.api}/auth/change-password`, payload);
  }

  getUsers(params?: { search?: string; isActive?: boolean; page?: number; pageSize?: number }): Observable<any> {
    const options: any = {};
    if (params) {
      options.params = {} as any;
      if (params.search) (options.params as any).search = params.search;
      if (typeof params.isActive === 'boolean') (options.params as any).isActive = params.isActive;
      if (params.page) (options.params as any).page = params.page;
      if (params.pageSize) (options.params as any).pageSize = params.pageSize;
    }
    return this.http.get(`${this.api}/users`, options);
  }

  createUser(payload: { email: string; password: string; fullName: string; firstName?: string; lastName?: string; phoneNumber?: string; isActive?: boolean; }): Observable<any> {
    return this.http.post(`${this.api}/users`, payload);
  }

  updateUser(id: string, payload: { fullName: string; firstName?: string; lastName?: string; phoneNumber?: string; isActive?: boolean; }): Observable<any> {
    return this.http.put(`${this.api}/users/${id}`, payload);
  }

  setUserActive(id: string, isActive: boolean): Observable<any> {
    return this.http.put(`${this.api}/users/${id}/active`, {}, { params: { isActive } as any });
  }

  getSessions(userId: string, params?: { page?: number; pageSize?: number }): Observable<any> {
    const options: any = { params: { userId } };
    if (params) {
      if (params.page) options.params.page = params.page;
      if (params.pageSize) options.params.pageSize = params.pageSize;
    }
    return this.http.get(`${this.api}/sessions`, options);
  }

  terminateSession(sessionId: string): Observable<any> {
    return this.http.delete(`${this.api}/sessions/${sessionId}`);
  }

  terminateAllUserSessions(userId: string): Observable<any> {
    return this.http.delete(`${this.api}/sessions/user/${userId}`);
  }

  getMfaSettings(userId: string): Observable<any> {
    return this.http.get(`${this.api}/mfa/settings/${userId}`);
  }

  getPasswordPolicy(): Observable<any> {
    return this.http.get(`${this.api}/password-policy`);
  }

  logout(): Observable<any> {
    return this.http.post(`${this.api}/auth/logout`, {});
  }
}
