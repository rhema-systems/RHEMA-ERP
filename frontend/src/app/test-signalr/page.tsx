'use client'

import { useEffect, useState } from 'react'
import { signalRService } from '@/services/signalr.service'
import { HubConnectionState } from '@microsoft/signalr'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'

export default function TestSignalRPage() {
  const [connectionState, setConnectionState] = useState<HubConnectionState>(HubConnectionState.Disconnected)
  const [logs, setLogs] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)

  const addLog = (message: string) => {
    setLogs(prev => [`${new Date().toLocaleTimeString()}: ${message}`, ...prev.slice(0, 19)])
  }

  useEffect(() => {
    const unsubscribe = signalRService.onConnectionStateChange((state) => {
      setConnectionState(state)
      addLog(`Connection state changed to: ${HubConnectionState[state]}`)
    })

    return unsubscribe
  }, [])

  const handleConnect = async () => {
    try {
      setError(null)
      addLog('Attempting to connect...')
      await signalRService.connect()
      addLog('Connection successful!')
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      setError(errorMessage)
      addLog(`Connection failed: ${errorMessage}`)
    }
  }

  const handleDisconnect = async () => {
    try {
      addLog('Disconnecting...')
      await signalRService.disconnect()
      addLog('Disconnected successfully')
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      addLog(`Disconnect error: ${errorMessage}`)
    }
  }

  const getConnectionBadge = () => {
    switch (connectionState) {
      case HubConnectionState.Connected:
        return <Badge variant="default" className="bg-green-500">Connected</Badge>
      case HubConnectionState.Connecting:
        return <Badge variant="secondary">Connecting</Badge>
      case HubConnectionState.Reconnecting:
        return <Badge variant="secondary" className="bg-yellow-500">Reconnecting</Badge>
      case HubConnectionState.Disconnected:
        return <Badge variant="destructive">Disconnected</Badge>
      case HubConnectionState.Disconnecting:
        return <Badge variant="secondary">Disconnecting</Badge>
      default:
        return <Badge variant="outline">Unknown</Badge>
    }
  }

  return (
    <div className="container mx-auto p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">SignalR Connection Test</h1>
        {getConnectionBadge()}
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Connection Controls</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex gap-2">
              <Button 
                onClick={handleConnect} 
                disabled={connectionState === HubConnectionState.Connected || connectionState === HubConnectionState.Connecting}
              >
                Connect
              </Button>
              <Button 
                variant="outline" 
                onClick={handleDisconnect}
                disabled={connectionState === HubConnectionState.Disconnected || connectionState === HubConnectionState.Disconnecting}
              >
                Disconnect
              </Button>
            </div>

            <div className="space-y-2">
              <p><strong>Connection State:</strong> {HubConnectionState[connectionState]}</p>
              <p><strong>Connection ID:</strong> {signalRService.connectionId || 'None'}</p>
              <p><strong>Is Connected:</strong> {signalRService.isConnected ? 'Yes' : 'No'}</p>
            </div>

            {error && (
              <div className="p-3 bg-red-50 border border-red-200 rounded-md">
                <p className="text-red-800 text-sm"><strong>Error:</strong> {error}</p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Connection Logs</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="h-64 overflow-y-auto bg-gray-50 p-3 rounded-md font-mono text-sm">
              {logs.length === 0 ? (
                <p className="text-gray-500">No logs yet...</p>
              ) : (
                logs.map((log, index) => (
                  <div key={index} className="mb-1">
                    {log}
                  </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}