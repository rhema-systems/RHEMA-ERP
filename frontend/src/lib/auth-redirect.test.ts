import { describe, expect, it } from 'vitest';
import {
  buildLoginRedirectUrl,
  buildTenantSelectRedirectUrl,
  getRedirectTargetFromSearchParams,
  normalizeRedirectTarget,
  resolveRedirectTarget,
} from './auth-redirect';

describe('auth-redirect', () => {
  it('normalizes safe internal redirect targets only', () => {
    expect(normalizeRedirectTarget('/crm/accounts/123?tab=timeline')).toBe('/crm/accounts/123?tab=timeline');
    expect(normalizeRedirectTarget('https://example.com/phish')).toBeNull();
    expect(normalizeRedirectTarget('//example.com/phish')).toBeNull();
    expect(normalizeRedirectTarget('/login')).toBeNull();
    expect(normalizeRedirectTarget('/tenant-select?redirect=%2Fcrm')).toBeNull();
  });

  it('builds login and tenant-select URLs with safe redirect targets', () => {
    expect(buildLoginRedirectUrl('/projects/alpha?tab=materials')).toBe('/login?redirect=%2Fprojects%2Falpha%3Ftab%3Dmaterials');
    expect(buildTenantSelectRedirectUrl('/crm/accounts/42')).toBe('/tenant-select?redirect=%2Fcrm%2Faccounts%2F42');
    expect(buildLoginRedirectUrl('/login')).toBe('/login');
  });

  it('reads safe redirect targets from search params and falls back when needed', () => {
    const validParams = new URLSearchParams('redirect=%2Fmaintenance%2Fwork-orders%3Fview%3Dmine');
    const invalidParams = new URLSearchParams('redirect=https%3A%2F%2Fevil.example');

    expect(getRedirectTargetFromSearchParams(validParams)).toBe('/maintenance/work-orders?view=mine');
    expect(getRedirectTargetFromSearchParams(invalidParams)).toBeNull();
    expect(resolveRedirectTarget('/development/projects/1', '/dashboard')).toBe('/development/projects/1');
    expect(resolveRedirectTarget('https://example.com', '/dashboard')).toBe('/dashboard');
  });
});
