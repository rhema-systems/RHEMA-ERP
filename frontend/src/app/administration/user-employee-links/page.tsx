'use client';

import UserEmployeeLinks from '../../../components/admin/UserEmployeeLinks';

export default function UserEmployeeLinksPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">User-Employee Links</h1>
          <p className="text-muted-foreground">
            Manage the connections between user accounts and employee records
          </p>
        </div>
      </div>
      <UserEmployeeLinks />
    </div>
  );
}
