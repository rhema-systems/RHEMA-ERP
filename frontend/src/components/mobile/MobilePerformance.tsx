"use client"

import React, { useState, useEffect, useRef, useCallback, lazy, Suspense } from 'react'
import { cn } from '../../lib/utils'

// Intersection Observer hook for lazy loading
export const useIntersectionObserver = (
  options: IntersectionObserverInit = {}
) => {
  const [isIntersecting, setIsIntersecting] = useState(false)
  const targetRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const observer = new IntersectionObserver(([entry]) => {
      setIsIntersecting(entry.isIntersecting)
    }, {
      threshold: 0.1,
      rootMargin: '50px',
      ...options
    })

    const currentTarget = targetRef.current
    if (currentTarget) {
      observer.observe(currentTarget)
    }

    return () => {
      if (currentTarget) {
        observer.unobserve(currentTarget)
      }
    }
  }, [options])

  return { isIntersecting, targetRef }
}

// Lazy loading wrapper component
interface LazyLoadProps {
  children: React.ReactNode
  fallback?: React.ReactNode
  offset?: string
  once?: boolean
  className?: string
}

export const LazyLoad: React.FC<LazyLoadProps> = ({
  children,
  fallback = <div className="animate-pulse bg-muted rounded h-32 w-full" />,
  offset = '50px',
  once = true,
  className
}) => {
  const { isIntersecting, targetRef } = useIntersectionObserver({
    rootMargin: offset
  })
  
  const [hasLoaded, setHasLoaded] = useState(false)

  useEffect(() => {
    if (isIntersecting && !hasLoaded) {
      setHasLoaded(true)
    }
  }, [isIntersecting, hasLoaded])

  const shouldRender = once ? hasLoaded : isIntersecting

  return (
    <div ref={targetRef} className={className}>
      {shouldRender ? children : fallback}
    </div>
  )
}

// Image lazy loading component with optimization
interface LazyImageProps extends React.ImgHTMLAttributes<HTMLImageElement> {
  src: string
  alt: string
  placeholder?: string
  blurDataURL?: string
  priority?: boolean
}

export const LazyImage: React.FC<LazyImageProps> = ({
  src,
  alt,
  placeholder = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZGVmcz48bGluZWFyR3JhZGllbnQgaWQ9ImciPjxzdG9wIHN0b3AtY29sb3I9IiNmNmY3ZjgiLz48c3RvcCBvZmZzZXQ9IjEiIHN0b3AtY29sb3I9IiNlMGUxZTIiLz48L2xpbmVhckdyYWRpZW50PjwvZGVmcz48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSJ1cmwoI2cpIi8+PC9zdmc+',
  blurDataURL,
  priority = false,
  className,
  ...props
}) => {
  const [isLoaded, setIsLoaded] = useState(false)
  const [error, setError] = useState(false)
  const { isIntersecting, targetRef } = useIntersectionObserver()

  const shouldLoad = priority || isIntersecting

  return (
    <div ref={targetRef} className={cn("relative overflow-hidden", className)}>
      {shouldLoad && (
        <>
          <img
            src={error ? placeholder : src}
            alt={alt}
            onLoad={() => setIsLoaded(true)}
            onError={() => setError(true)}
            className={cn(
              "transition-opacity duration-300",
              isLoaded ? "opacity-100" : "opacity-0"
            )}
            {...props}
          />
          {!isLoaded && (
            <div className="absolute inset-0">
              <img
                src={blurDataURL || placeholder}
                alt=""
                className="w-full h-full object-cover filter blur-sm"
              />
            </div>
          )}
        </>
      )}
      {!shouldLoad && (
        <div className="w-full h-full bg-muted animate-pulse" />
      )}
    </div>
  )
}

// Virtual scrolling for large lists
interface VirtualScrollProps<T> {
  items: T[]
  itemHeight: number
  containerHeight: number
  renderItem: (item: T, index: number) => React.ReactNode
  className?: string
  overscan?: number
}

export function VirtualScroll<T>({
  items,
  itemHeight,
  containerHeight,
  renderItem,
  className,
  overscan = 5
}: VirtualScrollProps<T>) {
  const [scrollTop, setScrollTop] = useState(0)
  const scrollElementRef = useRef<HTMLDivElement>(null)

  const handleScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
    setScrollTop(e.currentTarget.scrollTop)
  }, [])

  const visibleStart = Math.floor(scrollTop / itemHeight)
  const visibleEnd = Math.min(
    visibleStart + Math.ceil(containerHeight / itemHeight),
    items.length - 1
  )

  const paddingTop = Math.max(0, (visibleStart - overscan) * itemHeight)
  const paddingBottom = Math.max(
    0,
    (items.length - visibleEnd - 1 - overscan) * itemHeight
  )

  const visibleItems = items.slice(
    Math.max(0, visibleStart - overscan),
    Math.min(items.length, visibleEnd + 1 + overscan)
  )

  return (
    <div
      ref={scrollElementRef}
      className={cn("overflow-auto", className)}
      style={{ height: containerHeight }}
      onScroll={handleScroll}
    >
      <div style={{ paddingTop, paddingBottom }}>
        {visibleItems.map((item, index) => (
          <div
            key={visibleStart - overscan + index}
            style={{ height: itemHeight }}
          >
            {renderItem(item, visibleStart - overscan + index)}
          </div>
        ))}
      </div>
    </div>
  )
}

