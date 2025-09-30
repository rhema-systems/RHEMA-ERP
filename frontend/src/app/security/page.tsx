'use client'

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { Loader2 } from 'lucide-react'

export default function SecurityPage() {
  const router = useRouter()

  useEffect(() => {
    // Redirect to the new unified security management dashboard
    router.replace('/administration/security/dashboard')
  }, [router])

  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="text-center space-y-4">
        <Loader2 className="h-8 w-8 animate-spin mx-auto" />
        <p className="text-muted-foreground">Redirecting to Security Management...</p>
      </div>
    </div>
  )
}