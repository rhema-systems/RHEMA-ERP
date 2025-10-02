import type { PasswordPolicy } from '../services/profile';

export interface PasswordValidationResult {
  isValid: boolean;
  errors: string[];
}

export function validatePassword(password: string, policy: PasswordPolicy): PasswordValidationResult {
  const errors: string[] = [];

  // Check minimum length
  if (password.length < policy.minLength) {
    errors.push(`Password must be at least ${policy.minLength} characters long`);
  }

  // Check uppercase requirement
  if (policy.requireUppercase && !/[A-Z]/.test(password)) {
    errors.push('Password must contain at least one uppercase letter');
  }

  // Check lowercase requirement
  if (policy.requireLowercase && !/[a-z]/.test(password)) {
    errors.push('Password must contain at least one lowercase letter');
  }

  // Check digits requirement
  if (policy.requireDigits && !/[0-9]/.test(password)) {
    errors.push('Password must contain at least one digit');
  }

  // Check special characters requirement
  if (policy.requireSpecialChars && !/[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)) {
    errors.push('Password must contain at least one special character');
  }

  return {
    isValid: errors.length === 0,
    errors
  };
}

export function getPasswordRequirementsText(policy: PasswordPolicy): string[] {
  const requirements: string[] = [];

  requirements.push(`At least ${policy.minLength} characters long`);
  
  if (policy.requireUppercase) {
    requirements.push('Contains an uppercase letter');
  }
  
  if (policy.requireLowercase) {
    requirements.push('Contains a lowercase letter');
  }
  
  if (policy.requireDigits) {
    requirements.push('Contains a digit');
  }
  
  if (policy.requireSpecialChars) {
    requirements.push('Contains a special character');
  }

  return requirements;
}