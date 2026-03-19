"use client"

import React from 'react'
import { useQuery } from '@tanstack/react-query'
import { Alert, AlertDescription } from '../ui/alert'
import { Badge } from '../ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Bot, CheckCircle2, Clock, FileText, KeyRound, Loader2, Lock, Shield, ShieldAlert } from 'lucide-react'
import { settingsService, type SecuritySettings } from '../../services/settings'
import { securityService } from '../../services/security'

const formatConcurrentLoginPolicy = (policy: SecuritySettings['preventConcurrentLogin']) => {
  switch (policy) {
    case 'LogoutFromAllDevices':
      return 'New logins terminate older sessions'
    case 'PreventSubsequentLogins':
      return 'Existing sessions block additional logins'
    default:
      return 'Multiple concurrent logins allowed'
  }
}

const formatBoolean = (value: boolean, positiveLabel = 'Enabled', negativeLabel = 'Disabled') =>
  value ? positiveLabel : negativeLabel

const formatPolicyConfiguration = (configuration: Record<string, unknown>) => {
  const entries = Object.entries(configuration).slice(0, 3)

  if (entries.length === 0) {
    return 'No additional configuration rules'
  }

  return entries
    .map(([key, value]) => `${key}: ${String(value)}`)
    .join(' • ')
}

const PolicyRow = ({
  label,
  value,
  tone = 'default',
}: {
  label: string
  value: string
  tone?: 'default' | 'good' | 'warning'
}) => {
  const toneClass =
    tone === 'good'
      ? 'text-emerald-700 dark:text-emerald-300'
      : tone === 'warning'
        ? 'text-amber-700 dark:text-amber-300'
        : 'text-slate-900 dark:text-slate-100'

  return (
    <div className="flex items-start justify-between gap-3 border-b border-slate-100 py-3 last:border-b-0 dark:border-slate-800">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className={`text-right text-sm font-medium ${toneClass}`}>{value}</span>
    </div>
  )
}

