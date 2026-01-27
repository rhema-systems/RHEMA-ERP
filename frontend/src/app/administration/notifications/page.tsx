'use client'

import React from 'react'
import NotificationMonitoring from '../../../components/notifications/NotificationMonitoring'

export default function NotificationMonitoringPage() {
  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold">Notification System Monitoring</h1>
        <p className="text-muted-foreground">
          Monitor notification queues, dead-letter messages, and system health
        </p>
      </div>

      {/* Notification Monitoring Component */}
      <NotificationMonitoring />
    </div>
  )
}
