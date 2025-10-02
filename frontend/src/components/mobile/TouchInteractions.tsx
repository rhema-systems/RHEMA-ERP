"use client"

import React, { useState, useEffect, useRef, ReactNode, TouchEvent } from 'react'
import { cn } from '../../lib/utils'
import { RefreshCw } from 'lucide-react'

interface SwipeableCardProps {
  children: ReactNode
  onSwipeLeft?: () => void
  onSwipeRight?: () => void
  swipeThreshold?: number
  className?: string
  leftAction?: {
    icon: React.ElementType
    label: string
    color: string
  }
  rightAction?: {
    icon: React.ElementType
    label: string
    color: string
  }
}

export const SwipeableCard: React.FC<SwipeableCardProps> = ({
  children,
  onSwipeLeft,
  onSwipeRight,
  swipeThreshold = 100,
  className,
  leftAction,
  rightAction
}) => {
  const [startX, setStartX] = useState(0)
  const [currentX, setCurrentX] = useState(0)
  const [isDragging, setIsDragging] = useState(false)
  const [transform, setTransform] = useState(0)
  const cardRef = useRef<HTMLDivElement>(null)

  const handleTouchStart = (e: TouchEvent) => {
    setStartX(e.touches[0].clientX)
    setIsDragging(true)
  }

  const handleTouchMove = (e: TouchEvent) => {
    if (!isDragging) return
    
    const currentX = e.touches[0].clientX
    const deltaX = currentX - startX
    setCurrentX(currentX)
    setTransform(deltaX)
  }

  const handleTouchEnd = () => {
    if (!isDragging) return
    
    const deltaX = currentX - startX
    
    if (Math.abs(deltaX) > swipeThreshold) {
      if (deltaX > 0 && onSwipeRight) {
        onSwipeRight()
      } else if (deltaX < 0 && onSwipeLeft) {
        onSwipeLeft()
      }
    }
    
    setIsDragging(false)
    setTransform(0)
    setStartX(0)
    setCurrentX(0)
  }

  return (
    <div className={cn("relative overflow-hidden", className)}>
      {/* Left Action */}
      {leftAction && (
        <div 
          className={cn(
            "absolute inset-y-0 left-0 flex items-center justify-center w-20 transition-opacity",
            leftAction.color,
            transform > 0 && transform > swipeThreshold / 2 ? "opacity-100" : "opacity-0"
          )}
        >
          <div className="flex flex-col items-center gap-1">
            <leftAction.icon className="h-5 w-5 text-white" />
            <span className="text-xs text-white">{leftAction.label}</span>
          </div>
        </div>
      )}
      
      {/* Right Action */}
      {rightAction && (
        <div 
          className={cn(
            "absolute inset-y-0 right-0 flex items-center justify-center w-20 transition-opacity",
            rightAction.color,
            transform < 0 && Math.abs(transform) > swipeThreshold / 2 ? "opacity-100" : "opacity-0"
          )}
        >
          <div className="flex flex-col items-center gap-1">
            <rightAction.icon className="h-5 w-5 text-white" />
            <span className="text-xs text-white">{rightAction.label}</span>
          </div>
        </div>
      )}
      
      {/* Card Content */}
      <div
        ref={cardRef}
        className="transition-transform duration-200 ease-out touch-pan-y"
        style={{
          transform: `translateX(${transform}px)`,
          transition: isDragging ? 'none' : 'transform 0.2s ease-out'
        }}
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
      >
        {children}
      </div>
    </div>
  )
}

interface PullToRefreshProps {
  children: ReactNode
  onRefresh: () => Promise<void>
  refreshThreshold?: number
  className?: string
}

