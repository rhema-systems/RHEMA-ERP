'use client';

import { useEffect, useState } from 'react';
import { TwoFactorAuth } from '../../components/security/TwoFactorAuth';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card';
import { AccountSkeleton } from '../../components/ui/profile-skeleton';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Alert, AlertDescription } from '../../components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs';
import { Badge } from '../../components/ui/badge';
import { Separator } from '../../components/ui/separator';
import { ConfirmationDialog } from '../../components/ui/confirmation-dialog';
import { 
  Shield, 
  Key, 
  Lock,
  AlertTriangle,
  Info,
  Settings,
  Clock,
  Smartphone,
  History,
  Eye,
  EyeOff,
  Monitor,
  MapPin,
  LogOut,
  RefreshCw,
  Trash2,
  CheckCircle
} from 'lucide-react';

interface UserProfile {
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
}

import { useAuth } from '../../hooks/use-auth';
import { profileService, type ChangePasswordRequest, type PasswordPolicy } from '../../services/profile';
import { validatePassword, getPasswordRequirementsText } from '../../utils/passwordValidation';
import { sessionService, type UserSession } from '../../services/session';
import { PasswordStrengthMeter } from '../../components/forms/PasswordStrengthMeter';

export default function AccountPage() {
  const [userEmail, setUserEmail] = useState<string>('');
  const [userName, setUserName] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [twoFactorEnabled, setTwoFactorEnabled] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState(true);
  
  // Password change form state
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [isChangingPassword, setIsChangingPassword] = useState(false);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordPolicy, setPasswordPolicy] = useState<PasswordPolicy | null>(null);
  const [isLoadingPolicy, setIsLoadingPolicy] = useState(true);
  
  // Sessions state
  const [sessions, setSessions] = useState<UserSession[]>([]);
  const [isLoadingSessions, setIsLoadingSessions] = useState(false);
  const [sessionError, setSessionError] = useState<string | null>(null);
  const [sessionSuccess, setSessionSuccess] = useState<string | null>(null);
  const [terminatingSessionId, setTerminatingSessionId] = useState<string | null>(null);
  const [showTerminateDialog, setShowTerminateDialog] = useState(false);
  const [sessionToTerminate, setSessionToTerminate] = useState<{id: string, isCurrent: boolean} | null>(null);

  const { user, isLoading: authLoading } = useAuth();

  useEffect(() => {
    loadUserInfo();
    loadPasswordPolicy();
  }, [user]);

  useEffect(() => {
    // Load sessions when Sessions tab might be visible
    loadSessions();
  }, []);

  const loadPasswordPolicy = async () => {
    try {
      setIsLoadingPolicy(true);
      const policy = await profileService.getPasswordPolicy();
      setPasswordPolicy(policy);
    } catch (err) {
      console.error('Error loading password policy:', err);
      // Set default policy as fallback
      setPasswordPolicy({
        minLength: 8,
        requireUppercase: true,
        requireLowercase: true,
        requireDigits: true,
        requireSpecialChars: true,
        maxAge: 90,
        preventReuse: 5
      });
    } finally {
      setIsLoadingPolicy(false);
    }
  };

  const loadUserInfo = async () => {
    setIsLoading(true);
    setError(null);

    try {
      if (user) {
        setUserEmail(user.email || 'user@example.com');
        setUserName(user.username || user.firstName || 'User');
      } else {
        // Fallback to localStorage if hook hasn't loaded yet
        const userInfo = typeof window !== 'undefined' ? localStorage.getItem('userInfo') : null;
        if (userInfo) {
          const u = JSON.parse(userInfo);
          setUserEmail(u.email || 'user@example.com');
          setUserName(u.username || u.firstName || 'User');
        } else {
          setUserEmail('user@example.com');
          setUserName('User');
        }
      }
    } catch (err) {
      console.error('Error loading user info:', err);
      setError('Failed to load user information');
      setUserEmail('user@example.com');
      setUserName('User');
    } finally {
      setIsLoading(false);
    }
  };

  const handlePasswordChange = async () => {
    setPasswordError(null);
    setPasswordSuccess(null);

    // Basic validation
    if (!currentPassword || !newPassword) {
      setPasswordError('Both current and new password are required');
      return;
    }

    // Validate against password policy
    if (passwordPolicy) {
      const validationResult = validatePassword(newPassword, passwordPolicy);
      if (!validationResult.isValid) {
        setPasswordError(validationResult.errors.join('. '));
        return;
      }
    }

    setIsChangingPassword(true);

    try {
      const request: ChangePasswordRequest = {
        currentPassword,
        newPassword
      };
      
      const response = await profileService.changePassword(request);
      
      if (response.success) {
        setPasswordSuccess(response.message);
        setCurrentPassword('');
        setNewPassword('');
      } else {
        setPasswordError('Failed to change password. Please try again.');
      }
    } catch (err: any) {
      // Don't log errors to console - just show user-friendly message
      // The ProfileService already handles specific error cases
      setPasswordError(err.message || 'Failed to change password. Please try again.');
    } finally {
      setIsChangingPassword(false);
    }
  };

  const loadSessions = async () => {
    setIsLoadingSessions(true);
    setSessionError(null);
    
    try {
      const userSessions = await sessionService.getMyActiveSessions();
      setSessions(userSessions);
    } catch (err: any) {
      console.error('Error loading sessions:', err);
      setSessionError(err.message || 'Failed to load your active sessions');
    } finally {
      setIsLoadingSessions(false);
    }
  };

  const handleTerminateSession = (sessionId: string, isCurrentSession: boolean) => {
    setSessionToTerminate({ id: sessionId, isCurrent: isCurrentSession });
    setShowTerminateDialog(true);
  };

  const confirmTerminateSession = async () => {
    if (!sessionToTerminate) return;

    const { id: sessionId, isCurrent: isCurrentSession } = sessionToTerminate;
    
    setTerminatingSessionId(sessionId);
    setSessionError(null);
    setSessionSuccess(null);

    try {
      await sessionService.terminateMySession(sessionId);
      
      if (isCurrentSession) {
        // If terminating current session, user will be logged out
        setSessionSuccess('Current session terminated. You will be logged out shortly.');
        // Redirect to login after a short delay
        setTimeout(() => {
          window.location.href = '/login';
        }, 2000);
      } else {
        setSessionSuccess('Session terminated successfully');
        // Reload sessions list
        await loadSessions();
      }
    } catch (err: any) {
      console.error('Error terminating session:', err);
      setSessionError(err.message || 'Failed to terminate session');
    } finally {
      setTerminatingSessionId(null);
      setSessionToTerminate(null);
    }
  };

  if (isLoading || authLoading) {
    return (
      <DashboardLayout>
        <AccountSkeleton />
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout>
      <div className="space-y-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <Settings className="h-8 w-8" />
            Account Settings
          </h1>
          <p className="text-muted-foreground mt-2">
            Manage your account security and authentication preferences
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
      
      <Tabs defaultValue="security" className="w-full">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="security" className="flex items-center gap-2">
            <Shield className="h-4 w-4" />
            Security
          </TabsTrigger>
          <TabsTrigger value="2fa" className="flex items-center gap-2">
            <Key className="h-4 w-4" />
            Two-Factor Auth
          </TabsTrigger>
          <TabsTrigger value="password" className="flex items-center gap-2">
            <Lock className="h-4 w-4" />
            Password
          </TabsTrigger>
          <TabsTrigger value="sessions" className="flex items-center gap-2">
            <Clock className="h-4 w-4" />
            Sessions
          </TabsTrigger>
        </TabsList>
        
        <TabsContent value="security" className="space-y-6">
          {/* Profile Information Card */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Settings className="h-5 w-5" />
                Profile Information
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 border rounded-lg">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center">
                        <span className="text-sm font-medium text-blue-600">
                          {userName.slice(0, 2).toUpperCase()}
                        </span>
                      </div>
                      <div>
                        <p className="font-medium">{userName}</p>
                        <p className="text-sm text-muted-foreground">{userEmail}</p>
                      </div>
                    </div>
                  </div>
                  
                  {user && (
                    <>
                      <div className="flex items-center justify-between p-4 border rounded-lg">
                        <div className="flex items-center gap-3">
                          <Clock className="h-5 w-5 text-gray-600" />
                          <div>
                            <p className="font-medium">Last Login</p>
                            <p className="text-sm text-muted-foreground">
                              {user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleDateString() : 'Never'}
                            </p>
                          </div>
                        </div>
                      </div>
                      
                      <div className="flex items-center justify-between p-4 border rounded-lg">
                        <div className="flex items-center gap-3">
                          <Shield className="h-5 w-5 text-purple-600" />
                          <div>
                            <p className="font-medium">User Roles</p>
                            <div className="flex flex-wrap gap-1 mt-1">
                              {user.roles && user.roles.length > 0 ? (
                                user.roles.map((role: string) => (
                                  <Badge key={role} variant="secondary" className="text-xs">
                                    {role}
                                  </Badge>
                                ))
                              ) : (
                                <span className="text-sm text-muted-foreground">No roles assigned</span>
                              )}
                            </div>
                          </div>
                        </div>
                      </div>
                    </>
                  )}
                </div>
                
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 border rounded-lg">
                    <div className="flex items-center gap-3">
                      <Key className="h-5 w-5 text-blue-600" />
                      <div>
                        <p className="font-medium">Two-Factor Authentication</p>
                        <p className="text-sm text-muted-foreground">
                          {twoFactorEnabled ? 'Protected with 2FA' : 'Not configured'}
                        </p>
                      </div>
                    </div>
                    <Badge variant={twoFactorEnabled ? "default" : "secondary"}>
                      {twoFactorEnabled ? 'Enabled' : 'Disabled'}
                    </Badge>
                  </div>

                  <div className="flex items-center justify-between p-4 border rounded-lg">
                    <div className="flex items-center gap-3">
                      <Lock className="h-5 w-5 text-green-600" />
                      <div>
                        <p className="font-medium">Password Security</p>
                        <p className="text-sm text-muted-foreground">Strong password set</p>
                      </div>
                    </div>
                    <Badge variant="default">Secure</Badge>
                  </div>
                  
                  <div className="p-4 bg-blue-50 dark:bg-blue-950/50 rounded-lg border border-blue-200 dark:border-blue-800">
                    <div className="flex items-start gap-3">
                      <Info className="h-5 w-5 text-blue-600 mt-0.5" />
                      <div>
                        <p className="font-medium text-blue-900 dark:text-blue-100">Security Tips</p>
                        <ul className="text-sm text-blue-800 dark:text-blue-200 mt-2 space-y-1">
                          <li>• Enable two-factor authentication</li>
                          <li>• Use a strong, unique password</li>
                          <li>• Review your active sessions regularly</li>
                          <li>• Keep your contact information updated</li>
                        </ul>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
        
        <TabsContent value="2fa" className="space-y-6">
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Smartphone className="h-5 w-5" />
              <h2 className="text-xl font-semibold">Two-Factor Authentication</h2>
            </div>
            <p className="text-muted-foreground">
              Secure your account with an additional layer of authentication using your mobile device.
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
          <div className="flex justify-center">
            <Card className="w-full max-w-md">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Lock className="h-5 w-5" />
                  Change Password
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
              {passwordError && (
                <Alert variant="destructive">
                  <AlertTriangle className="h-4 w-4" />
                  <AlertDescription>{passwordError}</AlertDescription>
                </Alert>
              )}
              
              {passwordSuccess && (
                <Alert className="bg-green-50 dark:bg-green-950/50 border-green-200 dark:border-green-800">
                  <CheckCircle className="h-4 w-4 text-green-600 dark:text-green-400" />
                  <AlertDescription className="text-green-800 dark:text-green-200">
                    {passwordSuccess}
                  </AlertDescription>
                </Alert>
              )}

              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="currentPassword">Current Password</Label>
                  <div className="relative">
                    <Input
                      id="currentPassword"
                      type={showCurrentPassword ? "text" : "password"}
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      placeholder="Enter your current password"
                      className="pr-10"
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                      onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                    >
                      {showCurrentPassword ? (
                        <EyeOff className="h-4 w-4" />
                      ) : (
                        <Eye className="h-4 w-4" />
                      )}
                    </Button>
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="newPassword">New Password</Label>
                  <div className="relative">
                    <Input
                      id="newPassword"
                      type={showNewPassword ? "text" : "password"}
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder="Enter your new password"
                      className="pr-10"
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                      onClick={() => setShowNewPassword(!showNewPassword)}
                    >
                      {showNewPassword ? (
                        <EyeOff className="h-4 w-4" />
                      ) : (
                        <Eye className="h-4 w-4" />
                      )}
                    </Button>
                  </div>
                  
                  {/* Password Strength Meter */}
                  <PasswordStrengthMeter password={newPassword} className="mt-3" />
                  
                  {passwordPolicy && !isLoadingPolicy ? (
                    <div className="text-xs text-muted-foreground mt-2">
                      <p className="mb-1">Password must meet these requirements:</p>
                      <ul className="list-disc list-inside space-y-1">
                        {getPasswordRequirementsText(passwordPolicy).map((requirement, index) => (
                          <li key={index}>{requirement}</li>
                        ))}
                      </ul>
                    </div>
                  ) : (
                    <p className="text-xs text-muted-foreground mt-2">
                      {isLoadingPolicy ? 'Loading password requirements...' : 'Password must be at least 8 characters long'}
                    </p>
                  )}
                </div>

                <Button 
                  onClick={handlePasswordChange}
                  disabled={isChangingPassword || !currentPassword || !newPassword}
                  className="w-full"
                >
                  {isChangingPassword ? 'Changing Password...' : 'Change Password'}
                </Button>
              </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
        
        <TabsContent value="sessions" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <History className="h-5 w-5" />
                Active Sessions
                <Button
                  variant="outline"
                  size="sm"
                  onClick={loadSessions}
                  disabled={isLoadingSessions}
                  className="ml-auto"
                >
                  {isLoadingSessions ? (
                    <RefreshCw className="h-4 w-4 animate-spin" />
                  ) : (
                    <RefreshCw className="h-4 w-4" />
                  )}
                  Refresh
                </Button>
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="text-sm text-muted-foreground">
                  <p>These are all the devices where you're currently signed in. If you see a session you don't recognize, you should terminate it immediately.</p>
                </div>
                
                {/* Status Messages */}
                {sessionError && (
                  <Alert variant="destructive">
                    <AlertTriangle className="h-4 w-4" />
                    <AlertDescription>{sessionError}</AlertDescription>
                  </Alert>
                )}
                
                {sessionSuccess && (
                  <Alert>
                    <CheckCircle className="h-4 w-4" />
                    <AlertDescription>{sessionSuccess}</AlertDescription>
                  </Alert>
                )}
                
                {/* Sessions List */}
                {isLoadingSessions ? (
                  <div className="space-y-4">
                    {[1, 2, 3].map((i) => (
                      <div key={i} className="flex items-center justify-between p-4 border rounded-lg animate-pulse">
                        <div className="flex items-center gap-3">
                          <div className="w-3 h-3 bg-gray-300 rounded-full"></div>
                          <div className="space-y-2">
                            <div className="h-4 bg-gray-300 rounded w-48"></div>
                            <div className="h-3 bg-gray-200 rounded w-32"></div>
                          </div>
                        </div>
                        <div className="h-6 bg-gray-300 rounded w-16"></div>
                      </div>
                    ))}
                  </div>
                ) : sessions.length > 0 ? (
                  <div className="space-y-4">
                    {sessions.map((session) => (
                      <div key={session.sessionId} className="flex items-center justify-between p-4 border rounded-lg hover:bg-muted/50 transition-colors">
                        <div className="flex items-center gap-4">
                          <div className={`w-3 h-3 rounded-full ${
                            session.isCurrentSession ? 'bg-green-500' : 'bg-blue-500'
                          }`}></div>
                          
                          <div className="flex items-center gap-3">
                            <Monitor className="h-5 w-5 text-muted-foreground" />
                            <div>
                              <div className="flex items-center gap-2">
                                <p className="font-medium">
                                  {sessionService.getDeviceDescription(session)}
                                </p>
                                {session.isCurrentSession && (
                                  <Badge variant="outline" className="text-xs">
                                    Current
                                  </Badge>
                                )}
                              </div>
                              <div className="flex items-center gap-4 text-sm text-muted-foreground mt-1">
                                <div className="flex items-center gap-1">
                                  <MapPin className="h-3 w-3" />
                                  <span>{sessionService.getLocationDisplay(session.location)}</span>
                                </div>
                                <div className="flex items-center gap-1">
                                  <Clock className="h-3 w-3" />
                                  <span>Active {sessionService.formatDateTime(session.lastActivityTime)}</span>
                                </div>
                                <div className="flex items-center gap-1">
                                  <span>Duration: {sessionService.formatSessionDuration(session.sessionDuration)}</span>
                                </div>
                              </div>
                              <div className="text-xs text-muted-foreground mt-1">
                                IP: {session.ipAddress}
                              </div>
                            </div>
                          </div>
                        </div>
                        
                        <Button
                          variant={session.isCurrentSession ? "destructive" : "outline"}
                          size="sm"
                          onClick={() => handleTerminateSession(session.sessionId, session.isCurrentSession)}
                          disabled={terminatingSessionId === session.sessionId}
                          className="flex items-center gap-2"
                        >
                          {terminatingSessionId === session.sessionId ? (
                            <RefreshCw className="h-4 w-4 animate-spin" />
                          ) : (
                            <LogOut className="h-4 w-4" />
                          )}
                          {session.isCurrentSession ? 'Sign Out' : 'Terminate'}
                        </Button>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-center py-8">
                    <History className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                    <p className="text-muted-foreground">No active sessions found</p>
                    <Button variant="outline" onClick={loadSessions} className="mt-4">
                      <RefreshCw className="h-4 w-4 mr-2" />
                      Refresh Sessions
                    </Button>
                  </div>
                )}
                
                {/* Security Information */}
                <Separator />
                
                <div className="p-4 bg-blue-50 dark:bg-blue-950/50 rounded-lg border border-blue-200 dark:border-blue-800">
                  <div className="flex items-start gap-3">
                    <Info className="h-5 w-5 text-blue-600 mt-0.5" />
                    <div>
                      <p className="font-medium text-blue-900 dark:text-blue-100">Session Security</p>
                      <ul className="text-sm text-blue-800 dark:text-blue-200 mt-2 space-y-1">
                        <li>• Sessions are automatically terminated after extended inactivity</li>
                        <li>• If you see suspicious activity, terminate unknown sessions immediately</li>
                        <li>• Your current session is marked with a green indicator</li>
                        <li>• Contact support if you notice any unauthorized access</li>
                      </ul>
                    </div>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
      </div>

      {/* Session Termination Confirmation Dialog */}
      <ConfirmationDialog
        open={showTerminateDialog}
        onOpenChange={setShowTerminateDialog}
        title={sessionToTerminate?.isCurrent ? "Sign Out Current Session" : "Terminate Session"}
        description={
          sessionToTerminate?.isCurrent
            ? "Terminating this session will log you out and you'll need to sign in again. Are you sure you want to continue?"
            : "Are you sure you want to terminate this session? The user will be signed out of that device."
        }
        confirmText={sessionToTerminate?.isCurrent ? "Sign Out" : "Terminate"}
        cancelText="Cancel"
        variant="destructive"
        onConfirm={confirmTerminateSession}
        isLoading={terminatingSessionId === sessionToTerminate?.id}
      />
    </DashboardLayout>
  );
}
