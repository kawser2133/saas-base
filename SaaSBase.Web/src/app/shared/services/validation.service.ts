import { Injectable } from '@angular/core';
import { AbstractControl, FormGroup, ValidationErrors } from '@angular/forms';

export interface ValidationRule {
  name: string;
  message: string;
  validator: (value: any) => boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ValidationService {
  
  // Common validation rules
  static readonly RULES = {
    required: (message: string = 'This field is required'): ValidationRule => ({
      name: 'required',
      message,
      validator: (value: any) => value != null && value !== '' && value.toString().trim() !== ''
    }),

    email: (message: string = 'Please enter a valid email address'): ValidationRule => ({
      name: 'email',
      message,
      validator: (value: string) => {
        if (!value) return true; // Let required handle empty values
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(value);
      }
    }),

    minLength: (min: number, message?: string): ValidationRule => ({
      name: 'minLength',
      message: message || `Must be at least ${min} characters long`,
      validator: (value: string) => {
        if (!value) return true;
        return value.length >= min;
      }
    }),

    maxLength: (max: number, message?: string): ValidationRule => ({
      name: 'maxLength',
      message: message || `Must be no more than ${max} characters long`,
      validator: (value: string) => {
        if (!value) return true;
        return value.length <= max;
      }
    }),

    pattern: (regex: RegExp, message: string): ValidationRule => ({
      name: 'pattern',
      message,
      validator: (value: string) => {
        if (!value) return true;
        return regex.test(value);
      }
    }),

    min: (min: number, message?: string): ValidationRule => ({
      name: 'min',
      message: message || `Must be at least ${min}`,
      validator: (value: number) => {
        if (value == null) return true;
        return value >= min;
      }
    }),

    max: (max: number, message?: string): ValidationRule => ({
      name: 'max',
      message: message || `Must be no more than ${max}`,
      validator: (value: number) => {
        if (value == null) return true;
        return value <= max;
      }
    }),

    password: (message: string = 'Password must be at least 8 characters with uppercase, lowercase, number and special character'): ValidationRule => ({
      name: 'password',
      message,
      validator: (value: string) => {
        if (!value) return true;
        const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/;
        return passwordRegex.test(value);
      }
    }),

    phone: (message: string = 'Please enter a valid phone number'): ValidationRule => ({
      name: 'phone',
      message,
      validator: (value: string) => {
        if (!value) return true;
        const phoneRegex = /^[\+]?[1-9][\d]{0,15}$/;
        return phoneRegex.test(value.replace(/[\s\-\(\)]/g, ''));
      }
    }),

    url: (message: string = 'Please enter a valid URL'): ValidationRule => ({
      name: 'url',
      message,
      validator: (value: string) => {
        if (!value) return true;
        try {
          new URL(value);
          return true;
        } catch {
          return false;
        }
      }
    })
  };

  /**
   * Validate a single value against multiple rules
   */
  validate(value: any, rules: ValidationRule[]): string[] {
    const errors: string[] = [];
    
    for (const rule of rules) {
      if (!rule.validator(value)) {
        errors.push(rule.message);
      }
    }
    
    return errors;
  }

  /**
   * Validate a form group
   */
  validateForm(form: FormGroup, validationRules: { [key: string]: ValidationRule[] }): { [key: string]: string[] } {
    const errors: { [key: string]: string[] } = {};
    
    Object.keys(validationRules).forEach(controlName => {
      const control = form.get(controlName);
      if (control) {
        const fieldErrors = this.validate(control.value, validationRules[controlName]);
        if (fieldErrors.length > 0) {
          errors[controlName] = fieldErrors;
        }
      }
    });
    
    return errors;
  }

  /**
   * Get the first error message for a field
   */
  getFirstError(errors: string[]): string | null {
    return errors.length > 0 ? errors[0] : null;
  }

  /**
   * Check if a form is valid based on custom rules
   */
  isFormValid(form: FormGroup, validationRules: { [key: string]: ValidationRule[] }): boolean {
    const errors = this.validateForm(form, validationRules);
    return Object.keys(errors).length === 0;
  }

  /**
   * Mark form fields as touched to trigger validation display
   */
  markFormGroupTouched(form: FormGroup): void {
    Object.keys(form.controls).forEach(key => {
      const control = form.get(key);
      control?.markAsTouched();
    });
  }

  /**
   * Get validation errors for Angular reactive forms
   */
  getFormControlErrors(control: AbstractControl): string[] {
    if (!control.errors || !control.touched) {
      return [];
    }

    const errors: string[] = [];
    const errorMessages: { [key: string]: string } = {
      required: 'This field is required',
      email: 'Please enter a valid email address',
      minlength: `Must be at least ${control.errors['minlength']?.requiredLength} characters long`,
      maxlength: `Must be no more than ${control.errors['maxlength']?.requiredLength} characters long`,
      min: `Must be at least ${control.errors['min']?.min}`,
      max: `Must be no more than ${control.errors['max']?.max}`,
      pattern: 'Please enter a valid format'
    };

    Object.keys(control.errors).forEach(key => {
      if (errorMessages[key]) {
        errors.push(errorMessages[key]);
      }
    });

    return errors;
  }
}
