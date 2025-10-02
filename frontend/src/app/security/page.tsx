'use client';

import { useEffect, useState } from 'react';
import { TwoFactorAuth } from '../../components/security/TwoFactorAuth';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card';
import { Alert, AlertDescription } from '../../components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { 
  Shield, 
  Key, 
  AlertTriangle, 
  Info,
  Lock,
  User,
  Settings
} from 'lucide-react';

export default function SecurityPage() {
  const [userEmail, setUserEmail] = useState<string>('');
  const [userName, setUserName] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [twoFactorEnabled, setTwoFactorEnabled] = useState<boolean>(false);

  useEffect(() => {
    // Get user info from localStorage or token
    try {
      const userInfo = localStorage.getItem('userInfo');
      if (userInfo) {
        const user = JSON.parse(userInfo);
        setUserEmail(user.email || 'user@example.com');
        setUserName(user.username || user.firstName || 'User');
      } else {
        // Try to get from JWT token or make API call
        setUserEmail('user@example.com');
        setUserName('User');
      }
    } catch (err) {
      console.error('Error loading user info:', err);
      setError('Failed to load user information');
      setUserEmail('user@example.com');
      setUserName('User');
    }
  }, []);

  return (
    <div className="container mx-auto p-6 space-y-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <Shield className="h-8 w-8" />
            Security Settings
          </h1>
          <p className="text-muted-foreground mt-2">
            Manage your account security and authentication settings
          </p>
        </div>
        <Badge variant={twoFactorEnabled ? "default" : "secondary"} className="text-sm">
          {twoFactorEnabled ? '2FA Enabled' : '2FA Disabled'}
        </Badge>
      </div>
      
      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      
      <Tabs defaultValue="2fa" className="w-full">
        <TabsList className="grid w-full grid-cols-3">
          <TabsTrigger value="2fa" className="flex items-center gap-2">
            <Key className="h-4 w-4" />
            Two-Factor Auth
          </TabsTrigger>
          <TabsTrigger value="password" className="flex items-center gap-2">
            <Lock className="h-4 w-4" />
            Password
          </TabsTrigger>
          <TabsTrigger value="profile" className="flex items-center gap-2">
            <User className="h-4 w-4" />
            Profile
          </TabsTrigger>
        </TabsList>
        
        <TabsContent value="2fa" className="space-y-6">
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Shield className="h-5 w-5" />
              <h2 className="text-xl font-semibold">Two-Factor Authentication</h2>
            </div>
            <p className="text-muted-foreground">
              Add an extra layer of security to your account with two-factor authentication.
            </p>
          </div>
          
          <TwoFactorAuth 
            userEmail={userEmail}
            onStatusChange={(enabled) => {
              setTwoFactorEnabled(enabled);
              console.log('2FA status changed:', enabled);
            }}
          />
        </TabsContent>
        
        <TabsContent value="password" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Lock className="h-5 w-5" />
                Password Settings
              </CardTitle>
            </CardHeader>
            <CardContent>
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  Password management features are coming soon. For now, please contact your administrator to change your password.
                </AlertDescription>
              </Alert>
            </CardContent>
          </Card>
        </TabsContent>
        
        <TabsContent value="profile" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <User className="h-5 w-5" />
                Profile Security
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Username</label>
                    <p className="font-medium">{userName}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Email</label>
                    <p className="font-medium">{userEmail}</p>
                  </div>
                </div>
                
                <Alert>
                  <Info className="h-4 w-4" />
                  <AlertDescription>
                    Profile management features are coming soon. Contact your administrator for profile changes.
                  </AlertDescription>
                </Alert>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
