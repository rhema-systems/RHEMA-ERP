'use client'

import React from 'react'
import SystemExceptionLogs from '../../../components/admin/SystemExceptionLogs'

export default function SystemExceptionLogsPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">System Logs</h1>
        <p className="text-muted-foreground">
          View and resolve exceptions captured by global error handling
        </p>
      </div>
      <SystemExceptionLogs />
    </div>
  )
}

