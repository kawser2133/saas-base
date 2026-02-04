import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface UserMenuDto {
  label: string;
  route: string;
  icon: string;
  sortOrder: number;
  badge?: string;
  badgeColor?: string;
  submenu: UserMenuDto[];
}

export interface MenuSectionDto {
  title: string;
  items: UserMenuDto[];
}

export interface UserMenuResponseDto {
  sections: MenuSectionDto[];
}

@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;

  constructor(private http: HttpClient) {}

  getUserMenus(userId: string): Observable<UserMenuResponseDto> {
    return this.http.get<UserMenuResponseDto>(`${this.api}/menus/user/${userId}/navigation`);
  }
}


