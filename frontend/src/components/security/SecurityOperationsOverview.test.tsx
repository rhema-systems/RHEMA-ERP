import React from 'react'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({ getOperationsOverview: vi.fn() }))

vi.mock('@/services/security', () => ({
  securityService: { getOperationsOverview: mocks.getOperationsOverview },
}))

import { SecurityOperationsOverview } from './SecurityOperationsOverview'

const overview = {
  range: { key: '24h', startUtc: '2026-10-04T00:00:00Z', endUtc: '2026-10-05T00:00:00Z' },
  snapshot: {
    eligibleUsers: 10,
    activeUsers: 10,
    mfaEnabledUsers: 8,
    mfaAdoptionPercent: 80,
    privilegedUsers: 2,
    privilegedUsersWithoutMfa: 1,
    lockedAccounts: 1,
    activeSessions: 4,
    staleSessions: 1,
    disabledUsersWithActiveSessions: 0,
    sessionIdleThresholdMinutes: 30,
  },
  activity: {
    successfulLogins: 12,
    failedLogins: 3,
    loginFailureRatePercent: 20,
    passwordChanges: 1,
    securityEvents: 16,
    auditEvents: 9,
  },
  health: {
    score: 73,
    maximumScore: 100,
    modelVersion: '2026.10',
    factors: [{ key: 'mfa', name: 'MFA coverage', earnedPoints: 16, maximumPoints: 20, status: 'attention', evidence: '8 of 10 eligible users have MFA enabled.' }],
  },
  alerts: { openTotal: 0, critical: 0, high: 0, medium: 0, items: [] },
  recentEvents: [],
  privilegedUsers: [{
    userId: 'user-1', displayName: 'System Administrator', userName: 'admin', email: 'admin@example.test', roles: ['SuperAdmin'], privilegeLevel: 'System', isActive: true, mfaEnabled: false, isLocked: false, createdAtUtc: '2026-01-01T00:00:00Z', findings: ['MFA is not enabled.'],
  }],
  authenticationTrend: [{ periodStartUtc: '2026-10-05T00:00:00Z', label: 'Oct 5', successful: 12, failed: 3 }],
  configuration: { persisted: true, passwordMinimumLength: 8, failedAttemptThreshold: 5, lockoutMinutes: 30, sessionTimeoutMinutes: 30, accessTokenLifetimeMinutes: 60, concurrentLoginPolicy: 'Disabled', loginRateLimitingConfigured: true, captchaEnabled: false, captchaConfigured: false },
  retention: { configured: true, enabled: true, auditLogRetentionDays: 2555, securityLogRetentionDays: 365, lastRunSucceeded: true },
  generatedAtUtc: '2026-10-05T00:00:00Z',
}

function renderOverview() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}><SecurityOperationsOverview /></QueryClientProvider>)
}

describe('SecurityOperationsOverview', () => {
  beforeEach(() => mocks.getOperationsOverview.mockResolvedValue(overview))

  it('renders evidence-backed security values and the score explanation', async () => {
    renderOverview()

    expect(await screen.findByText('73 / 100')).toBeInTheDocument()
    expect(screen.getByText('8 of 10 eligible users have MFA enabled.')).toBeInTheDocument()
    expect(screen.getByText('System Administrator')).toBeInTheDocument()
    expect(screen.queryByText(/password compliance/i)).not.toBeInTheDocument()
  })

  it('uses an honest empty state for persisted alerts', async () => {
    renderOverview()
    expect(await screen.findByText('No open persisted alerts or detections.')).toBeInTheDocument()
  })
})
