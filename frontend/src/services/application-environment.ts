import { apiService } from './api.service';

export type ApplicationEnvironmentName =
  | 'Production'
  | 'Test'
  | 'UAT'
  | 'Staging'
  | 'Development'
  | 'Unknown';

export interface PublicApplicationEnvironment {
  environment: ApplicationEnvironmentName;
  isProduction: boolean;
  displayName: string;
  message: string;
  configurationValid: boolean;
  dataIsolationConfirmed: boolean;
  applicationVersion: string;
  buildId: string | null;
  deployedAtUtc: string | null;
}

const SUPPORTED_ENVIRONMENTS = new Set<ApplicationEnvironmentName>([
  'Production',
  'Test',
  'UAT',
  'Staging',
  'Development',
]);

export const UNKNOWN_APPLICATION_ENVIRONMENT: PublicApplicationEnvironment = {
  environment: 'Unknown',
  isProduction: false,
  displayName: 'Unknown Environment',
  message: 'Environment configuration is missing or unavailable. Treat this system as unsafe until corrected.',
  configurationValid: false,
  dataIsolationConfirmed: false,
  applicationVersion: 'Unknown',
  buildId: null,
  deployedAtUtc: null,
};

const safeText = (value: unknown, fallback: string): string =>
  typeof value === 'string' && value.trim().length > 0 ? value.trim() : fallback;

const safeOptionalText = (value: unknown): string | null =>
  typeof value === 'string' && value.trim().length > 0 ? value.trim() : null;

export function normalizeApplicationEnvironment(value: unknown): PublicApplicationEnvironment {
  if (!value || typeof value !== 'object') return UNKNOWN_APPLICATION_ENVIRONMENT;

  const response = value as Record<string, unknown>;
  const environment = typeof response.environment === 'string'
    && SUPPORTED_ENVIRONMENTS.has(response.environment as ApplicationEnvironmentName)
      ? response.environment as ApplicationEnvironmentName
      : 'Unknown';

  if (environment === 'Unknown' || response.configurationValid !== true) {
    return UNKNOWN_APPLICATION_ENVIRONMENT;
  }

  return {
    environment,
    isProduction: environment === 'Production',
    displayName: safeText(response.displayName, `${environment} Environment`),
    message: safeText(
      response.message,
      environment === 'Production'
        ? 'Live production environment'
        : `${environment} application environment`,
    ),
    configurationValid: true,
    dataIsolationConfirmed: response.dataIsolationConfirmed === true,
    applicationVersion: safeText(response.applicationVersion, 'Unknown'),
    buildId: safeOptionalText(response.buildId),
    deployedAtUtc: safeOptionalText(response.deployedAtUtc),
  };
}

class ApplicationEnvironmentService {
  async getPublicEnvironment(): Promise<PublicApplicationEnvironment> {
    try {
      const response = await apiService.publicRequest<unknown>('/public/config/environment', {
        method: 'GET',
        cache: 'no-store',
      });
      return normalizeApplicationEnvironment(response);
    } catch {
      return UNKNOWN_APPLICATION_ENVIRONMENT;
    }
  }
}

export const applicationEnvironmentService = new ApplicationEnvironmentService();
