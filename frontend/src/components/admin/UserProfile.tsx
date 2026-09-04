'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { ConfirmationDialog } from '../ui/confirmation-dialog';
import { Input } from '../ui/input';
import { Label } from '../ui/label';
import { Textarea } from '../ui/textarea';
import { Badge } from '../ui/badge';
import { Avatar, AvatarFallback, AvatarImage } from '../ui/avatar';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '../ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import {
  User,
  Mail,
  Phone,
  Calendar,
  MapPin,
  Clock,
  Shield,
  Activity,
  Settings,
  History,
  Bell,
  Key,
  Eye,
  UserCheck
} from 'lucide-react';
import {
  ResetUserPasswordRequest,
  User as UserType,
} from '../../services/admin-api.service';
import { cn } from '../../lib/utils';

interface UserProfileProps {
  user: UserType;
  onClose: () => void;
  onEdit?: (user: UserType) => void;
  onImpersonate?: (userId: string) => void;
  onResetPassword?: (
    userId: string,
    request: ResetUserPasswordRequest
  ) => Promise<void>;
  onSendWelcomeEmail?: (userId: string) => void;
}

interface ActivityLog {
  id: string;
  action: string;
  timestamp: Date;
  ipAddress: string;
  location?: string;
  device?: string;
  success: boolean;
}

interface UserSession {
  id: string;
  startTime: Date;
  lastActivity: Date;
  ipAddress: string;
  userAgent: string;
  isActive: boolean;
}

interface UserPreferences {
  theme: 'light' | 'dark' | 'system';
  language: string;
  timezone: string;
  emailNotifications: boolean;
  pushNotifications: boolean;
  twoFactorEnabled: boolean;
}

// Mock data - in real app, this would come from API
const generateMockActivityLogs = (): ActivityLog[] => [
  {
    id: '1',
    action: 'User login',
    timestamp: new Date(Date.now() - 1000 * 60 * 30), // 30 min ago
    ipAddress: '192.168.1.100',
    location: 'Accra, Ghana',
    device: 'Chrome on Windows',
    success: true
  },
  {
    id: '2',
    action: 'Password change',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2), // 2 hours ago
    ipAddress: '192.168.1.100',
    location: 'Accra, Ghana',
    device: 'Chrome on Windows',
    success: true
  },
  {
    id: '3',
    action: 'Failed login attempt',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24), // 1 day ago
    ipAddress: '45.123.45.67',
    location: 'Unknown',
    device: 'Mobile Safari',
    success: false
  }
];

const generateMockSessions = (): UserSession[] => [
  {
    id: '1',
    startTime: new Date(Date.now() - 1000 * 60 * 30),
    lastActivity: new Date(Date.now() - 1000 * 60 * 5),
    ipAddress: '192.168.1.100',
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
    isActive: true
  },
  {
    id: '2',
    startTime: new Date(Date.now() - 1000 * 60 * 60 * 2),
    lastActivity: new Date(Date.now() - 1000 * 60 * 60),
    ipAddress: '10.0.0.50',
    userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X)',
    isActive: false
  }
];

const mockPreferences: UserPreferences = {
  theme: 'system',
  language: 'en',
  timezone: 'UTC',
  emailNotifications: true,
  pushNotifications: false,
  twoFactorEnabled: false
};

export const validateTemporaryPasswordReset = (
  newPassword: string,
  confirmation: string,
  reason: string
) => {
  if (newPassword.length < 12)
    return 'Temporary password must be at least 12 characters.';
  if (!/[A-Z]/.test(newPassword))
    return 'Temporary password must include an uppercase letter.';
  if (!/[a-z]/.test(newPassword))
    return 'Temporary password must include a lowercase letter.';
  if (!/[0-9]/.test(newPassword))
    return 'Temporary password must include a number.';
  if (!/[^A-Za-z0-9]/.test(newPassword))
    return 'Temporary password must include a special character.';
  if (newPassword !== confirmation)
    return 'Temporary password and confirmation must match.';
  if (reason.trim().length < 10)
    return 'Enter an administrative reason of at least 10 characters.';
  return undefined;
};

