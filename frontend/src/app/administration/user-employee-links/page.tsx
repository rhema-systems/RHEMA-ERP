'use client';

import NextLink from 'next/link';
import { Button } from '@/components/ui/button';
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
        <Button variant="outline" asChild>
          <NextLink href="/administration/user-employee-links/unlinked">Unlinked-users queue</NextLink>
        </Button>
      </div>
      <UserEmployeeLinks />
    </div>
  );
}
