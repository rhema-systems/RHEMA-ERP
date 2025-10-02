'use client';

import { useEffect, useState } from 'react';
import { TwoFactorAuth } from '../../../../components/security/TwoFactorAuth';
import { Alert, AlertDescription } from '../../../../components/ui/alert';
import { AlertTriangle } from 'lucide-react';

export default function TwoFactorAuthPage() {
  const [userEmail, setUserEmail] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Get user info from localStorage or token
    try {
      const userInfo = localStorage.getItem('userInfo');
      if (userInfo) {
        const user = JSON.parse(userInfo);
        setUserEmail(user.email || 'user@example.com');
      } else {
        setUserEmail('user@example.com');
      }
    } catch (err) {
      console.error('Error loading user info:', err);
      setError('Failed to load user information');
      setUserEmail('user@example.com');
    }
  }, []);

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
      
      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      
      <TwoFactorAuth 
        userEmail={userEmail}
        onStatusChange={(enabled) => console.log('2FA status changed:', enabled)}
      />
    </div>
  );
}
