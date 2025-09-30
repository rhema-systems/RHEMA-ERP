'use client';

import { AuditLog } from '../../../../components/security/AuditLog';

export default function AuditMonitoringPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Audit & Monitoring</h1>
          <p className="text-muted-foreground">
            Monitor security events and audit system activity
          </p>
        </div>
      </div>
      
      <AuditLog />
    </div>
  );
}