'use client'

import Link from 'next/link'
import React, { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock3,
  ExternalLink,
  KeyRound,
  Loader2,
  LockKeyhole,
  MonitorSmartphone,
  RefreshCw,
  ShieldCheck,
  UserCog,
  Users,
} from 'lucide-react'
import { securityService, type SecurityRange } from '@/services/security'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const rangeLabels: Record<SecurityRange, string> = {
  '24h': 'Last 24 hours',
  '7d': 'Last 7 days',
  '30d': 'Last 30 days',
}

const severityClasses: Record<string, string> = {
  critical: 'border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300',
  high: 'border-orange-200 bg-orange-50 text-orange-700 dark:border-orange-900 dark:bg-orange-950/40 dark:text-orange-300',
  medium: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300',
  low: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-300',
  informational: 'border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-300',
}

function formatTimestamp(value?: string) {
  if (!value) return 'Not recorded'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? 'Not recorded' : parsed.toLocaleString()
}

function MetricCard({
  title,
  value,
  detail,
  icon: Icon,
  tone = 'blue',
}: {
  title: string
  value: string | number
  detail: string
  icon: typeof ShieldCheck
  tone?: 'blue' | 'green' | 'amber' | 'red' | 'violet'
}) {
  const tones = {
    blue: 'bg-blue-50 text-blue-700 dark:bg-blue-950/50 dark:text-blue-300',
    green: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300',
    amber: 'bg-amber-50 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300',
    red: 'bg-red-50 text-red-700 dark:bg-red-950/50 dark:text-red-300',
    violet: 'bg-violet-50 text-violet-700 dark:bg-violet-950/50 dark:text-violet-300',
  }

  return (
    <Card className="overflow-hidden">
      <CardContent className="flex items-start justify-between p-5">
        <div className="min-w-0">
          <p className="text-sm font-medium text-muted-foreground">{title}</p>
          <p className="mt-2 text-3xl font-semibold tracking-tight">{value}</p>
          <p className="mt-2 text-xs leading-5 text-muted-foreground">{detail}</p>
        </div>
        <span className={`rounded-xl p-2.5 ${tones[tone]}`}><Icon className="h-5 w-5" /></span>
      </CardContent>
    </Card>
  )
}

