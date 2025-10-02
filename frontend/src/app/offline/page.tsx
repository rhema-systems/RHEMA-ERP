"use client"

import React from 'react'
import { Card, CardContent } from '../../components/ui/card'
import { Button } from '../../components/ui/button'
import { WifiOff, RefreshCw } from 'lucide-react'

export default function OfflinePage() {
  const handleRefresh = () => {
    window.location.reload()
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-background p-4">
      <Card className="w-full max-w-md">
        <CardContent className="p-8 text-center">
          <div className="flex justify-center mb-6">
            <div className="w-16 h-16 bg-muted rounded-full flex items-center justify-center">
              <WifiOff className="w-8 h-8 text-muted-foreground" />
            </div>
          </div>
          
          <h1 className="text-2xl font-bold mb-4">You're Offline</h1>
          
          <p className="text-muted-foreground mb-6">
            It looks like you've lost your internet connection. 
            Some features may not be available until you reconnect.
          </p>
          
          <div className="space-y-4">
            <Button 
              onClick={handleRefresh}
              className="w-full"
            >
              <RefreshCw className="w-4 h-4 mr-2" />
              Try Again
            </Button>
            
            <div className="text-sm text-muted-foreground">
              <p>Don't worry - your data is safe!</p>
              <p className="mt-2">
                Any changes you make will be saved and synced when you reconnect.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}