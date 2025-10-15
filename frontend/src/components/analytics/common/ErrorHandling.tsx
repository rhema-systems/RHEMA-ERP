'use client';

import React from 'react';
import { AlertTriangle, RefreshCw, WifiOff, Server, AlertCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

// #region Error Types

export interface AnalyticsError {
  type: 'network' | 'server' | 'validation' | 'unknown';
  message: string;
  details?: string;
  code?: string;
  timestamp?: string;
}

// #endregion

// #region Error Boundary Component

interface AnalyticsErrorBoundaryState {
  hasError: boolean;
  error?: Error;
}

interface AnalyticsErrorBoundaryProps {
  children: React.ReactNode;
  fallback?: React.ComponentType<{ error: Error; reset: () => void }>;
  onError?: (error: Error, errorInfo: React.ErrorInfo) => void;
}

export class AnalyticsErrorBoundary extends React.Component<
  AnalyticsErrorBoundaryProps,
  AnalyticsErrorBoundaryState
> {
  constructor(props: AnalyticsErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error: Error): AnalyticsErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    console.error('Analytics Error Boundary caught an error:', error, errorInfo);
    this.props.onError?.(error, errorInfo);
  }

  render() {
    if (this.state.hasError && this.state.error) {
      const reset = () => this.setState({ hasError: false, error: undefined });
      
      if (this.props.fallback) {
        const Fallback = this.props.fallback;
        return <Fallback error={this.state.error} reset={reset} />;
      }

      return (
        <AnalyticsErrorDisplay
          error={{
            type: 'unknown',
            message: this.state.error.message,
            details: this.state.error.stack,
          }}
          onRetry={reset}
          showDetails
        />
      );
    }

    return this.props.children;
  }
}

// #endregion

// #region Error Display Component

interface AnalyticsErrorDisplayProps {
  error: AnalyticsError;
  onRetry?: () => void;
  showDetails?: boolean;
  className?: string;
  variant?: 'card' | 'alert' | 'inline';
}

