'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../../../components/layout/dashboard-layout';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../../components/ui/card';
import { Button } from '../../../../components/ui/button';
import { Input } from '../../../../components/ui/input';
import { Label } from '../../../../components/ui/label';
import { Switch } from '../../../../components/ui/switch';
import { Separator } from '../../../../components/ui/separator';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../../components/ui/select';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '../../../../components/ui/tabs';
import { adminApiService, PasswordPolicy } from '../../../../services/admin-api.service';
import { useToast } from '../../../../hooks/use-toast';
import { Shield, Lock, Clock, RefreshCw } from 'lucide-react';

export default function PasswordPolicyPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch password policy data
  const { data: passwordPolicy, isLoading } = useQuery({
    queryKey: ['password-policy'],
    queryFn: () => adminApiService.getPasswordPolicy(),
  });

  // Form state
  const [policy, setPolicy] = useState<PasswordPolicy>({
    minLength: 8,
    requireUppercase: true,
    requireLowercase: true,
    requireDigits: true,
    requireSpecialChars: true,
    maxAge: 90,
    preventReuse: 5
  });

  // Account settings state (for session management)
  const [sessionTimeout, setSessionTimeout] = useState(30);
  const [maxFailedAttempts, setMaxFailedAttempts] = useState(5);
  const [lockoutDuration, setLockoutDuration] = useState(15);
  const [enableCaptcha, setEnableCaptcha] = useState(false);

  // Update form when data loads
  React.useEffect(() => {
    if (passwordPolicy) {
      setPolicy(passwordPolicy);
    }
  }, [passwordPolicy]);

  // Update password policy mutation
  const updatePolicyMutation = useMutation({
    mutationFn: (updatedPolicy: PasswordPolicy) => 
      adminApiService.updatePasswordPolicy(updatedPolicy),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['password-policy'] });
      toast({
        title: 'Success',
        description: 'Password policy updated successfully',
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to update password policy',
        variant: 'destructive',
      });
    },
  });

  const handleSavePolicy = () => {
    updatePolicyMutation.mutate(policy);
  };

  const handleSaveAccountSettings = () => {
    // This would typically call an account settings API
    toast({
      title: 'Success',
      description: 'Account settings updated successfully',
    });
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Settings</h1>
          <p className="text-muted-foreground">
            Configure system security and account policies
          </p>
        </div>

        <Tabs defaultValue="password-policy" className="space-y-6">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="password-policy">Password policy</TabsTrigger>
            <TabsTrigger value="lockout">Lockout</TabsTrigger>
            <TabsTrigger value="identity-verification">Identity verification</TabsTrigger>
            <TabsTrigger value="sessions">Sessions</TabsTrigger>
          </TabsList>

          <TabsContent value="password-policy" className="space-y-6">
            <Card>
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Shield className="h-5 w-5 text-primary" />
                  <CardTitle>Password strength settings</CardTitle>
                </div>
                <CardDescription>
                  Configure password complexity requirements for user accounts
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="space-y-4">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                      <Label htmlFor="minLength">Required length</Label>
                      <Select
                        value={policy.minLength.toString()}
                        onValueChange={(value) => setPolicy({ ...policy, minLength: parseInt(value) })}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {Array.from({ length: 9 }, (_, i) => i + 3).map((length) => (
                            <SelectItem key={length} value={length.toString()}>
                              {length}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <p className="text-sm text-muted-foreground">
                        The minimum length a password must be.
                      </p>
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="uniqueChars">Required unique characters number</Label>
                      <Select
                        value="1"
                        onValueChange={() => {}}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="1">1</SelectItem>
                          <SelectItem value="2">2</SelectItem>
                          <SelectItem value="3">3</SelectItem>
                        </SelectContent>
                      </Select>
                      <p className="text-sm text-muted-foreground">
                        The minimum number of unique characters which a password must contain.
                      </p>
                    </div>
                  </div>

                  <Separator />

                  <div className="space-y-4">
                    <div className="flex items-center justify-between">
                      <div className="space-y-0.5">
                        <Label className="text-base">Required non-alphanumeric character</Label>
                        <p className="text-sm text-muted-foreground">
                          If passwords must contain a non-alphanumeric character.
                        </p>
                      </div>
                      <Switch
                        checked={policy.requireSpecialChars}
                        onCheckedChange={(checked) => 
                          setPolicy({ ...policy, requireSpecialChars: checked })
                        }
                      />
                    </div>

                    <div className="flex items-center justify-between">
                      <div className="space-y-0.5">
                        <Label className="text-base">Required lower case character</Label>
                        <p className="text-sm text-muted-foreground">
                          If passwords must contain a lower case ASCII character.
                        </p>
                      </div>
                      <Switch
                        checked={policy.requireLowercase}
                        onCheckedChange={(checked) => 
                          setPolicy({ ...policy, requireLowercase: checked })
                        }
                      />
                    </div>

                    <div className="flex items-center justify-between">
                      <div className="space-y-0.5">
                        <Label className="text-base">Required upper case character</Label>
                        <p className="text-sm text-muted-foreground">
                          If passwords must contain a upper case ASCII character.
                        </p>
                      </div>
                      <Switch
                        checked={policy.requireUppercase}
                        onCheckedChange={(checked) => 
                          setPolicy({ ...policy, requireUppercase: checked })
                        }
                      />
                    </div>

                    <div className="flex items-center justify-between">
                      <div className="space-y-0.5">
                        <Label className="text-base">Required digit</Label>
                        <p className="text-sm text-muted-foreground">
                          If passwords must contain a digit.
                        </p>
                      </div>
                      <Switch
                        checked={policy.requireDigits}
                        onCheckedChange={(checked) => 
                          setPolicy({ ...policy, requireDigits: checked })
                        }
                      />
                    </div>
                  </div>
                </div>

                <Separator />

                <Card>
                  <CardHeader>
                    <div className="flex items-center gap-2">
                      <RefreshCw className="h-4 w-4" />
                      <CardTitle className="text-lg">Password renewing settings</CardTitle>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="flex items-center justify-between">
                      <div className="space-y-0.5">
                        <Label className="text-base">Force users to periodically change password</Label>
                        <p className="text-sm text-muted-foreground">
                          Whether users are forced to periodically change their password.
                        </p>
                      </div>
                      <Switch
                        checked={!!policy.maxAge}
                        onCheckedChange={(checked) => 
                          setPolicy({ ...policy, maxAge: checked ? 90 : undefined })
                        }
                      />
                    </div>

                    {policy.maxAge && (
                      <div className="space-y-2">
                        <Label htmlFor="maxAge">Password change period (days)</Label>
                        <Input
                          id="maxAge"
                          type="number"
                          value={policy.maxAge || ''}
                          onChange={(e) => setPolicy({ 
                            ...policy, 
                            maxAge: parseInt(e.target.value) || undefined 
                          })}
                          className="w-32"
                        />
                        <p className="text-sm text-muted-foreground">
                          Number of days after which users must change their password.
                        </p>
                      </div>
                    )}

                    <div className="space-y-2">
                      <Label htmlFor="preventReuse">Prevent password reuse</Label>
                      <Input
                        id="preventReuse"
                        type="number"
                        value={policy.preventReuse || ''}
                        onChange={(e) => setPolicy({ 
                          ...policy, 
                          preventReuse: parseInt(e.target.value) || undefined 
                        })}
                        className="w-32"
                      />
                      <p className="text-sm text-muted-foreground">
                        Number of previous passwords to remember and prevent reuse.
                      </p>
                    </div>
                  </CardContent>
                </Card>

                <div className="flex justify-end">
                  <Button 
                    onClick={handleSavePolicy}
                    disabled={updatePolicyMutation.isPending}
                    className="flex items-center gap-2"
                  >
                    {updatePolicyMutation.isPending ? (
                      <>
                        <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                        Saving...
                      </>
                    ) : (
                      'Save Changes'
                    )}
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="lockout" className="space-y-6">
            <Card>
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Lock className="h-5 w-5 text-primary" />
                  <CardTitle>Account Lockout Settings</CardTitle>
                </div>
                <CardDescription>
                  Configure account security and lockout policies
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="space-y-2">
                    <Label htmlFor="maxFailedAttempts">Max Failed Login Attempts</Label>
                    <Input
                      id="maxFailedAttempts"
                      type="number"
                      value={maxFailedAttempts}
                      onChange={(e) => setMaxFailedAttempts(parseInt(e.target.value))}
                      min="1"
                      max="10"
                    />
                    <p className="text-sm text-muted-foreground">
                      Number of failed login attempts before account lockout
                    </p>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="lockoutDuration">Lockout Duration (minutes)</Label>
                    <Input
                      id="lockoutDuration"
                      type="number"
                      value={lockoutDuration}
                      onChange={(e) => setLockoutDuration(parseInt(e.target.value))}
                      min="1"
                      max="1440"
                    />
                    <p className="text-sm text-muted-foreground">
                      How long to lock accounts after failed attempts
                    </p>
                  </div>
                </div>

                <div className="flex items-center justify-between">
                  <div className="space-y-0.5">
                    <Label className="text-base">Enable CAPTCHA</Label>
                    <p className="text-sm text-muted-foreground">
                      Require CAPTCHA verification after failed login attempts
                    </p>
                  </div>
                  <Switch
                    checked={enableCaptcha}
                    onCheckedChange={setEnableCaptcha}
                  />
                </div>

                <div className="flex justify-end">
                  <Button onClick={handleSaveAccountSettings}>
                    Save Changes
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="identity-verification" className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Identity Verification</CardTitle>
                <CardDescription>
                  Configure identity verification and two-factor authentication
                </CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-muted-foreground">
                  Identity verification features will be implemented in a future update.
                </p>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="sessions" className="space-y-6">
            <Card>
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Clock className="h-5 w-5 text-primary" />
                  <CardTitle>Session Management</CardTitle>
                </div>
                <CardDescription>
                  Configure user session timeout and management
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="space-y-2">
                  <Label htmlFor="sessionTimeout">Session Timeout (minutes)</Label>
                  <Input
                    id="sessionTimeout"
                    type="number"
                    value={sessionTimeout}
                    onChange={(e) => setSessionTimeout(parseInt(e.target.value))}
                    min="5"
                    max="480"
                    className="w-32"
                  />
                  <p className="text-sm text-muted-foreground">
                    Automatically log out users after this period of inactivity
                  </p>
                </div>

                <div className="flex justify-end">
                  <Button onClick={handleSaveAccountSettings}>
                    Save Changes
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
  );
}
