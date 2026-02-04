import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { Currency, OrganizationService } from '../../core/services/organization.service';

/**
 * CurrencyService - Service to manage and provide default/base currency information
 * This service caches the default currency and provides methods to get currency symbol and code
 */
@Injectable({
  providedIn: 'root'
})
export class CurrencyService {
  private defaultCurrency$ = new BehaviorSubject<Currency | null>(null);
  private currencies$ = new BehaviorSubject<Currency[]>([]);
  private isLoading$ = new BehaviorSubject<boolean>(false);

  constructor(private organizationService: OrganizationService) {
    this.loadCurrencies();
  }

  /**
   * Load currencies from organization service
   */
  private loadCurrencies(): void {
    this.isLoading$.next(true);
    this.organizationService.getCurrencies({ isActive: true }).subscribe({
      next: (response: any) => {
        const currencies: Currency[] = response.items || response || [];
        this.currencies$.next(currencies);
        
        // Find default currency
        const defaultCurrency = currencies.find(c => c.isDefault) || 
                               (currencies.length > 0 ? currencies[0] : null);
        this.defaultCurrency$.next(defaultCurrency);
        this.isLoading$.next(false);
      },
      error: (error) => {
        console.error('Error loading currencies:', error);
        this.isLoading$.next(false);
      }
    });
  }

  /**
   * Get default currency as Observable
   */
  getDefaultCurrency(): Observable<Currency | null> {
    return this.defaultCurrency$.asObservable();
  }

  /**
   * Get default currency synchronously (returns current value)
   */
  getDefaultCurrencySync(): Currency | null {
    return this.defaultCurrency$.value;
  }

  /**
   * Get default currency symbol
   * @param fallback - Fallback symbol if no default currency is found (default: '$')
   */
  getDefaultCurrencySymbol(fallback: string = '$'): string {
    const defaultCurrency = this.defaultCurrency$.value;
    return defaultCurrency?.symbol || fallback;
  }

  /**
   * Get default currency code
   * @param fallback - Fallback code if no default currency is found (default: 'USD')
   */
  getDefaultCurrencyCode(fallback: string = 'USD'): string {
    const defaultCurrency = this.defaultCurrency$.value;
    return defaultCurrency?.code || fallback;
  }

  /**
   * Get currency symbol by currency code
   * @param currencyCode - Currency code (e.g., 'USD', 'EUR')
   * @param fallback - Fallback symbol if currency not found
   */
  getCurrencySymbolByCode(currencyCode: string, fallback: string = '$'): string {
    if (!currencyCode) {
      return this.getDefaultCurrencySymbol(fallback);
    }
    
    const currencies = this.currencies$.value;
    const currency = currencies.find(c => c.code === currencyCode);
    return currency?.symbol || this.getDefaultCurrencySymbol(fallback);
  }

  /**
   * Format price with currency symbol
   * @param price - Price value
   * @param currencyCode - Optional currency code, if not provided uses default
   * @param decimalPlaces - Number of decimal places (default: 2)
   */
  formatPrice(price: number | null | undefined, currencyCode?: string, decimalPlaces: number = 2): string {
    if (price === null || price === undefined) {
      return '-';
    }
    
    const symbol = currencyCode 
      ? this.getCurrencySymbolByCode(currencyCode)
      : this.getDefaultCurrencySymbol();
    
    return symbol + price.toFixed(decimalPlaces);
  }

  /**
   * Refresh currencies from server
   */
  refreshCurrencies(): void {
    this.loadCurrencies();
  }

  /**
   * Get all currencies as Observable
   */
  getCurrencies(): Observable<Currency[]> {
    return this.currencies$.asObservable();
  }

  /**
   * Get all currencies synchronously
   */
  getCurrenciesSync(): Currency[] {
    return this.currencies$.value;
  }
}