export function SecurityOperationsOverview() {
  const [range, setRange] = useState<SecurityRange>('24h')
  const query = useQuery({
    queryKey: ['securityOperationsOverview', range],
    queryFn: () => securityService.getOperationsOverview(range),
    refetchInterval: 60_000,
  })

  if (query.isLoading) {
    return (
      <div className="flex min-h-[420px] items-center justify-center rounded-xl border bg-card">
        <div className="text-center text-muted-foreground">
          <Loader2 className="mx-auto mb-3 h-8 w-8 animate-spin" />
          Loading tenant security evidence…
        </div>
      </div>
    )
  }

  if (query.isError || !query.data) {
    return (
      <Alert variant="destructive">
        <AlertTriangle className="h-4 w-4" />
        <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
          <span>Security operations data could not be loaded. No fallback or sample values are shown.</span>
          <Button size="sm" variant="outline" onClick={() => query.refetch()}>Try again</Button>
        </AlertDescription>
      </Alert>
    )
  }

  const data = query.data
  const score = data.health.score
  const scoreTone = score >= 85 ? 'text-emerald-600' : score >= 65 ? 'text-amber-600' : 'text-red-600'
  const maximumTrend = Math.max(
    1,
    ...data.authenticationTrend.flatMap(point => [point.successful, point.failed])
  )

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 rounded-xl border bg-card p-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="font-semibold">Security Operations Overview</p>
          <p className="text-sm text-muted-foreground">
            Tenant-scoped authentication, access, session, configuration, and audit evidence.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Select value={range} onValueChange={value => setRange(value as SecurityRange)}>
            <SelectTrigger className="w-[160px]" aria-label="Security analytics time range">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(Object.keys(rangeLabels) as SecurityRange[]).map(value => (
                <SelectItem value={value} key={value}>{rangeLabels[value]}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="icon"
            aria-label="Refresh security operations data"
            onClick={() => query.refetch()}
            disabled={query.isFetching}
          >
            <RefreshCw className={`h-4 w-4 ${query.isFetching ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          title="Security health"
          value={`${score} / ${data.health.maximumScore}`}
          detail={`Explainable control model ${data.health.modelVersion}`}
          icon={ShieldCheck}
          tone={score >= 85 ? 'green' : score >= 65 ? 'amber' : 'red'}
        />
        <MetricCard
          title="Failed logins"
          value={data.activity.failedLogins}
          detail={`${data.activity.loginFailureRatePercent}% of recorded login attempts in ${rangeLabels[range].toLowerCase()}`}
          icon={LockKeyhole}
          tone={data.activity.failedLogins > 0 ? 'amber' : 'green'}
        />
        <MetricCard
          title="Active sessions"
          value={data.snapshot.activeSessions}
          detail={`${data.snapshot.staleSessions} stale after ${data.snapshot.sessionIdleThresholdMinutes} minutes`}
          icon={MonitorSmartphone}
          tone={data.snapshot.staleSessions > 0 ? 'amber' : 'blue'}
        />
        <MetricCard
          title="Open security signals"
          value={data.alerts.openTotal}
          detail={`${data.alerts.critical} critical · ${data.alerts.high} high`}
          icon={AlertTriangle}
          tone={data.alerts.critical > 0 ? 'red' : data.alerts.high > 0 ? 'amber' : 'green'}
        />
        <MetricCard
          title="MFA adoption"
          value={`${data.snapshot.mfaAdoptionPercent}%`}
          detail={`${data.snapshot.mfaEnabledUsers} of ${data.snapshot.eligibleUsers} active eligible users`}
          icon={KeyRound}
          tone={data.snapshot.mfaAdoptionPercent >= 90 ? 'green' : 'amber'}
        />
        <MetricCard
          title="Privileged access"
          value={data.snapshot.privilegedUsers}
          detail={`${data.snapshot.privilegedUsersWithoutMfa} privileged users without MFA`}
          icon={UserCog}
          tone={data.snapshot.privilegedUsersWithoutMfa > 0 ? 'red' : 'violet'}
        />
        <MetricCard
          title="Locked accounts"
          value={data.snapshot.lockedAccounts}
          detail="Current account snapshot"
          icon={Users}
          tone={data.snapshot.lockedAccounts > 0 ? 'amber' : 'green'}
        />
        <MetricCard
          title="Security events"
          value={data.activity.securityEvents}
          detail={`${data.activity.auditEvents} audit events in the same period`}
          icon={Activity}
          tone="blue"
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.05fr_1.6fr]">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between gap-3">
              <span>Health score evidence</span>
              <span className={`text-2xl ${scoreTone}`}>{score}</span>
            </CardTitle>
            <CardDescription>Every point is tied to the evidence shown below.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {data.health.factors.map(factor => (
              <div key={factor.key} className="space-y-1.5">
                <div className="flex items-center justify-between gap-3 text-sm">
                  <span className="font-medium">{factor.name}</span>
                  <span>{factor.earnedPoints}/{factor.maximumPoints}</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-muted">
                  <div
                    className={factor.status === 'healthy' ? 'h-full bg-emerald-500' : factor.status === 'attention' ? 'h-full bg-amber-500' : 'h-full bg-red-500'}
                    style={{ width: `${factor.maximumPoints ? (factor.earnedPoints / factor.maximumPoints) * 100 : 0}%` }}
                  />
                </div>
                <p className="text-xs leading-5 text-muted-foreground">{factor.evidence}</p>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div>
              <CardTitle>Authentication activity</CardTitle>
              <CardDescription>Successful and failed login records from the security log.</CardDescription>
            </div>
            <Badge variant="outline">{rangeLabels[range]}</Badge>
          </CardHeader>
          <CardContent>
            {data.authenticationTrend.length === 0 ? (
              <div className="flex min-h-52 flex-col items-center justify-center text-center text-muted-foreground">
                <Activity className="mb-3 h-8 w-8" />
                <p className="font-medium text-foreground">No authentication events in this period</p>
                <p className="mt-1 text-sm">The chart will populate when login events are recorded.</p>
              </div>
            ) : (
              <div className="space-y-3">
                {data.authenticationTrend.map(point => (
                  <div key={point.periodStartUtc} className="grid grid-cols-[72px_1fr_42px] items-center gap-3 text-xs">
                    <span className="text-muted-foreground">{point.label}</span>
                    <div className="space-y-1">
                      <div className="h-2 rounded-full bg-muted">
                        <div className="h-2 rounded-full bg-emerald-500" style={{ width: `${(point.successful / maximumTrend) * 100}%` }} />
                      </div>
                      <div className="h-2 rounded-full bg-muted">
                        <div className="h-2 rounded-full bg-red-500" style={{ width: `${(point.failed / maximumTrend) * 100}%` }} />
                      </div>
                    </div>
                    <span className="text-right text-muted-foreground">{point.successful}/{point.failed}</span>
                  </div>
                ))}
                <div className="flex gap-4 border-t pt-3 text-xs text-muted-foreground">
                  <span><i className="mr-1 inline-block h-2 w-2 rounded-full bg-emerald-500" />Successful</span>
                  <span><i className="mr-1 inline-block h-2 w-2 rounded-full bg-red-500" />Failed</span>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div>
              <CardTitle>Open alerts and detections</CardTitle>
              <CardDescription>Persisted records only. Empty means no records were found.</CardDescription>
            </div>
            <Badge variant={data.alerts.critical > 0 ? 'destructive' : 'secondary'}>{data.alerts.openTotal} open</Badge>
          </CardHeader>
          <CardContent className="space-y-3">
            {data.alerts.items.length === 0 ? (
              <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">
                <CheckCircle2 className="mx-auto mb-2 h-7 w-7 text-emerald-600" />
                No open persisted alerts or detections.
              </div>
            ) : data.alerts.items.map(item => (
              <div key={`${item.source}-${item.id}`} className="rounded-lg border p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="outline" className={severityClasses[item.severity] ?? severityClasses.informational}>{item.severity}</Badge>
                  <Badge variant="outline">{item.status}</Badge>
                  <span className="ml-auto text-xs text-muted-foreground">{formatTimestamp(item.timestampUtc)}</span>
                </div>
                <p className="mt-2 font-medium">{item.title}</p>
                <p className="mt-1 text-sm text-muted-foreground">{item.description}</p>
                {(item.userName || item.ipAddress) && (
                  <p className="mt-2 text-xs text-muted-foreground">{item.userName ?? 'Unknown user'}{item.ipAddress ? ` · ${item.ipAddress}` : ''}</p>
                )}
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div>
              <CardTitle>Recent security activity</CardTitle>
              <CardDescription>Authentication and security-relevant administration evidence.</CardDescription>
            </div>
            <Link className="inline-flex items-center gap-1 text-sm font-medium text-primary" href="/administration/identity-management/security-logs">
              Security logs <ExternalLink className="h-3.5 w-3.5" />
            </Link>
          </CardHeader>
          <CardContent>
            {data.recentEvents.length === 0 ? (
              <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">No security activity was recorded in this period.</div>
            ) : (
              <ol className="space-y-4">
                {data.recentEvents.map(event => (
                  <li key={`${event.source}-${event.id}`} className="relative border-l pl-4">
                    <span className="absolute -left-1.5 top-1 h-3 w-3 rounded-full border-2 border-background bg-primary" />
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-medium">{event.title}</p>
                      <Badge variant="outline" className={severityClasses[event.severity] ?? severityClasses.informational}>{event.severity}</Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">{formatTimestamp(event.timestampUtc)} · {event.source}{event.userName ? ` · ${event.userName}` : ''}</p>
                    <p className="mt-1 text-xs text-muted-foreground">{event.evidence}</p>
                  </li>
                ))}
              </ol>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div>
            <CardTitle>Privileged access review</CardTitle>
            <CardDescription>Administrative role assignments, MFA, account state, and last-login evidence.</CardDescription>
          </div>
          <Badge variant="outline">{data.privilegedUsers.length} shown</Badge>
        </CardHeader>
        <CardContent className="overflow-x-auto">
          {data.privilegedUsers.length === 0 ? (
            <div className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">No privileged users were found in this tenant.</div>
          ) : (
            <table className="w-full min-w-[820px] text-sm">
              <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                <tr><th className="pb-3 pr-4">User</th><th className="pb-3 pr-4">Roles</th><th className="pb-3 pr-4">MFA</th><th className="pb-3 pr-4">Status</th><th className="pb-3 pr-4">Last login</th><th className="pb-3">Findings</th></tr>
              </thead>
              <tbody className="divide-y">
                {data.privilegedUsers.map(user => (
                  <tr key={user.userId}>
                    <td className="py-3 pr-4"><p className="font-medium">{user.displayName || user.userName}</p><p className="text-xs text-muted-foreground">{user.email}</p></td>
                    <td className="py-3 pr-4"><div className="flex flex-wrap gap-1">{user.roles.map(role => <Badge variant="outline" key={role}>{role}</Badge>)}</div></td>
                    <td className="py-3 pr-4"><Badge variant={user.mfaEnabled ? 'secondary' : 'destructive'}>{user.mfaEnabled ? 'Enabled' : 'Missing'}</Badge></td>
                    <td className="py-3 pr-4"><Badge variant={user.isActive && !user.isLocked ? 'outline' : 'destructive'}>{user.isLocked ? 'Locked' : user.isActive ? 'Active' : 'Inactive'}</Badge></td>
                    <td className="py-3 pr-4 text-muted-foreground">{formatTimestamp(user.lastLoginUtc)}</td>
                    <td className="py-3 text-xs text-muted-foreground">{user.findings.length ? user.findings.join(' ') : 'No review findings.'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Authentication and session controls</CardTitle><CardDescription>Persisted tenant configuration, without secret values.</CardDescription></CardHeader>
          <CardContent className="grid gap-3 sm:grid-cols-2">
            {[
              ['Password minimum', data.configuration.persisted ? `${data.configuration.passwordMinimumLength} characters` : 'Unavailable'],
              ['Lockout threshold', data.configuration.persisted ? `${data.configuration.failedAttemptThreshold} attempts` : 'Unavailable'],
              ['Lockout duration', data.configuration.persisted ? `${data.configuration.lockoutMinutes} minutes` : 'Unavailable'],
              ['Session timeout', data.configuration.persisted ? `${data.configuration.sessionTimeoutMinutes} minutes` : 'Unavailable'],
              ['Access token lifetime', data.configuration.persisted ? `${data.configuration.accessTokenLifetimeMinutes} minutes` : 'Unavailable'],
              ['Concurrent logins', data.configuration.concurrentLoginPolicy],
              ['Login rate limiting', data.configuration.loginRateLimitingConfigured ? 'Configured' : 'Missing'],
              ['CAPTCHA', data.configuration.captchaEnabled ? (data.configuration.captchaConfigured ? 'Enabled and configured' : 'Enabled, configuration incomplete') : 'Disabled'],
            ].map(([label, value]) => <div className="rounded-lg border p-3" key={label}><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 font-medium">{value}</p></div>)}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4"><div><CardTitle>Retention evidence</CardTitle><CardDescription>Tenant retention policy and last persisted cleanup run.</CardDescription></div><Link className="inline-flex items-center gap-1 text-sm font-medium text-primary" href="/administration/security/retention">Manage <ExternalLink className="h-3.5 w-3.5" /></Link></CardHeader>
          <CardContent className="space-y-3">
            {!data.retention.configured ? <div className="rounded-lg border border-dashed p-5 text-sm text-muted-foreground">No persisted retention policy was found for this tenant.</div> : <>
              <div className="grid gap-3 sm:grid-cols-2"><div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Audit logs</p><p className="mt-1 font-medium">{data.retention.auditLogRetentionDays} days</p></div><div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Security logs</p><p className="mt-1 font-medium">{data.retention.securityLogRetentionDays} days</p></div></div>
              <div className="rounded-lg border p-3"><div className="flex items-center gap-2"><Clock3 className="h-4 w-4 text-muted-foreground" /><p className="font-medium">Last cleanup run</p></div><p className="mt-2 text-sm text-muted-foreground">{formatTimestamp(data.retention.lastRunStartedAtUtc)} · {data.retention.lastRunSucceeded == null ? 'No result recorded' : data.retention.lastRunSucceeded ? 'Succeeded' : 'Failed'}</p></div>
            </>}
          </CardContent>
        </Card>
      </div>

      <p className="text-right text-xs text-muted-foreground">Generated {formatTimestamp(data.generatedAtUtc)} · Snapshot values are labeled separately from {rangeLabels[range].toLowerCase()} activity.</p>
    </div>
  )
}
