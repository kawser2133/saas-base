import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { OrganizationService, Organization } from './organization.service';

export interface OrganizationTheme {
  primaryColor: string;
  secondaryColor: string;
}

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private defaultPrimaryColor = '#2563eb'; // --primary-600
  private defaultSecondaryColor = '#6b7280'; // --gray-600

  private themeSubject = new BehaviorSubject<OrganizationTheme>({
    primaryColor: this.defaultPrimaryColor,
    secondaryColor: this.defaultSecondaryColor
  });

  public theme$: Observable<OrganizationTheme> = this.themeSubject.asObservable();

  constructor(private organizationService: OrganizationService) {
    this.loadOrganizationTheme();
  }

  /**
   * Load organization theme colors and apply them
   */
  loadOrganizationTheme(): void {
    const orgId = localStorage.getItem('organizationId');
    if (orgId) {
      this.organizationService.getOrganization(orgId).subscribe({
        next: (org: Organization) => {
          this.applyTheme({
            primaryColor: org.primaryColor || this.defaultPrimaryColor,
            secondaryColor: org.secondaryColor || this.defaultSecondaryColor
          });
        },
        error: () => {
          // Use defaults if organization not found
          this.applyTheme({
            primaryColor: this.defaultPrimaryColor,
            secondaryColor: this.defaultSecondaryColor
          });
        }
      });
    } else {
      // Use defaults if no organization ID
      this.applyTheme({
        primaryColor: this.defaultPrimaryColor,
        secondaryColor: this.defaultSecondaryColor
      });
    }
  }

  /**
   * Apply theme colors to CSS variables
   */
  applyTheme(theme: OrganizationTheme): void {
    this.themeSubject.next(theme);
    
    const root = document.documentElement;
    
    // Apply primary color
    if (theme.primaryColor) {
      root.style.setProperty('--org-primary-color', theme.primaryColor);
      root.style.setProperty('--org-primary-rgb', this.hexToRgb(theme.primaryColor));
      
      // Generate color variations
      const primaryVariations = this.generateColorVariations(theme.primaryColor);
      root.style.setProperty('--org-primary-50', primaryVariations[50]);
      root.style.setProperty('--org-primary-100', primaryVariations[100]);
      root.style.setProperty('--org-primary-200', primaryVariations[200]);
      root.style.setProperty('--org-primary-300', primaryVariations[300]);
      root.style.setProperty('--org-primary-400', primaryVariations[400]);
      root.style.setProperty('--org-primary-500', primaryVariations[500]);
      root.style.setProperty('--org-primary-600', primaryVariations[600]);
      root.style.setProperty('--org-primary-700', primaryVariations[700]);
      root.style.setProperty('--org-primary-800', primaryVariations[800]);
      root.style.setProperty('--org-primary-900', primaryVariations[900]);
    }
    
    // Apply secondary color
    if (theme.secondaryColor) {
      root.style.setProperty('--org-secondary-color', theme.secondaryColor);
      root.style.setProperty('--org-secondary-rgb', this.hexToRgb(theme.secondaryColor));
      
      // Generate color variations
      const secondaryVariations = this.generateColorVariations(theme.secondaryColor);
      root.style.setProperty('--org-secondary-50', secondaryVariations[50]);
      root.style.setProperty('--org-secondary-100', secondaryVariations[100]);
      root.style.setProperty('--org-secondary-200', secondaryVariations[200]);
      root.style.setProperty('--org-secondary-300', secondaryVariations[300]);
      root.style.setProperty('--org-secondary-400', secondaryVariations[400]);
      root.style.setProperty('--org-secondary-500', secondaryVariations[500]);
      root.style.setProperty('--org-secondary-600', secondaryVariations[600]);
      root.style.setProperty('--org-secondary-700', secondaryVariations[700]);
      root.style.setProperty('--org-secondary-800', secondaryVariations[800]);
      root.style.setProperty('--org-secondary-900', secondaryVariations[900]);
    }
  }

  /**
   * Get current theme
   */
  getCurrentTheme(): OrganizationTheme {
    return this.themeSubject.value;
  }

  /**
   * Convert hex color to RGB
   */
  private hexToRgb(hex: string): string {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    if (!result) return '37, 99, 235'; // Default blue
    
    const r = parseInt(result[1], 16);
    const g = parseInt(result[2], 16);
    const b = parseInt(result[3], 16);
    
    return `${r}, ${g}, ${b}`;
  }

  /**
   * Generate color variations (lighter and darker shades)
   */
  private generateColorVariations(hex: string): { [key: number]: string } {
    const rgb = this.hexToRgb(hex).split(',').map(v => parseInt(v.trim()));
    const [r, g, b] = rgb;
    
    const variations: { [key: number]: string } = {};
    
    // Generate lighter shades (50-400)
    variations[50] = this.lightenColor(r, g, b, 0.9);
    variations[100] = this.lightenColor(r, g, b, 0.8);
    variations[200] = this.lightenColor(r, g, b, 0.6);
    variations[300] = this.lightenColor(r, g, b, 0.4);
    variations[400] = this.lightenColor(r, g, b, 0.2);
    
    // Base color (500-600)
    variations[500] = hex;
    variations[600] = hex;
    
    // Generate darker shades (700-900)
    variations[700] = this.darkenColor(r, g, b, 0.15);
    variations[800] = this.darkenColor(r, g, b, 0.3);
    variations[900] = this.darkenColor(r, g, b, 0.45);
    
    return variations;
  }

  /**
   * Lighten a color
   */
  private lightenColor(r: number, g: number, b: number, amount: number): string {
    const newR = Math.min(255, Math.round(r + (255 - r) * amount));
    const newG = Math.min(255, Math.round(g + (255 - g) * amount));
    const newB = Math.min(255, Math.round(b + (255 - b) * amount));
    return `#${this.componentToHex(newR)}${this.componentToHex(newG)}${this.componentToHex(newB)}`;
  }

  /**
   * Darken a color
   */
  private darkenColor(r: number, g: number, b: number, amount: number): string {
    const newR = Math.max(0, Math.round(r * (1 - amount)));
    const newG = Math.max(0, Math.round(g * (1 - amount)));
    const newB = Math.max(0, Math.round(b * (1 - amount)));
    return `#${this.componentToHex(newR)}${this.componentToHex(newG)}${this.componentToHex(newB)}`;
  }

  /**
   * Convert number to hex
   */
  private componentToHex(c: number): string {
    const hex = c.toString(16);
    return hex.length === 1 ? '0' + hex : hex;
  }
}
