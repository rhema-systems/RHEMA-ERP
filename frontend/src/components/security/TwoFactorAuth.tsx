"use client"

import React, { useState, useEffect } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Badge } from '../ui/badge'
import { Alert, AlertDescription } from '../ui/alert'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '../ui/dialog'
import { QRCodeSVG } from 'qrcode.react'
import { securityService, type TwoFactorSettings, type TwoFactorSetup } from '../../services/security'
import { 
  Shield, 
  ShieldCheck, 
  ShieldX, 
  Smartphone, 
  Key, 
  Download, 
  RefreshCw, 
  Copy, 
  Check,
  AlertTriangle,
  Info
} from 'lucide-react'

interface TwoFactorAuthProps {
  userEmail?: string
  isEnabled?: boolean
  onStatusChange?: (enabled: boolean) => void
}

interface BackupCode {
  code: string
  used: boolean
  usedAt?: Date
}

export const TwoFactorAuth: React.FC<TwoFactorAuthProps> = ({
  userEmail = "user@example.com",
  isEnabled = false,
  onStatusChange
}) => {
  const [twoFactorSettings, setTwoFactorSettings] = useState<TwoFactorSettings | null>(null)
  const [setupStep, setSetupStep] = useState<'initial' | 'qr' | 'verify' | 'backup'>('initial')
  const [setupData, setSetupData] = useState<TwoFactorSetup | null>(null)
  const [verificationCode, setVerificationCode] = useState('')
  const [password, setPassword] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [showBackupCodes, setShowBackupCodes] = useState(false)
  const [copiedText, setCopiedText] = useState<string | null>(null)

  // Load 2FA settings on component mount
  useEffect(() => {
    loadTwoFactorSettings()
  }, [])

  const loadTwoFactorSettings = async () => {
    setIsLoading(true)
    setError(null)
    try {
      const settings = await securityService.getTwoFactorSettings()
      setTwoFactorSettings(settings)
      onStatusChange?.(settings.isEnabled)
    } catch (err) {
      setError('Failed to load 2FA settings')
      console.error('Error loading 2FA settings:', err)
    } finally {
      setIsLoading(false)
    }
  }

  const handleSetup2FA = async () => {
    if (!password) {
      setError('Password is required to enable 2FA')
      return
    }

    setIsLoading(true)
    setError(null)
    
    try {
      // Call API to initiate 2FA setup (this generates the QR code)
      const setup = await securityService.enableTwoFactor({
        verificationCode: '', // Empty for initial setup
        password
      })
      
      setSetupData(setup)
      setSetupStep('qr')
    } catch (err: any) {
      // Make error messages more user-friendly
      const errorMessage = err?.message || 'Failed to generate 2FA setup. Please try again.'
      
      if (errorMessage.toLowerCase().includes('incorrect password')) {
        setError('The password you entered is incorrect. Please double-check and try again.')
      } else if (errorMessage.toLowerCase().includes('password')) {
        setError('Password verification failed. Please make sure you entered your current password correctly.')
      } else {
        setError('Unable to start 2FA setup. Please try again or contact support if the problem persists.')
      }
    } finally {
      setIsLoading(false)
    }
  }

  const handleVerifyCode = async () => {
    if (!verificationCode || verificationCode.length !== 6) {
      setError('Please enter a valid 6-digit code')
      return
    }
    
    if (!password) {
      setError('Password is required')
      return
    }
    
    setIsLoading(true)
    setError(null)
    
    try {
      // Call API to verify the code and complete 2FA setup
      const setup = await securityService.enableTwoFactor({
        verificationCode,
        password
      })
      
      setSetupData(setup)
      setSetupStep('backup')
      setSuccess('2FA has been successfully enabled!')
    } catch (err: any) {
      // Make error messages more user-friendly
      const errorMessage = err?.message || 'Invalid verification code. Please try again.'
      
      if (errorMessage.toLowerCase().includes('incorrect password')) {
        setError('The password you entered is incorrect. Please double-check and try again.')
      } else if (errorMessage.toLowerCase().includes('verification code')) {
        setError('The verification code is incorrect or has expired. Please enter the current 6-digit code from your authenticator app.')
      } else if (errorMessage.toLowerCase().includes('code')) {
        setError('The code you entered is not valid. Please make sure you\'re using the current 6-digit code from your authenticator app.')
      } else {
        setError('Unable to verify the code. Please try again with a fresh code from your authenticator app.')
      }
    } finally {
      setIsLoading(false)
    }
  }

  const handleEnable2FA = async () => {
    setIsLoading(true)
    try {
      // Refresh settings to get updated state
      await loadTwoFactorSettings()
      setSetupStep('initial')
      setSuccess('2FA has been enabled successfully!')
    } catch (err) {
      setError('Failed to enable 2FA')
    } finally {
      setIsLoading(false)
    }
  }

  const handleDisable2FA = async () => {
    if (!password) {
      setError('Password is required to disable 2FA')
      return
    }

    setIsLoading(true)
    try {
      await securityService.disableTwoFactor({
        password,
        reason: 'User requested'
      })
      
      await loadTwoFactorSettings()
      setSuccess('2FA has been disabled')
    } catch (err: any) {
      // Make error messages more user-friendly
      const errorMessage = err?.message || 'Failed to disable 2FA'
      
      if (errorMessage.toLowerCase().includes('incorrect password')) {
        setError('The password you entered is incorrect. Please double-check and try again.')
      } else if (errorMessage.toLowerCase().includes('password')) {
        setError('Password verification failed. Please make sure you entered your current password correctly.')
      } else {
        setError('Unable to disable 2FA. Please try again or contact support if the problem persists.')
      }
    } finally {
      setIsLoading(false)
    }
  }

  const copyToClipboard = async (text: string, label: string) => {
    try {
      await navigator.clipboard.writeText(text)
      setCopiedText(label)
      setTimeout(() => setCopiedText(null), 2000)
    } catch (err) {
      console.error('Failed to copy to clipboard:', err)
    }
  }

  const downloadBackupCodes = () => {
    if (!setupData?.recoveryCodes) return
    
    const codesText = setupData.recoveryCodes.join('\n')
    
    const blob = new Blob([`ERP System - 2FA Backup Codes\nGenerated: ${new Date().toISOString()}\n\n${codesText}`], 
      { type: 'text/plain' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `erp-2fa-backup-codes-${new Date().toISOString().split('T')[0]}.txt`
    a.click()
    URL.revokeObjectURL(url)
  }

  const regenerateBackupCodes = async () => {
    setError('Backup code regeneration not yet implemented')
  }

  return (
    <div className="space-y-6">
      
      {/* 2FA Status Card */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            {twoFactorSettings?.isEnabled ? (
              <ShieldCheck className="h-5 w-5 text-green-600" />
            ) : (
              <ShieldX className="h-5 w-5 text-red-600" />
            )}
            Two-Factor Authentication
            <Badge variant={twoFactorSettings?.isEnabled ? "default" : "destructive"}>
              {twoFactorSettings?.isEnabled ? 'Enabled' : 'Disabled'}
            </Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {/* Success Messages (only show success on main page) */}
            {success && (
              <Alert>
                <Check className="h-4 w-4" />
                <AlertDescription>{success}</AlertDescription>
              </Alert>
            )}

            <p className="text-sm text-muted-foreground">
              {twoFactorSettings?.isEnabled 
                ? 'Your account is protected with two-factor authentication. You\'ll need your authenticator app to sign in.'
                : 'Add an extra layer of security to your account by enabling two-factor authentication.'
              }
            </p>

            {/* Action Buttons */}
            <div className="flex gap-2">
              {!twoFactorSettings?.isEnabled ? (
                <Dialog>
                  <DialogTrigger asChild>
                    <Button onClick={() => {
                      setSetupStep('initial')
                      setError(null)
                      setPassword('')
                      setVerificationCode('')
                    }}>
                      <Shield className="h-4 w-4 mr-2" />
                      Enable 2FA
                    </Button>
                  </DialogTrigger>
                  <DialogContent className="max-w-md">
                    <DialogHeader>
                      <DialogTitle className="flex items-center gap-2">
                        <Shield className="h-5 w-5" />
                        Set up Two-Factor Authentication
                      </DialogTitle>
                    </DialogHeader>
                    
                    {setupStep === 'initial' && (
                      <div className="space-y-4">
                        <div className="text-sm text-muted-foreground">
                          <p className="mb-3">Two-factor authentication adds an extra layer of security to your account.</p>
                          
                          <div className="space-y-2">
                            <p className="font-medium">You'll need:</p>
                            <ul className="list-disc list-inside space-y-1 text-xs">
                              <li>A smartphone with an authenticator app</li>
                              <li>Google Authenticator, Authy, or similar app</li>
                              <li>Access to save backup codes securely</li>
                            </ul>
                          </div>
                        </div>
                        
                        {/* Error display within dialog */}
                        {error && (
                          <Alert variant="destructive">
                            <AlertTriangle className="h-4 w-4" />
                            <AlertDescription>{error}</AlertDescription>
                          </Alert>
                        )}
                        
                        <div className="space-y-2">
                          <Label htmlFor="setup-password">Current Password</Label>
                          <Input
                            id="setup-password"
                            type="password"
                            placeholder="Enter your password"
                            value={password}
                            onChange={(e) => {
                              setPassword(e.target.value)
                              setError(null)
                            }}
                          />
                        </div>
                        
                        <Button 
                          onClick={handleSetup2FA} 
                          disabled={isLoading || !password}
                          className="w-full"
                        >
                          {isLoading ? (
                            <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
                          ) : (
                            <Smartphone className="h-4 w-4 mr-2" />
                          )}
                          Start Setup
                        </Button>
                      </div>
                    )}

                    {setupStep === 'qr' && (
                      <div className="space-y-4">
                        <div className="text-center">
                          <h4 className="font-medium mb-2">Scan QR Code</h4>
                          <p className="text-sm text-muted-foreground mb-4">
                            Use your authenticator app to scan this QR code
                          </p>
                          
                          <div className="flex justify-center mb-4">
                            <div className="p-4 bg-white rounded-lg border">
                              {setupData?.qrCodeUrl ? (
                                <img src={setupData.qrCodeUrl} alt="2FA QR Code" width={200} height={200} />
                              ) : (
                                <div className="w-[200px] h-[200px] bg-muted rounded flex items-center justify-center">
                                  <p className="text-sm text-muted-foreground">Loading QR Code...</p>
                                </div>
                              )}
                            </div>
                          </div>
                          
                          <div className="text-xs text-muted-foreground">
                            <p className="mb-2">Can't scan? Enter this key manually:</p>
                            <div className="flex items-center gap-2 p-2 bg-muted rounded font-mono text-xs">
                              <span className="flex-1">{setupData?.authenticatorKey || 'Loading...'}</span>
                              <Button 
                                size="sm" 
                                variant="ghost" 
                                onClick={() => copyToClipboard(setupData?.authenticatorKey || '', 'secret')}
                                disabled={!setupData?.authenticatorKey}
                              >
                                {copiedText === 'secret' ? (
                                  <Check className="h-3 w-3" />
                                ) : (
                                  <Copy className="h-3 w-3" />
                                )}
                              </Button>
                            </div>
                          </div>
                        </div>
                        
                        <Button 
                          onClick={() => setSetupStep('verify')}
                          className="w-full"
                        >
                          I've Added the Account
                        </Button>
                      </div>
                    )}

                    {setupStep === 'verify' && (
                      <div className="space-y-4">
                        <div>
                          <h4 className="font-medium mb-2">Verify Setup</h4>
                          <p className="text-sm text-muted-foreground mb-4">
                            Enter the 6-digit code from your authenticator app
                          </p>
                          
                          {/* Error display within verify dialog */}
                          {error && (
                            <Alert variant="destructive">
                              <AlertTriangle className="h-4 w-4" />
                              <AlertDescription>{error}</AlertDescription>
                            </Alert>
                          )}
                          
                          <div className="space-y-2">
                            <Label htmlFor="verification-code">Verification Code</Label>
                            <Input
                              id="verification-code"
                              placeholder="000000"
                              value={verificationCode}
                              onChange={(e) => {
                                setError(null)
                                setVerificationCode(e.target.value.replace(/\D/g, '').slice(0, 6))
                              }}
                              className="text-center text-lg font-mono tracking-wider"
                              maxLength={6}
                            />
                          </div>
                        </div>
                        
                        <Button 
                          onClick={handleVerifyCode} 
                          disabled={isLoading || verificationCode.length !== 6}
                          className="w-full"
                        >
                          {isLoading ? (
                            <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
                          ) : (
                            <Check className="h-4 w-4 mr-2" />
                          )}
                          Verify & Enable
                        </Button>
                        
                      </div>
                    )}

                    {setupStep === 'backup' && (
                      <div className="space-y-4">
                        <div>
                          <h4 className="font-medium mb-2 text-green-600">Setup Complete!</h4>
                          <p className="text-sm text-muted-foreground mb-4">
                            Save these backup codes in a secure location. You can use them to access your account if you lose your phone.
                          </p>
                          
                          <div className="bg-muted p-3 rounded-lg">
                            <div className="grid grid-cols-2 gap-2 text-xs font-mono">
                              {setupData?.recoveryCodes?.map((code, index) => (
                                <div key={index} className="p-1">
                                  {code}
                                </div>
                              )) || (
                                <div className="col-span-2 text-center text-muted-foreground">
                                  Loading backup codes...
                                </div>
                              )}
                            </div>
                          </div>
                        </div>
                        
                        <div className="flex gap-2">
                          <Button 
                            onClick={downloadBackupCodes}
                            variant="outline"
                            className="flex-1"
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                          <Button 
                            onClick={handleEnable2FA}
                            className="flex-1"
                          >
                            Complete Setup
                          </Button>
                        </div>
                      </div>
                    )}
                  </DialogContent>
                </Dialog>
              ) : (
                <>
                  <Button
                    variant="outline"
                    onClick={() => setShowBackupCodes(true)}
                  >
                    <Key className="h-4 w-4 mr-2" />
                    View Backup Codes
                  </Button>
                  <Dialog>
                    <DialogTrigger asChild>
                      <Button 
                        variant="destructive" 
                        disabled={isLoading}
                        onClick={() => {
                          setError(null)
                          setPassword('')
                        }}
                      >
                        {isLoading && <RefreshCw className="h-4 w-4 mr-2 animate-spin" />}
                        Disable 2FA
                      </Button>
                    </DialogTrigger>
                    <DialogContent className="max-w-md">
                      <DialogHeader>
                        <DialogTitle className="flex items-center gap-2">
                          <ShieldX className="h-5 w-5" />
                          Disable Two-Factor Authentication
                        </DialogTitle>
                      </DialogHeader>
                      
                      <div className="space-y-4">
                        <Alert variant="destructive">
                          <AlertTriangle className="h-4 w-4" />
                          <AlertDescription>
                            Disabling 2FA will make your account less secure. Are you sure you want to continue?
                          </AlertDescription>
                        </Alert>
                        
                        {/* Error display within disable dialog */}
                        {error && (
                          <Alert variant="destructive">
                            <AlertTriangle className="h-4 w-4" />
                            <AlertDescription>{error}</AlertDescription>
                          </Alert>
                        )}
                        
                        <div className="space-y-2">
                          <Label htmlFor="disable-password">Current Password</Label>
                          <Input
                            id="disable-password"
                            type="password"
                            placeholder="Enter your password to confirm"
                            value={password}
                            onChange={(e) => {
                              setPassword(e.target.value)
                              setError(null)
                            }}
                          />
                        </div>
                        
                        <Button 
                          onClick={handleDisable2FA}
                          disabled={isLoading || !password}
                          variant="destructive"
                          className="w-full"
                        >
                          {isLoading ? (
                            <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
                          ) : (
                            <ShieldX className="h-4 w-4 mr-2" />
                          )}
                          Disable 2FA
                        </Button>
                      </div>
                    </DialogContent>
                  </Dialog>
                </>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Backup Codes Management */}
      {twoFactorSettings?.isEnabled && (
        <Dialog open={showBackupCodes} onOpenChange={setShowBackupCodes}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Key className="h-5 w-5" />
                Backup Codes
              </DialogTitle>
            </DialogHeader>
            
            <div className="space-y-4">
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  These codes can be used to access your account if you lose access to your authenticator app. 
                  Each code can only be used once.
                </AlertDescription>
              </Alert>
              
              <div className="bg-muted p-4 rounded-lg">
                {twoFactorSettings?.recoveryCodes && twoFactorSettings.recoveryCodes.length > 0 ? (
                  <div className="grid grid-cols-1 gap-2">
                    {twoFactorSettings.recoveryCodes.map((code, index) => (
                      <div 
                        key={index} 
                        className="flex items-center justify-between p-2 rounded font-mono text-sm bg-white"
                      >
                        <span>{code}</span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-center text-muted-foreground py-4">
                    <p>No backup codes available.</p>
                    <p className="text-xs mt-1">Backup codes are generated when you first enable 2FA.</p>
                  </div>
                )}
              </div>
              
              <div className="flex gap-2">
                <Button 
                  onClick={downloadBackupCodes}
                  variant="outline"
                  className="flex-1"
                >
                  <Download className="h-4 w-4 mr-2" />
                  Download Codes
                </Button>
                <Button 
                  onClick={regenerateBackupCodes}
                  variant="outline"
                  className="flex-1"
                  disabled={isLoading}
                >
                  {isLoading ? (
                    <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
                  ) : (
                    <RefreshCw className="h-4 w-4 mr-2" />
                  )}
                  Regenerate
                </Button>
              </div>
              
              <div className="text-xs text-muted-foreground">
                <p>• Keep these codes in a secure location</p>
                <p>• Don't share them with anyone</p>
                <p>• Generate new codes if you suspect they've been compromised</p>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      )}
    </div>
  )
}