export const AnalyticsErrorDisplay: React.FC<AnalyticsErrorDisplayProps> = ({
  error,
  onRetry,
  showDetails = false,
  className,
  variant = 'card',
}) => {
  const getErrorIcon = () => {
    switch (error.type) {
      case 'network':
        return <WifiOff className="h-5 w-5" />;
      case 'server':
        return <Server className="h-5 w-5" />;
      case 'validation':
        return <AlertCircle className="h-5 w-5" />;
      default:
        return <AlertTriangle className="h-5 w-5" />;
    }
  };

  const getErrorTitle = () => {
    switch (error.type) {
      case 'network':
        return 'Connection Error';
      case 'server':
        return 'Server Error';
      case 'validation':
        return 'Invalid Data';
      default:
        return 'Analytics Error';
    }
  };

  const getErrorSuggestion = () => {
    switch (error.type) {
      case 'network':
        return 'Please check your internet connection and try again.';
      case 'server':
        return 'Our servers are experiencing issues. Please try again in a few moments.';
      case 'validation':
        return 'Please check your filter settings and ensure all required fields are filled.';
      default:
        return 'An unexpected error occurred. Please try again.';
    }
  };

  if (variant === 'alert') {
    return (
      <Alert variant="destructive" className={className}>
        {getErrorIcon()}
        <AlertTitle>{getErrorTitle()}</AlertTitle>
        <AlertDescription className="mt-2">
          {error.message}
          {showDetails && error.details && (
            <details className="mt-2">
              <summary className="cursor-pointer text-sm">Technical details</summary>
              <pre className="mt-1 text-xs whitespace-pre-wrap">{error.details}</pre>
            </details>
          )}
        </AlertDescription>
        {onRetry && (
          <Button
            variant="outline"
            size="sm"
            onClick={onRetry}
            className="mt-3"
          >
            <RefreshCw className="h-4 w-4 mr-2" />
            Try Again
          </Button>
        )}
      </Alert>
    );
  }

  if (variant === 'inline') {
    return (
      <div className={cn('flex items-center justify-center p-8 text-center', className)}>
        <div className="max-w-md">
          <div className="flex justify-center mb-4 text-muted-foreground">
            {getErrorIcon()}
          </div>
          <h3 className="text-lg font-semibold text-foreground mb-2">
            {getErrorTitle()}
          </h3>
          <p className="text-sm text-muted-foreground mb-4">
            {error.message}
          </p>
          <p className="text-xs text-muted-foreground mb-4">
            {getErrorSuggestion()}
          </p>
          {onRetry && (
            <Button onClick={onRetry} variant="outline" size="sm">
              <RefreshCw className="h-4 w-4 mr-2" />
              Try Again
            </Button>
          )}
        </div>
      </div>
    );
  }

  // Default card variant
  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-destructive">
          {getErrorIcon()}
          {getErrorTitle()}
        </CardTitle>
        <CardDescription>
          {error.message}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground mb-4">
          {getErrorSuggestion()}
        </p>
        
        {showDetails && error.details && (
          <details className="mb-4">
            <summary className="cursor-pointer text-sm font-medium">
              Technical Details
            </summary>
            <pre className="mt-2 p-3 bg-muted rounded text-xs whitespace-pre-wrap overflow-auto">
              {error.details}
            </pre>
          </details>
        )}

        <div className="flex items-center gap-2">
          {onRetry && (
            <Button onClick={onRetry} variant="outline" size="sm">
              <RefreshCw className="h-4 w-4 mr-2" />
              Try Again
            </Button>
          )}
          {error.timestamp && (
            <span className="text-xs text-muted-foreground ml-auto">
              {new Date(error.timestamp).toLocaleString()}
            </span>
          )}
        </div>
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Loading Skeletons

interface AnalyticsLoadingSkeletonProps {
  variant?: 'chart' | 'kpi' | 'table' | 'gauge' | 'list';
  className?: string;
  rows?: number;
}

export const AnalyticsLoadingSkeleton: React.FC<AnalyticsLoadingSkeletonProps> = ({
  variant = 'chart',
  className,
  rows = 5,
}) => {
  if (variant === 'kpi') {
    return (
      <Card className={className}>
        <CardContent className="p-6">
          <div className="flex items-center justify-between">
            <div className="space-y-2">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-8 w-16" />
              <Skeleton className="h-3 w-20" />
            </div>
            <Skeleton className="h-12 w-12 rounded-full" />
          </div>
        </CardContent>
      </Card>
    );
  }

  if (variant === 'gauge') {
    return (
      <Card className={className}>
        <CardHeader>
          <Skeleton className="h-6 w-48" />
          <Skeleton className="h-4 w-32" />
        </CardHeader>
        <CardContent>
          <div className="flex justify-center mb-4">
            <Skeleton className="h-40 w-40 rounded-full" />
          </div>
          <div className="grid grid-cols-3 gap-4">
            <div className="text-center space-y-2">
              <Skeleton className="h-4 w-16 mx-auto" />
              <Skeleton className="h-6 w-12 mx-auto" />
              <Skeleton className="h-2 w-20 mx-auto" />
            </div>
            <div className="text-center space-y-2">
              <Skeleton className="h-4 w-16 mx-auto" />
              <Skeleton className="h-6 w-12 mx-auto" />
              <Skeleton className="h-2 w-20 mx-auto" />
            </div>
            <div className="text-center space-y-2">
              <Skeleton className="h-4 w-16 mx-auto" />
              <Skeleton className="h-6 w-12 mx-auto" />
              <Skeleton className="h-2 w-20 mx-auto" />
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (variant === 'table') {
    return (
      <Card className={className}>
        <CardHeader>
          <Skeleton className="h-6 w-48" />
          <Skeleton className="h-4 w-64" />
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            <div className="grid grid-cols-4 gap-4 pb-2 border-b">
              <Skeleton className="h-4 w-16" />
              <Skeleton className="h-4 w-20" />
              <Skeleton className="h-4 w-16" />
              <Skeleton className="h-4 w-20" />
            </div>
            {Array.from({ length: rows }).map((_, i) => (
              <div key={i} className="grid grid-cols-4 gap-4">
                <Skeleton className="h-4 w-24" />
                <Skeleton className="h-4 w-16" />
                <Skeleton className="h-4 w-12" />
                <Skeleton className="h-4 w-20" />
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  if (variant === 'list') {
    return (
      <Card className={className}>
        <CardHeader>
          <Skeleton className="h-6 w-48" />
          <Skeleton className="h-4 w-32" />
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {Array.from({ length: rows }).map((_, i) => (
              <div key={i} className="flex items-center justify-between p-3 border rounded-lg">
                <div className="flex items-center space-x-3">
                  <Skeleton className="h-8 w-8 rounded-full" />
                  <div className="space-y-1">
                    <Skeleton className="h-4 w-32" />
                    <Skeleton className="h-3 w-24" />
                  </div>
                </div>
                <Skeleton className="h-6 w-16" />
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  // Default chart variant
  return (
    <Card className={className}>
      <CardHeader>
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-4 w-64" />
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          <div className="h-64 w-full flex items-end justify-between space-x-2">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton
                key={i}
                className="w-full"
                style={{ height: `${Math.random() * 80 + 20}%` }}
              />
            ))}
          </div>
          <div className="flex justify-center space-x-6">
            <div className="flex items-center space-x-2">
              <Skeleton className="h-3 w-3 rounded-full" />
              <Skeleton className="h-3 w-16" />
            </div>
            <div className="flex items-center space-x-2">
              <Skeleton className="h-3 w-3 rounded-full" />
              <Skeleton className="h-3 w-20" />
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Loading Overlay

interface LoadingOverlayProps {
  isLoading: boolean;
  children: React.ReactNode;
  className?: string;
  message?: string;
}

export const LoadingOverlay: React.FC<LoadingOverlayProps> = ({
  isLoading,
  children,
  className,
  message = 'Loading analytics data...',
}) => {
  return (
    <div className={cn('relative', className)}>
      {children}
      {isLoading && (
        <div className="absolute inset-0 bg-background/80 backdrop-blur-sm flex items-center justify-center z-10">
          <div className="text-center">
            <RefreshCw className="h-8 w-8 animate-spin mx-auto mb-4 text-primary" />
            <p className="text-sm text-muted-foreground">{message}</p>
          </div>
        </div>
      )}
    </div>
  );
};

// #endregion