export const PullToRefresh: React.FC<PullToRefreshProps> = ({
  children,
  onRefresh,
  refreshThreshold = 70,
  className
}) => {
  const [startY, setStartY] = useState(0)
  const [currentY, setCurrentY] = useState(0)
  const [isDragging, setIsDragging] = useState(false)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [pullDistance, setPullDistance] = useState(0)
  const containerRef = useRef<HTMLDivElement>(null)

  const handleTouchStart = (e: TouchEvent) => {
    if (containerRef.current?.scrollTop === 0) {
      setStartY(e.touches[0].clientY)
      setIsDragging(true)
    }
  }

  const handleTouchMove = (e: TouchEvent) => {
    if (!isDragging || isRefreshing) return
    
    const currentY = e.touches[0].clientY
    const deltaY = currentY - startY
    
    if (deltaY > 0) {
      e.preventDefault()
      setCurrentY(currentY)
      setPullDistance(Math.min(deltaY, refreshThreshold + 30))
    }
  }

  const handleTouchEnd = async () => {
    if (!isDragging || isRefreshing) return
    
    const deltaY = currentY - startY
    
    if (deltaY > refreshThreshold) {
      setIsRefreshing(true)
      try {
        await onRefresh()
      } finally {
        setIsRefreshing(false)
      }
    }
    
    setIsDragging(false)
    setPullDistance(0)
    setStartY(0)
    setCurrentY(0)
  }

  const refreshIndicatorOpacity = Math.min(pullDistance / refreshThreshold, 1)
  const shouldShowRefreshing = isRefreshing || pullDistance > refreshThreshold

  return (
    <div 
      ref={containerRef}
      className={cn("relative overflow-hidden", className)}
      onTouchStart={handleTouchStart}
      onTouchMove={handleTouchMove}
      onTouchEnd={handleTouchEnd}
    >
      {/* Pull to Refresh Indicator */}
      <div 
        className="absolute top-0 left-0 right-0 flex items-center justify-center bg-primary/10 transition-all duration-200"
        style={{ 
          height: `${pullDistance}px`,
          opacity: refreshIndicatorOpacity
        }}
      >
        <div className="flex items-center gap-2 text-primary">
          <RefreshCw 
            className={cn(
              "h-5 w-5 transition-transform",
              shouldShowRefreshing && "animate-spin"
            )} 
          />
          <span className="text-sm font-medium">
            {isRefreshing ? 'Refreshing...' : 
             pullDistance > refreshThreshold ? 'Release to refresh' : 
             'Pull to refresh'}
          </span>
        </div>
      </div>
      
      {/* Content */}
      <div 
        className="transition-transform duration-200"
        style={{
          transform: `translateY(${pullDistance}px)`,
          transition: isDragging ? 'none' : 'transform 0.2s ease-out'
        }}
      >
        {children}
      </div>
    </div>
  )
}

interface TouchableOpacityProps {
  children: ReactNode
  onPress?: () => void
  activeOpacity?: number
  disabled?: boolean
  className?: string
}

export const TouchableOpacity: React.FC<TouchableOpacityProps> = ({
  children,
  onPress,
  activeOpacity = 0.7,
  disabled = false,
  className
}) => {
  const [isPressed, setIsPressed] = useState(false)

  const handleTouchStart = () => {
    if (!disabled) {
      setIsPressed(true)
    }
  }

  const handleTouchEnd = () => {
    if (!disabled) {
      setIsPressed(false)
      if (onPress) {
        onPress()
      }
    }
  }

  const handleTouchCancel = () => {
    setIsPressed(false)
  }

  return (
    <div
      className={cn(
        "transition-opacity duration-150",
        disabled && "opacity-50 cursor-not-allowed",
        className
      )}
      style={{
        opacity: isPressed && !disabled ? activeOpacity : 1
      }}
      onTouchStart={handleTouchStart}
      onTouchEnd={handleTouchEnd}
      onTouchCancel={handleTouchCancel}
      onMouseDown={handleTouchStart}
      onMouseUp={handleTouchEnd}
      onMouseLeave={handleTouchCancel}
    >
      {children}
    </div>
  )
}

interface LongPressProps {
  children: ReactNode
  onLongPress: () => void
  onPress?: () => void
  delay?: number
  className?: string
}

export const LongPress: React.FC<LongPressProps> = ({
  children,
  onLongPress,
  onPress,
  delay = 500,
  className
}) => {
  const [isPressed, setIsPressed] = useState(false)
  const timerRef = useRef<NodeJS.Timeout>()

  const handleStart = () => {
    setIsPressed(true)
    timerRef.current = setTimeout(() => {
      onLongPress()
      setIsPressed(false)
    }, delay)
  }

  const handleEnd = () => {
    setIsPressed(false)
    if (timerRef.current) {
      clearTimeout(timerRef.current)
      if (onPress) {
        onPress()
      }
    }
  }

  const handleCancel = () => {
    setIsPressed(false)
    if (timerRef.current) {
      clearTimeout(timerRef.current)
    }
  }

  useEffect(() => {
    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current)
      }
    }
  }, [])

  return (
    <div
      className={cn(
        "transition-transform duration-150",
        isPressed && "scale-95",
        className
      )}
      onTouchStart={handleStart}
      onTouchEnd={handleEnd}
      onTouchCancel={handleCancel}
      onMouseDown={handleStart}
      onMouseUp={handleEnd}
      onMouseLeave={handleCancel}
    >
      {children}
    </div>
  )
}

// Hook for detecting touch device
export const useTouch = () => {
  const [isTouch, setIsTouch] = useState(false)

  useEffect(() => {
    setIsTouch('ontouchstart' in window || navigator.maxTouchPoints > 0)
  }, [])

  return isTouch
}

// Hook for enhanced mobile detection
export const useMobile = () => {
  const [isMobile, setIsMobile] = useState(false)
  const [screenSize, setScreenSize] = useState({ width: 0, height: 0 })

  useEffect(() => {
    const checkDevice = () => {
      const width = window.innerWidth
      const height = window.innerHeight
      setScreenSize({ width, height })
      setIsMobile(width < 768)
    }

    checkDevice()
    window.addEventListener('resize', checkDevice)
    return () => window.removeEventListener('resize', checkDevice)
  }, [])

  return { 
    isMobile, 
    screenSize,
    isTablet: screenSize.width >= 768 && screenSize.width < 1024,
    isDesktop: screenSize.width >= 1024
  }
}