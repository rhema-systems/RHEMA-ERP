"use client"

import { useEffect } from 'react'
import { PWAManager } from '../lib/pwa'

export const PWAInit = () => {
  useEffect(() => {
    // Initialize PWA manager
    PWAManager.getInstance().init()
  }, [])

  return null
}