export function UserProfile({
  user,
  onClose,
  onEdit,
  onImpersonate,
  onResetPassword,
  onSendWelcomeEmail
}: UserProfileProps) {
  const [activeTab, setActiveTab] = useState('overview');
  const [activityLogs] = useState<ActivityLog[]>(generateMockActivityLogs());
  const [sessions] = useState<UserSession[]>(generateMockSessions());
  const [preferences] = useState<UserPreferences>(mockPreferences);
  const [resetOpen, setResetOpen] = useState(false);
  const [newTemporaryPassword, setNewTemporaryPassword] = useState('');
  const [confirmTemporaryPassword, setConfirmTemporaryPassword] = useState('');
  const [resetReason, setResetReason] = useState('');
  const [resetError, setResetError] = useState('');
  const [resettingPassword, setResettingPassword] = useState(false);

  const clearResetSecret = () => {
    setNewTemporaryPassword('');
    setConfirmTemporaryPassword('');
    setResetReason('');
    setResetError('');
  };

  const changeResetOpen = (open: boolean) => {
    if (!open && resettingPassword) return;
    setResetOpen(open);
    if (!open) clearResetSecret();
  };

  const resetPassword = async () => {
    const validation = validateTemporaryPasswordReset(
      newTemporaryPassword,
      confirmTemporaryPassword,
      resetReason
    );
    if (validation) {
      setResetError(validation);
      return false;
    }
    if (!onResetPassword) {
      setResetError('Password reset is unavailable for this account.');
      return false;
    }

    try {
      setResettingPassword(true);
      setResetError('');
      await onResetPassword(user.id, {
        newPassword: newTemporaryPassword,
        reason: resetReason.trim(),
      });
      clearResetSecret();
      return true;
    } catch (error) {
      setResetError(
        error instanceof Error
          ? error.message
          : 'The temporary password could not be set.'
      );
      return false;
    } finally {
      setResettingPassword(false);
    }
  };

  const getInitials = (firstName?: string, lastName?: string, username?: string) => {
    if (firstName && lastName) {
      return `${firstName[0]}${lastName[0]}`.toUpperCase();
    }
    if (username) {
      return username.slice(0, 2).toUpperCase();
    }
    return 'U';
  };

  const formatDate = (date: Date) => {
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(date);
  };

  const getTimeAgo = (date: Date) => {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / (1000 * 60));
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));

    if (minutes < 60) return `${minutes} minutes ago`;
    if (hours < 24) return `${hours} hours ago`;
    return `${days} days ago`;
  };

  return (
    <>
    <div className="max-w-4xl max-h-[90vh] overflow-hidden">
      <div className="pb-4 border-b">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <Avatar className="h-12 w-12">
              <AvatarImage src={`https://api.dicebear.com/7.x/initials/svg?seed=${user.username}`} />
              <AvatarFallback>
                {getInitials(user.firstName, user.lastName, user.username)}
              </AvatarFallback>
            </Avatar>
            <div>
              <h3 className="text-xl font-semibold">
                {user.firstName && user.lastName 
                  ? `${user.firstName} ${user.lastName}` 
                  : user.username}
              </h3>
              <div className="flex items-center space-x-2 text-sm text-muted-foreground">
                <span>{user.email}</span>
                <Badge variant={user.isActive ? 'default' : 'secondary'}>
                  {user.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </div>
            </div>
          </div>

          <div className="flex items-center space-x-2">
            {onEdit && (
              <Button variant="outline" size="sm" onClick={() => onEdit(user)}>
                <Settings className="h-4 w-4 mr-2" />
                Edit
              </Button>
            )}
            {onImpersonate && (
              <Button variant="outline" size="sm" onClick={() => onImpersonate(user.id)}>
                <UserCheck className="h-4 w-4 mr-2" />
                Impersonate
              </Button>
            )}
          </div>
        </div>
      </div>

        <div className="flex-1 overflow-hidden">
          <Tabs value={activeTab} onValueChange={setActiveTab} className="h-full">
            <TabsList className="grid w-full grid-cols-4">
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="activity">Activity</TabsTrigger>
              <TabsTrigger value="sessions">Sessions</TabsTrigger>
              <TabsTrigger value="preferences">Preferences</TabsTrigger>
            </TabsList>

            <div className="mt-4 overflow-y-auto max-h-96">
              <TabsContent value="overview" className="space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Personal Information */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <User className="h-5 w-5 mr-2" />
                        Personal Information
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="flex items-center space-x-3">
                        <Mail className="h-4 w-4 text-muted-foreground" />
                        <div>
                          <div className="text-sm font-medium">Email</div>
                          <div className="text-sm text-muted-foreground">{user.email}</div>
                        </div>
                      </div>
                      
                      {user.phoneNumber && (
                        <div className="flex items-center space-x-3">
                          <Phone className="h-4 w-4 text-muted-foreground" />
                          <div>
                            <div className="text-sm font-medium">Phone</div>
                            <div className="text-sm text-muted-foreground">{user.phoneNumber}</div>
                          </div>
                        </div>
                      )}
                      
                      <div className="flex items-center space-x-3">
                        <Calendar className="h-4 w-4 text-muted-foreground" />
                        <div>
                          <div className="text-sm font-medium">Member Since</div>
                          <div className="text-sm text-muted-foreground">
                            {formatDate(new Date(user.createdAt))}
                          </div>
                        </div>
                      </div>
                      
                      {user.lastLoginAt && (
                        <div className="flex items-center space-x-3">
                          <Clock className="h-4 w-4 text-muted-foreground" />
                          <div>
                            <div className="text-sm font-medium">Last Login</div>
                            <div className="text-sm text-muted-foreground">
                              {formatDate(new Date(user.lastLoginAt))}
                            </div>
                          </div>
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  {/* Roles & Permissions */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Shield className="h-5 w-5 mr-2" />
                        Roles & Permissions
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div>
                        <div className="text-sm font-medium mb-2">Assigned Roles</div>
                        <div className="flex flex-wrap gap-2">
                          {user.roles.map((role) => (
                            <Badge key={role} variant="secondary">
                              {role}
                            </Badge>
                          ))}
                        </div>
                      </div>
                      
                      <div className="pt-4 space-y-2">
                        <Button
                          variant="outline"
                          size="sm"
                          className="w-full justify-start"
                          onClick={() => {
                            setResetError('');
                            setResetOpen(true);
                          }}
                        >
                          <Key className="h-4 w-4 mr-2" />
                          Reset Password
                        </Button>
                        
                        <Button
                          variant="outline"
                          size="sm"
                          className="w-full justify-start"
                          onClick={() => onSendWelcomeEmail?.(user.id)}
                        >
                          <Mail className="h-4 w-4 mr-2" />
                          Send Welcome Email
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>

              <TabsContent value="activity" className="space-y-4">
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg flex items-center">
                      <Activity className="h-5 w-5 mr-2" />
                      Recent Activity
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-4">
                      {activityLogs.map((log) => (
                        <div key={log.id} className="flex items-start space-x-3 pb-4 border-b last:border-0">
                          <div className={cn(
                            "p-1 rounded-full",
                            log.success ? "bg-green-100 text-green-600" : "bg-red-100 text-red-600"
                          )}>
                            <Activity className="h-3 w-3" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between">
                              <p className="text-sm font-medium">{log.action}</p>
                              <span className="text-xs text-muted-foreground">
                                {getTimeAgo(log.timestamp)}
                              </span>
                            </div>
                            <div className="text-xs text-muted-foreground space-y-1">
                              <div>IP: {log.ipAddress}</div>
                              {log.location && <div>Location: {log.location}</div>}
                              {log.device && <div>Device: {log.device}</div>}
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="sessions" className="space-y-4">
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg flex items-center">
                      <Eye className="h-5 w-5 mr-2" />
                      Active Sessions
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-4">
                      {sessions.map((session) => (
                        <div key={session.id} className="flex items-start space-x-3 p-4 border rounded-lg">
                          <div className={cn(
                            "p-1 rounded-full",
                            session.isActive ? "bg-green-100 text-green-600" : "bg-gray-100 text-gray-600"
                          )}>
                            <Clock className="h-3 w-3" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between">
                              <div className="flex items-center space-x-2">
                                <span className="text-sm font-medium">
                                  {session.isActive ? 'Active Session' : 'Inactive Session'}
                                </span>
                                <Badge variant={session.isActive ? 'default' : 'secondary'} className="text-xs">
                                  {session.isActive ? 'Active' : 'Ended'}
                                </Badge>
                              </div>
                              <Button variant="outline" size="sm" disabled={!session.isActive}>
                                End Session
                              </Button>
                            </div>
                            <div className="text-xs text-muted-foreground space-y-1 mt-2">
                              <div>Started: {formatDate(session.startTime)}</div>
                              <div>Last Activity: {formatDate(session.lastActivity)}</div>
                              <div>IP: {session.ipAddress}</div>
                              <div className="truncate">User Agent: {session.userAgent}</div>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="preferences" className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Display Preferences */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Settings className="h-5 w-5 mr-2" />
                        Display Preferences
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-sm font-medium">Theme</div>
                          <div className="text-sm text-muted-foreground">
                            {preferences.theme}
                          </div>
                        </div>
                        <Badge variant="outline">
                          {preferences.theme}
                        </Badge>
                      </div>
                      
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-sm font-medium">Language</div>
                          <div className="text-sm text-muted-foreground">English</div>
                        </div>
                        <Badge variant="outline">EN</Badge>
                      </div>
                      
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-sm font-medium">Timezone</div>
                          <div className="text-sm text-muted-foreground">{preferences.timezone}</div>
                        </div>
                        <Badge variant="outline">UTC</Badge>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Security & Notifications */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center">
                        <Bell className="h-5 w-5 mr-2" />
                        Security & Notifications
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-sm font-medium">Email Notifications</div>
                          <div className="text-sm text-muted-foreground">
                            Receive email alerts
                          </div>
                        </div>
                        <Badge variant={preferences.emailNotifications ? 'default' : 'secondary'}>
                          {preferences.emailNotifications ? 'Enabled' : 'Disabled'}
                        </Badge>
                      </div>
                      
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-sm font-medium">Push Notifications</div>
                          <div className="text-sm text-muted-foreground">
                            Browser notifications
                          </div>
                        </div>
                        <Badge variant={preferences.pushNotifications ? 'default' : 'secondary'}>
                          {preferences.pushNotifications ? 'Enabled' : 'Disabled'}
                        </Badge>
                      </div>
                      
                      <div className="flex items-center justify-between">
                        <div>
                          <div className="text-sm font-medium">Two-Factor Authentication</div>
                          <div className="text-sm text-muted-foreground">
                            Additional security layer
                          </div>
                        </div>
                        <Badge variant={preferences.twoFactorEnabled ? 'default' : 'secondary'}>
                          {preferences.twoFactorEnabled ? 'Enabled' : 'Disabled'}
                        </Badge>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>
            </div>
          </Tabs>
        </div>
    </div>
    <ConfirmationDialog
      open={resetOpen}
      onOpenChange={changeResetOpen}
      title={`Set temporary password for ${user.username}`}
      description="The user must replace this temporary password at their next sign-in. It expires according to the tenant security policy. Share it only through an approved secure channel."
      confirmText="Set temporary password"
      isLoading={resettingPassword}
      onConfirm={resetPassword}
      maxWidth="520px"
    >
      <div className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="admin-new-temporary-password">
            New Temporary Password
          </Label>
          <Input
            id="admin-new-temporary-password"
            type="password"
            autoComplete="new-password"
            value={newTemporaryPassword}
            onChange={(event) => setNewTemporaryPassword(event.target.value)}
          />
          <p className="text-xs text-muted-foreground">
            Use at least 12 characters with uppercase, lowercase, number, and
            special-character content. No password is generated or saved in
            the browser.
          </p>
        </div>
        <div className="space-y-2">
          <Label htmlFor="admin-confirm-temporary-password">
            Confirm Temporary Password
          </Label>
          <Input
            id="admin-confirm-temporary-password"
            type="password"
            autoComplete="new-password"
            value={confirmTemporaryPassword}
            onChange={(event) =>
              setConfirmTemporaryPassword(event.target.value)
            }
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="admin-password-reset-reason">
            Administrative reason
          </Label>
          <Textarea
            id="admin-password-reset-reason"
            value={resetReason}
            maxLength={500}
            onChange={(event) => setResetReason(event.target.value)}
            placeholder="Record why this credential reset is required."
          />
        </div>
        {resetError && (
          <div
            role="alert"
            className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive"
          >
            {resetError}
          </div>
        )}
      </div>
    </ConfirmationDialog>
    </>
  );
}

export default UserProfile;