export const SecurityPolicies: React.FC = () => {
  const {
    data: settings,
    isLoading: settingsLoading,
    error: settingsError,
  } = useQuery({
    queryKey: ['securitySettings'],
    queryFn: () => settingsService.getSecuritySettings(),
  })

  const {
    data: policies,
    isLoading: policiesLoading,
    error: policiesError,
  } = useQuery({
    queryKey: ['securityPolicies'],
    queryFn: () => securityService.getSecurityPolicies(),
  })

  const isLoading = settingsLoading || policiesLoading
  const enabledPolicies = policies?.filter((policy) => policy.isEnabled) ?? []

  if (isLoading) {
    return (
      <div className="flex items-center justify-center rounded-xl border border-dashed border-slate-200 p-10 dark:border-slate-800">
        <Loader2 className="mr-3 h-5 w-5 animate-spin" />
        <span className="text-sm text-muted-foreground">Loading persisted security posture...</span>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 className="text-2xl font-bold">Security Policies</h2>
          <p className="text-muted-foreground">
            Live tenant controls and custom policy records pulled from persisted security data.
          </p>
        </div>
        <Badge variant="outline" className="w-fit border-emerald-200 text-emerald-700 dark:border-emerald-800 dark:text-emerald-300">
          <Shield className="mr-1 h-3 w-3" />
          Persisted Configuration
        </Badge>
      </div>

      {(settingsError || policiesError) && (
        <Alert variant="destructive">
          <ShieldAlert className="h-4 w-4" />
          <AlertDescription>
            Some security policy data could not be loaded from live sources. Settings and policy decisions should be
            reviewed after connectivity or permission issues are resolved.
          </AlertDescription>
        </Alert>
      )}

      <Alert>
        <CheckCircle2 className="h-4 w-4" />
        <AlertDescription>
          Global controls such as password rules, session handling, lockout, and CAPTCHA persist through the Security
          Settings tab. Custom tenant policies shown below are loaded from the database and reflect their latest saved
          state.
        </AlertDescription>
      </Alert>

      {settings && (
        <div className="grid gap-4 lg:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <KeyRound className="h-5 w-5" />
                Password & Access Baseline
              </CardTitle>
              <CardDescription>Current password strength posture and reuse controls.</CardDescription>
            </CardHeader>
            <CardContent>
              <PolicyRow label="Minimum password length" value={`${settings.passwordMinLength} characters`} tone={settings.passwordMinLength >= 12 ? 'good' : 'warning'} />
              <PolicyRow
                label="Complexity requirements"
                value={[
                  settings.passwordRequireUppercase && 'Uppercase',
                  settings.passwordRequireLowercase && 'Lowercase',
                  settings.passwordRequireDigits && 'Digits',
                  settings.passwordRequireSpecialChars && 'Special characters',
                ]
                  .filter(Boolean)
                  .join(', ') || 'None'}
                tone={settings.passwordRequireSpecialChars ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Password expiry"
                value={settings.passwordMaxAge ? `${settings.passwordMaxAge} days` : 'Never expires'}
                tone={settings.passwordMaxAge && settings.passwordMaxAge <= 90 ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Password history"
                value={settings.passwordPreventReuse ? `${settings.passwordPreventReuse} previous passwords blocked` : 'No history enforcement'}
                tone={settings.passwordPreventReuse && settings.passwordPreventReuse >= 5 ? 'good' : 'warning'}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Clock className="h-5 w-5" />
                Session Protection
              </CardTitle>
              <CardDescription>Current inactivity, token, and concurrency controls.</CardDescription>
            </CardHeader>
            <CardContent>
              <PolicyRow
                label="Session timeout"
                value={`${settings.sessionTimeoutMinutes} minutes`}
                tone={settings.sessionTimeoutMinutes <= 60 ? 'good' : 'warning'}
              />
              <PolicyRow
                label="JWT token lifetime"
                value={`${settings.jwtTokenLifetimeMinutes} minutes`}
                tone={settings.jwtTokenLifetimeMinutes <= 120 ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Concurrent login policy"
                value={formatConcurrentLoginPolicy(settings.preventConcurrentLogin)}
                tone={settings.preventConcurrentLogin === 'Disabled' ? 'warning' : 'good'}
              />
              <PolicyRow
                label="Account lockout"
                value={`${settings.maxFailedLoginAttempts} failed attempts, ${settings.accountLockoutMinutes} minute lockout`}
                tone={settings.maxFailedLoginAttempts <= 5 ? 'good' : 'warning'}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Lock className="h-5 w-5" />
                Login Abuse Defense
              </CardTitle>
              <CardDescription>Rate limiting and abuse controls on the authentication surface.</CardDescription>
            </CardHeader>
            <CardContent>
              <PolicyRow
                label="Rate limit attempts"
                value={`${settings.rateLimitLoginMaxAttempts} attempts per ${settings.rateLimitLoginWindowMinutes} minutes`}
                tone={settings.rateLimitLoginMaxAttempts <= 5 ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Rate limit block duration"
                value={`${settings.rateLimitLoginBlockDurationMinutes} minutes`}
                tone={settings.rateLimitLoginBlockDurationMinutes >= 15 ? 'good' : 'warning'}
              />
              <PolicyRow
                label="CAPTCHA protection"
                value={formatBoolean(settings.captchaEnabled, `${settings.captchaProvider} enabled`, 'Disabled')}
                tone={settings.captchaEnabled ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Challenge keys present"
                value={settings.captchaEnabled
                  ? formatBoolean(Boolean(settings.recaptchaSiteKey || settings.hCaptchaSiteKey), 'Public challenge key configured', 'Missing challenge key')
                  : 'Not required while CAPTCHA is disabled'}
                tone={settings.captchaEnabled && !(settings.recaptchaSiteKey || settings.hCaptchaSiteKey) ? 'warning' : 'default'}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Bot className="h-5 w-5" />
                Governance & Disclosure
              </CardTitle>
              <CardDescription>Supportive controls that influence user trust and compliance readiness.</CardDescription>
            </CardHeader>
            <CardContent>
              <PolicyRow
                label="Terms of service"
                value={formatBoolean(Boolean(settings.termsOfServiceUrl), 'Configured', 'Missing')}
                tone={settings.termsOfServiceUrl ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Privacy policy"
                value={formatBoolean(Boolean(settings.privacyPolicyUrl), 'Configured', 'Missing')}
                tone={settings.privacyPolicyUrl ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Public trust posture"
                value={settings.captchaEnabled || settings.privacyPolicyUrl ? 'Public-facing controls present' : 'Minimal public hardening'}
                tone={settings.captchaEnabled && settings.privacyPolicyUrl ? 'good' : 'warning'}
              />
              <PolicyRow
                label="Saved custom policies"
                value={`${enabledPolicies.length} enabled of ${(policies ?? []).length} total`}
                tone={enabledPolicies.length > 0 ? 'good' : 'warning'}
              />
            </CardContent>
          </Card>
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Tenant Security Policy Records
            <Badge variant="secondary">{policies?.length ?? 0}</Badge>
          </CardTitle>
          <CardDescription>
            Custom security policies currently stored for this tenant.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {policies && policies.length > 0 ? (
            policies.map((policy) => (
              <div key={policy.id} className="rounded-xl border border-slate-200 p-4 dark:border-slate-800">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <h3 className="font-semibold">{policy.name}</h3>
                      <Badge variant={policy.isEnabled ? 'default' : 'secondary'}>
                        {policy.isEnabled ? 'Enabled' : 'Disabled'}
                      </Badge>
                      <Badge variant="outline">{policy.policyType}</Badge>
                    </div>
                    <p className="text-sm text-muted-foreground">
                      {policy.description || 'No description provided.'}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Last changed by {policy.modifiedBy} on {policy.lastModified.toLocaleString()}
                    </p>
                  </div>
                  <div className="max-w-xl text-sm text-muted-foreground">
                    {formatPolicyConfiguration(policy.configuration)}
                  </div>
                </div>
              </div>
            ))
          ) : (
            <div className="rounded-xl border border-dashed border-slate-200 p-8 text-center dark:border-slate-800">
              <Shield className="mx-auto mb-3 h-10 w-10 text-muted-foreground" />
              <h3 className="font-medium">No custom tenant policies found</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                The application is currently relying on its persisted global security settings without additional tenant
                policy records.
              </p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
