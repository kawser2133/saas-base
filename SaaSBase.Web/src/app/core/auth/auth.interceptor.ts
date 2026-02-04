import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { clearAuthStorage, getAccessToken, decodeJwt } from './auth.utils';
import { catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';
import { NotificationService } from '../../shared/services/notification.service';

const isAuthRefreshRequest = (req: HttpRequest<any>): boolean => {
  return req.url.includes('/auth/refresh');
};

const isAuthRequest = (req: HttpRequest<any>): boolean => {
  return req.url.includes('/auth/login') || 
         req.url.includes('/auth/forgot-password') || 
         req.url.includes('/auth/reset-password') ||
         req.url.includes('/auth/verify-email');
};

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = getAccessToken();
  const notificationService = inject(NotificationService);
  
  // Build headers object
  const headers: { [key: string]: string } = {};
  
  // Add authorization header if token exists and not a refresh request
  if (token && !isAuthRefreshRequest(req)) {
    headers['Authorization'] = `Bearer ${token}`;
    
    // Extract organization ID from JWT token
    const decodedToken = decodeJwt(token);
    const organizationId = decodedToken?.tenant_id || decodedToken?.organizationId;
    
    if (organizationId) {
      headers['X-Organization-Id'] = organizationId;
    }
  }
  
  const authReq = Object.keys(headers).length > 0 
    ? req.clone({ setHeaders: headers })
    : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      // Handle 403 Forbidden errors globally
      if (err?.status === 403 && !isAuthRequest(req)) {
        // Extract error message from ProblemDetails format or other error formats
        const errorMessage = err.error?.detail || err.error?.title || err.error?.message || 
          'You do not have the required permissions to access this resource. Please contact your administrator if you believe this is an error.';
        
        // Check if it's an organization inactive error
        if (errorMessage.toLowerCase().includes('organization is inactive')) {
          // Clear auth and redirect to login with message
          clearAuthStorage();
          notificationService.error(
            'Organization Inactive',
            'Your organization account has been deactivated. Please contact your administrator.'
          );
          setTimeout(() => {
            window.location.href = '/login';
          }, 2000);
          return throwError(() => err);
        }
        
        notificationService.unauthorized(
          'Access Denied',
          errorMessage
        );
        return throwError(() => err);
      }

      // Don't redirect on 401 for auth requests (login, forgot password, etc.)
      // Let the component handle the error and show appropriate message
      if (err?.status === 401 && !isAuthRefreshRequest(req) && !isAuthRequest(req)) {
        // For 401 errors on protected routes, clear storage and redirect
        // Don't attempt refresh in interceptor to avoid injection context issues
        clearAuthStorage();
        window.location.href = '/login';
        return throwError(() => err);
      }
      // For auth requests and other errors, just pass through the error
      return throwError(() => err);
    })
  );
};


