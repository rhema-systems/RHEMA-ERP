'use client';

import { TwoFactorAuth } from '../../../../components/security/TwoFactorAuth';

export default function TwoFactorAuthPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Two-Factor Authentication</h1>
          <p className="text-muted-foreground">
            Secure your account with two-factor authentication
          </p>
        </div>
      </div>
      
      <TwoFactorAuth 
        userEmail="admin@company.com" 
        isEnabled={false}
        onStatusChange={(enabled) => console.log('2FA status changed:', enabled)}
      />
    </div>
  );
}