'use client';

import React from 'react';
import Image from 'next/image';
import { CheckCircle2, Loader2, MonitorCog, RotateCcw, UploadCloud } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { cn } from '@/lib/utils';
import type { LoginPageStyle } from '@/services/settings';

interface LoginAppearanceOption {
  value: LoginPageStyle;
  name: string;
  description: string;
  previewSrc: string;
  previewAlt: string;
}

const LOGIN_APPEARANCE_OPTIONS: LoginAppearanceOption[] = [
  {
    value: 'LightCorporate',
    name: 'Light Corporate',
    description: 'Bright enterprise campus presentation with a clean white sign-in panel.',
    previewSrc: '/images/auth/previews/login-light-corporate.webp',
    previewAlt: 'Preview of the Light Corporate RHEMA-ERP login design',
  },
  {
    value: 'DarkPremium',
    name: 'Dark Premium',
    description: 'Refined global network presentation with a dark glass sign-in panel.',
    previewSrc: '/images/auth/previews/login-dark-premium.webp',
    previewAlt: 'Preview of the Dark Premium RHEMA-ERP login design',
  },
];

interface LoginAppearanceSettingsProps {
  value: LoginPageStyle;
  onValueChange: (value: LoginPageStyle) => void;
  disabled?: boolean;
  isLoading?: boolean;
  errorMessage?: string | null;
  lightBackgroundUrl?: string | null;
  darkBackgroundUrl?: string | null;
  onBackgroundUpload: (style: LoginPageStyle, file: File) => void;
  onBackgroundReset: (style: LoginPageStyle) => void;
  uploadingStyle?: LoginPageStyle | null;
  resettingStyle?: LoginPageStyle | null;
}

export function LoginAppearanceSettings({
  value,
  onValueChange,
  disabled = false,
  isLoading = false,
  errorMessage,
  lightBackgroundUrl,
  darkBackgroundUrl,
  onBackgroundUpload,
  onBackgroundReset,
  uploadingStyle = null,
  resettingStyle = null,
}: LoginAppearanceSettingsProps) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-start gap-3">
          <span className="rounded-lg bg-blue-50 p-2 text-blue-700">
            <MonitorCog className="h-5 w-5" aria-hidden="true" />
          </span>
          <div>
            <CardTitle>Login Page Appearance</CardTitle>
            <CardDescription className="mt-1">
              Choose the login experience unauthenticated users will see. The
              selected design takes effect the next time the public login page loads.
            </CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div
            className="grid gap-4 md:grid-cols-2"
            aria-label="Loading login appearance settings"
          >
            <div className="h-72 animate-pulse rounded-xl bg-slate-100" />
            <div className="h-72 animate-pulse rounded-xl bg-slate-100" />
          </div>
        ) : errorMessage ? (
          <div
            role="alert"
            className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {errorMessage}
          </div>
        ) : (
          <RadioGroup
            value={value}
            onValueChange={(nextValue) => onValueChange(nextValue as LoginPageStyle)}
            disabled={disabled}
            aria-label="Default login page design"
            className="grid gap-5 md:grid-cols-2"
          >
            {LOGIN_APPEARANCE_OPTIONS.map((option) => {
              const isSelected = value === option.value;
              const customBackgroundUrl = option.value === 'DarkPremium'
                ? darkBackgroundUrl
                : lightBackgroundUrl;
              const previewSrc = customBackgroundUrl || option.previewSrc;
              const isUploading = uploadingStyle === option.value;
              const isResetting = resettingStyle === option.value;
              const isBusy = isUploading || isResetting;

              return (
                <div
                  key={option.value}
                  className={cn(
                    'group overflow-hidden rounded-xl border bg-white transition',
                    'focus-within:ring-2 focus-within:ring-blue-600 focus-within:ring-offset-2',
                    disabled ? 'opacity-70' : 'hover:border-blue-300 hover:shadow-md',
                    isSelected && 'border-blue-600 ring-2 ring-blue-600 ring-offset-2'
                  )}
                >
                  <div className="relative aspect-video overflow-hidden bg-slate-100">
                    {customBackgroundUrl ? (
                      // The API origin differs from the Next.js origin during local development.
                      // This tenant-controlled URL has already passed the image upload gate.
                      <img
                        key={previewSrc}
                        src={previewSrc}
                        alt={option.previewAlt}
                        className="absolute inset-0 h-full w-full object-cover transition duration-300 group-hover:scale-[1.02]"
                      />
                    ) : (
                      <Image
                        src={previewSrc}
                        alt={option.previewAlt}
                        fill
                        sizes="(max-width: 768px) 100vw, 40vw"
                        className="object-cover transition duration-300 group-hover:scale-[1.02]"
                      />
                    )}
                    <div className="absolute inset-0 bg-gradient-to-t from-slate-950/20 to-transparent" />
                  </div>
                  <div className="flex items-start gap-3 p-4">
                    <RadioGroupItem
                      id={`login-page-style-${option.value}`}
                      value={option.value}
                      aria-label={option.name}
                      className="mt-1"
                    />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <label
                          htmlFor={`login-page-style-${option.value}`}
                          className={cn('font-semibold text-slate-950', !disabled && 'cursor-pointer')}
                        >
                          {option.name}
                        </label>
                        {isSelected && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700">
                            <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                            Default login page
                          </span>
                        )}
                      </div>
                      <p className="mt-1.5 text-sm leading-5 text-slate-600">
                        {option.description}
                      </p>
                      <p className="mt-2 text-xs font-medium text-slate-500">
                        {customBackgroundUrl ? 'Custom tenant background' : 'Bundled default background'}
                      </p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          disabled={disabled || isBusy}
                          asChild
                        >
                          <label
                            htmlFor={`login-background-upload-${option.value}`}
                            className={cn(!disabled && !isBusy && 'cursor-pointer')}
                          >
                            {isUploading ? (
                              <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
                            ) : (
                              <UploadCloud className="mr-2 h-4 w-4" aria-hidden="true" />
                            )}
                            {isUploading ? 'Uploading...' : 'Upload background'}
                          </label>
                        </Button>
                        <input
                          id={`login-background-upload-${option.value}`}
                          type="file"
                          accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp"
                          className="sr-only"
                          disabled={disabled || isBusy}
                          onChange={(event) => {
                            const file = event.target.files?.[0];
                            if (file) onBackgroundUpload(option.value, file);
                            event.target.value = '';
                          }}
                        />
                        {customBackgroundUrl && (
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            disabled={disabled || isBusy}
                            onClick={() => onBackgroundReset(option.value)}
                          >
                            {isResetting ? (
                              <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
                            ) : (
                              <RotateCcw className="mr-2 h-4 w-4" aria-hidden="true" />
                            )}
                            {isResetting ? 'Restoring...' : 'Restore default'}
                          </Button>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </RadioGroup>
        )}

        {!isLoading && !errorMessage && (
          <p className="mt-5 text-sm text-slate-500">
            This setting changes only the public login presentation. It does not
            change the authenticated application theme or sign users out.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
