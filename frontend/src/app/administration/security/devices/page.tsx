'use client';

import { DeviceManagement } from '../../../../components/security/DeviceManagement';

export default function DeviceManagementPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Device Management</h1>
          <p className="text-muted-foreground">
            Manage trusted devices and active sessions
          </p>
        </div>
      </div>
      
      <DeviceManagement />
    </div>
  );
}