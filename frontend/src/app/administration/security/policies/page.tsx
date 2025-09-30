'use client';

import { SecurityPolicies } from '../../../../components/security/SecurityPolicies';

export default function SecurityPoliciesPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Security Policies</h1>
          <p className="text-muted-foreground">
            Configure security policies and access controls
          </p>
        </div>
      </div>
      
      <SecurityPolicies />
    </div>
  );
}