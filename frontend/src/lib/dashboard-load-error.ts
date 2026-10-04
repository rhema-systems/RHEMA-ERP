type ApiErrorLike = {
  status?: unknown;
  response?: {
    status?: unknown;
  };
};

export interface DashboardLoadErrorPresentation {
  accessDenied: boolean;
  message: string;
  canRetry: boolean;
}

export const getHttpStatus = (error: unknown): number | undefined => {
  if (!error || typeof error !== 'object') return undefined;
  const candidate = error as ApiErrorLike;
  const status = candidate.status ?? candidate.response?.status;
  return typeof status === 'number' ? status : undefined;
};

export const isDashboardAccessError = (error: unknown) => {
  const status = getHttpStatus(error);
  return status === 401 || status === 403;
};

export const getDashboardLoadErrorPresentation = (
  error: unknown,
): DashboardLoadErrorPresentation => {
  const status = getHttpStatus(error);

  if (status === 403) {
    return {
      accessDenied: true,
      message:
        'You do not have permission to view the enterprise dashboard. Ask an administrator for Dashboard access.',
      canRetry: false,
    };
  }

  if (status === 401) {
    return {
      accessDenied: true,
      message: 'Your session is not authorized to view the enterprise dashboard. Sign in again or contact an administrator.',
      canRetry: false,
    };
  }

  return {
    accessDenied: false,
    message: 'The enterprise dashboard could not be loaded. Please retry.',
    canRetry: true,
  };
};
