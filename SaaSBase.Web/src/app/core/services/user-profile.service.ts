import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface UserProfile {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  avatar?: string;
  avatarUrl?: string; // Add this field to match backend response
  employeeId?: string;
  department?: string;
  jobTitle?: string;
  location?: string;
  notes?: string;
  language?: string;
  theme?: string;
  preferences: {
    theme: 'light' | 'dark';
    language: string;
    notifications: {
      email: boolean;
      push: boolean;
      sms: boolean;
    };
  };
  createdAt: string;
  updatedAt: string;
  notificationPreferences?: {
    id: string;
    userId: string;
    emailNotifications: boolean;
    smsNotifications: boolean;
    pushNotifications: boolean;
    inventoryAlerts: boolean;
    orderNotifications: boolean;
    systemNotifications: boolean;
    marketingEmails: boolean;
    securityAlerts: boolean;
    systemUpdates: boolean;
    weeklyReports: boolean;
    monthlyReports: boolean;
    notificationFrequency: string;
    preferredNotificationTime: string;
    createdAt: string;
    lastModifiedAt: string;
  };
}

export interface UpdateProfileRequest {
  fullName?: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  avatar?: string;
  employeeId?: string;
  department?: string;
  jobTitle?: string;
  location?: string;
  notes?: string;
  language?: string;
  theme?: string;
  preferences: {
    theme: 'light' | 'dark';
    language: string;
    notifications: {
      email: boolean;
      push: boolean;
      sms: boolean;
    };
  };
}

@Injectable({ providedIn: 'root' })
export class UserProfileService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;

  constructor(private http: HttpClient) {}

  getProfile(userId: string): Observable<UserProfile> { 
    return this.http.get<UserProfile>(`${this.api}/user-profile/${userId}`).pipe(
      map(profile => ({
        ...profile,
        avatar: profile.avatarUrl ? `${environment.apiBaseUrl}${profile.avatarUrl}` : profile.avatar,
        avatarUrl: profile.avatarUrl ? `${environment.apiBaseUrl}${profile.avatarUrl}` : profile.avatarUrl
      }))
    );
  }
  
  updateProfile(userId: string, profile: UpdateProfileRequest): Observable<UserProfile> { 
    return this.http.put<UserProfile>(`${this.api}/user-profile/${userId}`, profile).pipe(
      map(updatedProfile => ({
        ...updatedProfile,
        avatar: updatedProfile.avatarUrl ? `${environment.apiBaseUrl}${updatedProfile.avatarUrl}` : updatedProfile.avatar,
        avatarUrl: updatedProfile.avatarUrl ? `${environment.apiBaseUrl}${updatedProfile.avatarUrl}` : updatedProfile.avatarUrl
      }))
    );
  }
  
  uploadAvatar(userId: string, file: File): Observable<any> {
    const formData = new FormData();
    formData.append('avatar', file);
    
    return this.http.post<any>(`${this.api}/user-profile/${userId}/avatar`, formData).pipe(
      map(response => ({
        ...response,
        avatarUrl: response.avatarUrl ? `${environment.apiBaseUrl}${response.avatarUrl}` : response.avatarUrl,
        avatar: response.avatarUrl ? `${environment.apiBaseUrl}${response.avatarUrl}` : response.avatar
      }))
    );
  }
  
  removeAvatar(userId: string): Observable<any> {
    return this.http.delete<any>(`${this.api}/user-profile/${userId}/avatar`);
  }
  
  getNotificationPrefs(userId: string): Observable<any> { 
    return this.http.get(`${this.api}/user-profile/${userId}/notification-preferences`); 
  }
  
  getActivityLogs(userId: string, page = 1, pageSize = 50): Observable<any> {
    return this.http.get(`${this.api}/user-profile/${userId}/activity-logs`, { params: { page, pageSize } as any });
  }

  // Dropdown options methods - using Users endpoints
  getDropdownOptions(): Observable<{locations: string[], departments: string[], positions: string[]}> {
    return this.http.get<{locations: string[], departments: string[], positions: string[]}>(`${this.api}/users/dropdown-options`);
  }

  getLocationOptions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/users/dropdown-options/locations`);
  }

  getDepartmentOptions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/users/dropdown-options/departments`);
  }

  getPositionOptions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.api}/users/dropdown-options/positions`);
  }

  updateNotificationPreferences(userId: string, preferences: any): Observable<any> {
    return this.http.put<any>(`${this.api}/user-profile/${userId}/notification-preferences`, preferences);
  }
}


