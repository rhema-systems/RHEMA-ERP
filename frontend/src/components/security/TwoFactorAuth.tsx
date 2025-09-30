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
  const [is2FAEnabled, setIs2FAEnabled] = useState(isEnabled)
  const [setupStep, setSetupStep] = useState<'initial' | 'qr' | 'verify' | 'backup'>('initial')
  const [secretKey, setSecretKey] = useState('')
  const [qrCodeUrl, setQrCodeUrl] = useState('')
  const [verificationCode, setVerificationCode] = useState('')
  const [backupCodes, setBackupCodes] = useState<BackupCode[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [showBackupCodes, setShowBackupCodes] = useState(false)
  const [copiedText, setCopiedText] = useState<string | null>(null)

  // Mock data for demonstration
  useEffect(() => {
    if (is2FAEnabled) {
      setBackupCodes([
        { code: '12345-67890', used: false },
        { code: '98765-43210', used: false },
        { code: '11111-22222', used: true, usedAt: new Date('2024-01-15') },
        { code: '33333-44444', used: false },
        { code: '55555-66666', used: false },
        { code: '77777-88888', used: false },
        { code: '99999-00000', used: false },
        { code: '12121-34343', used: false }
      ])
    }
  }, [is2FAEnabled])

  const generateSecretKey = () => {
    // In real app, this would be generated server-side
    const secret = 'JBSWY3DPEHPK3PXP'
    setSecretKey(secret)
    
    // Generate QR code URL for authenticator apps
    const appName = encodeURIComponent('ERP System')
    const accountName = encodeURIComponent(userEmail)
    const qrUrl = `otpauth://totp/${appName}:${accountName}?secret=${secret}&issuer=${appName}&algorithm=SHA1&digits=6&period=30`
    setQrCodeUrl(qrUrl)
    
    setSetupStep('qr')
  }

  const handleSetup2FA = async () => {
    setIsLoading(true)
    setError(null)
    
    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1000))
      generateSecretKey()
    } catch (err) {
      setError('Failed to generate 2FA setup. Please try again.')
    } finally {
      setIsLoading(false)
    }
  }

  const handleVerifyCode = async () => {
    if (!verificationCode || verificationCode.length !== 6) {
      setError('Please enter a valid 6-digit code')
      return
    }
    
    setIsLoading(true)
    setError(null)
    
    try {
      // Simulate API verification
      await new Promise(resolve => setTimeout(resolve, 1500))
      
      // Mock verification - accept 123456 as valid code
      if (verificationCode === '123456') {
        setSetupStep('backup')
        generateBackupCodes()
        setSuccess('2FA has been successfully enabled!')
      } else {
        setError('Invalid verification code. Please try again.')
      }
    } catch (err) {
      setError('Verification failed. Please try again.')
    } finally {
      setIsLoading(false)
    }
  }

  const generateBackupCodes = () => {
    const codes: BackupCode[] = []
    for (let i = 0; i < 8; i++) {
      const code = `${Math.floor(Math.random() * 90000) + 10000}-${Math.floor(Math.random() * 90000) + 10000}`
      codes.push({ code, used: false })
    }
    setBackupCodes(codes)
  }

  const handleEnable2FA = async () => {
    setIsLoading(true)
    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1000))
      setIs2FAEnabled(true)
      setSetupStep('initial')
      onStatusChange?.(true)
      setSuccess('2FA has been enabled successfully!')
    } catch (err) {
      setError('Failed to enable 2FA')
    } finally {
      setIsLoading(false)
    }
  }

  const handleDisable2FA = async () => {
    setIsLoading(true)
    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1000))
      setIs2FAEnabled(false)
      setBackupCodes([])
      onStatusChange?.(false)
      setSuccess('2FA has been disabled')
    } catch (err) {
      setError('Failed to disable 2FA')
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
    const codesText = backupCodes
      .map(bc => `${bc.code} ${bc.used ? '(USED)' : ''}`)
      .join('\n')
    
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
    setIsLoading(true)
    try {
      await new Promise(resolve => setTimeout(resolve, 1000))
      generateBackupCodes()
      setSuccess('New backup codes generated successfully!')
    } catch (err) {
      setError('Failed to regenerate backup codes')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="space-y-6">
      
      {/* 2FA Status Card */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            {is2FAEnabled ? (
              <ShieldCheck className="h-5 w-5 text-green-600" />
            ) : (
              <ShieldX className="h-5 w-5 text-red-600" />
            )}
            Two-Factor Authentication
            <Badge variant={is2FAEnabled ? "default" : "destructive"}>
              {is2FAEnabled ? 'Enabled' : 'Disabled'}
            </Badge>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {/* Status Messages */}
            {error && (
              <Alert variant="destructive">
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            
            {success && (
              <Alert>
                <Check className="h-4 w-4" />
                <AlertDescription>{success}</AlertDescription>
              </Alert>
            )}

            <p className="text-sm text-muted-foreground">
              {is2FAEnabled 
                ? 'Your account is protected with two-factor authentication. You\'ll need your authenticator app to sign in.'
                : 'Add an extra layer of security to your account by enabling two-factor authentication.'
              }
            </p>

            {/* Action Buttons */}
            <div className="flex gap-2">
              {!is2FAEnabled ? (
                <Dialog>
                  <DialogTrigger asChild>
                    <Button onClick={() => setSetupStep('initial')}>
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
                        
                        <Button 
                          onClick={handleSetup2FA} 
                          disabled={isLoading}
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
                              <QRCodeSVG value={qrCodeUrl} size={200} />
                            </div>
                          </div>
                          
                          <div className="text-xs text-muted-foreground">
                            <p className="mb-2">Can't scan? Enter this key manually:</p>
                            <div className="flex items-center gap-2 p-2 bg-muted rounded font-mono text-xs">
                              <span className="flex-1">{secretKey}</span>
                              <Button 
                                size="sm" 
                                variant="ghost" 
                                onClick={() => copyToClipboard(secretKey, 'secret')}
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
                        
                        <div className="text-xs text-center text-muted-foreground">
                          <p>Demo: Use code <span className="font-mono bg-muted px-1 rounded">123456</span> to verify</p>
                        </div>
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
                              {backupCodes.slice(0, 8).map((backup, index) => (
                                <div key={index} className="p-1">
                                  {backup.code}
                                </div>
                              ))}
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
                  <Button
                    variant="destructive"
                    onClick={handleDisable2FA}
                    disabled={isLoading}
                  >
                    {isLoading && <RefreshCw className="h-4 w-4 mr-2 animate-spin" />}
                    Disable 2FA
                  </Button>
                </>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Backup Codes Management */}
      {is2FAEnabled && (
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
                <div className="grid grid-cols-1 gap-2">
                  {backupCodes.map((backup, index) => (
                    <div 
                      key={index} 
                      className={`flex items-center justify-between p-2 rounded font-mono text-sm ${
                        backup.used 
                          ? 'bg-red-50 text-red-600 line-through' 
                          : 'bg-white'
                      }`}
                    >
                      <span>{backup.code}</span>
                      {backup.used && (
                        <Badge variant="destructive" className="text-xs">
                          Used {backup.usedAt?.toLocaleDateString()}
                        </Badge>
                      )}
                    </div>
                  ))}
                </div>
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