// Performance monitoring hook
export const usePerformanceMonitor = () => {
  const [metrics, setMetrics] = useState<{
    FCP?: number
    LCP?: number
    FID?: number
    CLS?: number
    TTFB?: number
  }>({})

  useEffect(() => {
    // Only run in browser
    if (typeof window === 'undefined') return

    // Observe performance metrics
    const observer = new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        switch (entry.entryType) {
          case 'paint':
            if (entry.name === 'first-contentful-paint') {
              setMetrics(prev => ({ ...prev, FCP: entry.startTime }))
            }
            break
          case 'largest-contentful-paint':
            setMetrics(prev => ({ ...prev, LCP: entry.startTime }))
            break
          case 'first-input':
            setMetrics(prev => ({ ...prev, FID: (entry as any).processingStart - entry.startTime }))
            break
          case 'layout-shift':
            if (!(entry as any).hadRecentInput) {
              setMetrics(prev => ({ 
                ...prev, 
                CLS: (prev.CLS || 0) + (entry as any).value 
              }))
            }
            break
          case 'navigation':
            const navEntry = entry as PerformanceNavigationTiming
            setMetrics(prev => ({ 
              ...prev, 
              TTFB: navEntry.responseStart - navEntry.requestStart 
            }))
            break
        }
      }
    })

    // Observe different entry types
    try {
      observer.observe({ entryTypes: ['paint', 'largest-contentful-paint', 'first-input', 'layout-shift', 'navigation'] })
    } catch (e) {
      console.warn('Performance observer not fully supported')
    }

    return () => observer.disconnect()
  }, [])

  return metrics
}

// Bundle size analyzer component
export const BundleSizeInfo: React.FC = () => {
  const [bundleInfo, setBundleInfo] = useState<{
    jsSize: number
    cssSize: number
    totalSize: number
  } | null>(null)

  useEffect(() => {
    // Analyze loaded resources
    const resources = performance.getEntriesByType('resource') as PerformanceResourceTiming[]
    
    let jsSize = 0
    let cssSize = 0
    
    resources.forEach(resource => {
      if (resource.name.includes('.js')) {
        jsSize += resource.transferSize || 0
      } else if (resource.name.includes('.css')) {
        cssSize += resource.transferSize || 0
      }
    })
    
    setBundleInfo({
      jsSize,
      cssSize,
      totalSize: jsSize + cssSize
    })
  }, [])

  if (!bundleInfo) return null

  const formatSize = (bytes: number) => {
    const kb = bytes / 1024
    return kb > 1024 ? `${(kb / 1024).toFixed(1)}MB` : `${kb.toFixed(1)}KB`
  }

  return (
    <div className="text-xs text-muted-foreground p-2 border rounded">
      <div>JS: {formatSize(bundleInfo.jsSize)}</div>
      <div>CSS: {formatSize(bundleInfo.cssSize)}</div>
      <div>Total: {formatSize(bundleInfo.totalSize)}</div>
    </div>
  )
}

// Preload component for critical resources
interface PreloadProps {
  href: string
  as: 'script' | 'style' | 'image' | 'font'
  type?: string
  crossOrigin?: string
}

export const Preload: React.FC<PreloadProps> = ({ href, as, type, crossOrigin }) => {
  useEffect(() => {
    const link = document.createElement('link')
    link.rel = 'preload'
    link.href = href
    link.as = as
    if (type) link.type = type
    if (crossOrigin) link.crossOrigin = crossOrigin
    
    document.head.appendChild(link)
    
    return () => {
      document.head.removeChild(link)
    }
  }, [href, as, type, crossOrigin])

  return null
}

// Memory usage monitor
export const useMemoryMonitor = () => {
  const [memoryInfo, setMemoryInfo] = useState<{
    used?: number
    total?: number
    jsHeapSize?: number
  }>({})

  useEffect(() => {
    if ('memory' in performance) {
      const updateMemory = () => {
        const memory = (performance as any).memory
        setMemoryInfo({
          used: memory.usedJSHeapSize,
          total: memory.totalJSHeapSize,
          jsHeapSize: memory.jsHeapSizeLimit
        })
      }

      updateMemory()
      const interval = setInterval(updateMemory, 5000)
      return () => clearInterval(interval)
    }
  }, [])

  return memoryInfo
}

// Touch performance optimization
export const useTouchOptimization = () => {
  useEffect(() => {
    // Add CSS for better touch performance
    const style = document.createElement('style')
    style.textContent = `
      .touch-optimized {
        -webkit-touch-callout: none;
        -webkit-user-select: none;
        -khtml-user-select: none;
        -moz-user-select: none;
        -ms-user-select: none;
        user-select: none;
        -webkit-tap-highlight-color: transparent;
        touch-action: manipulation;
      }
      
      .smooth-scroll {
        -webkit-overflow-scrolling: touch;
        scroll-behavior: smooth;
      }
      
      .hardware-acceleration {
        transform: translateZ(0);
        will-change: transform;
      }
    `
    
    document.head.appendChild(style)
    
    return () => {
      document.head.removeChild(style)
    }
  }, [])
}