export function quantitySurveyConfigurationError(error: unknown): string {
  const candidate = error as { response?: { detail?: string; code?: string; errors?: unknown }; message?: string } | null;
  const problem = candidate?.response;
  const raw = problem?.errors;
  const details = (Array.isArray(raw) ? raw : raw && typeof raw === 'object' ? Object.values(raw).flat() : [])
    .filter((value): value is string => typeof value === 'string' && Boolean(value.trim()));
  const parts = [...new Set([problem?.detail || candidate?.message || 'The request failed.', ...details])];
  return `${parts.join(' ')}${problem?.code ? ` (${problem.code})` : ''}`;
}
