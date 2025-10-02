"use client"

import React, { useState, useEffect, ReactNode } from 'react'
import { cn } from '../../lib/utils'
import { Button } from '../ui/button'
import { Sheet, SheetContent, SheetTrigger } from '../ui/sheet'
import { 
  Menu, 
  X, 
  ChevronLeft, 
  Home,
  Bell,
  User,
  Settings,
  Search
} from 'lucide-react'

interface MobileLayoutProps {
  children: ReactNode
  title?: string
  showBackButton?: boolean
  onBack?: () => void
  showSearch?: boolean
  onSearchToggle?: () => void
  rightAction?: ReactNode
  bottomNavigation?: boolean
  className?: string
}

interface MobileBottomNavItem {
  icon: React.ElementType
  label: string
  href: string
  isActive?: boolean
  badge?: number
}

export const MobileLayout: React.FC<MobileLayoutProps> = ({
  children,
  title,
  showBackButton = false,
  onBack,
  showSearch = false,
  onSearchToggle,
  rightAction,
  bottomNavigation = true,
  className
}) => {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [isMobile, setIsMobile] = useState(false)

  useEffect(() => {
    const checkIsMobile = () => {
      setIsMobile(window.innerWidth < 768)
    }
    
    checkIsMobile()
    window.addEventListener('resize', checkIsMobile)
    return () => window.removeEventListener('resize', checkIsMobile)
  }, [])

  const bottomNavItems: MobileBottomNavItem[] = [
    { icon: Home, label: 'Dashboard', href: '/dashboard', isActive: true },
    { icon: Bell, label: 'Notifications', href: '/notifications', badge: 3 },
    { icon: Search, label: 'Search', href: '/search' },
    { icon: User, label: 'Profile', href: '/profile' },
    { icon: Settings, label: 'Settings', href: '/settings' }
  ]

  if (!isMobile) {
    return (
      <div className={cn("min-h-screen", className)}>
        {children}
      </div>
    )
  }

  return (
    <div className={cn("min-h-screen bg-background flex flex-col", className)}>
      {/* Mobile Header */}
      <header className="sticky top-0 z-50 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div className="flex items-center justify-between h-14 px-4">
          {/* Left side */}
          <div className="flex items-center gap-2">
            {showBackButton ? (
              <Button
                variant="ghost"
                size="sm"
                onClick={onBack}
                className="p-2 h-auto"
              >
                <ChevronLeft className="h-5 w-5" />
              </Button>
            ) : (
              <Sheet open={sidebarOpen} onOpenChange={setSidebarOpen}>
                <SheetTrigger asChild>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="p-2 h-auto"
                  >
                    <Menu className="h-5 w-5" />
                  </Button>
                </SheetTrigger>
                <SheetContent side="left" className="w-80 p-0">
                  <MobileSidebar onClose={() => setSidebarOpen(false)} />
                </SheetContent>
              </Sheet>
            )}
            
            {title && (
              <h1 className="text-lg font-semibold truncate">{title}</h1>
            )}
          </div>

          {/* Right side */}
          <div className="flex items-center gap-2">
            {showSearch && (
              <Button
                variant="ghost"
                size="sm"
                onClick={onSearchToggle}
                className="p-2 h-auto"
              >
                <Search className="h-5 w-5" />
              </Button>
            )}
            {rightAction}
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className={cn(
        "flex-1 overflow-auto",
        bottomNavigation && "pb-16"
      )}>
        {children}
      </main>

      {/* Bottom Navigation */}
      {bottomNavigation && (
        <nav className="fixed bottom-0 left-0 right-0 z-50 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-t">
          <div className="flex items-center justify-around h-16 px-2">
            {bottomNavItems.map((item) => {
              const Icon = item.icon
              return (
                <Button
                  key={item.href}
                  variant="ghost"
                  size="sm"
                  className={cn(
                    "flex flex-col items-center justify-center h-12 px-2 relative",
                    item.isActive && "text-primary"
                  )}
                >
                  <div className="relative">
                    <Icon className="h-5 w-5" />
                    {item.badge && item.badge > 0 && (
                      <span className="absolute -top-2 -right-2 h-4 w-4 bg-red-500 text-white text-xs rounded-full flex items-center justify-center">
                        {item.badge > 99 ? '99+' : item.badge}
                      </span>
                    )}
                  </div>
                  <span className="text-xs mt-1 truncate">{item.label}</span>
                </Button>
              )
            })}
          </div>
        </nav>
      )}
    </div>
  )
}

interface MobileSidebarProps {
  onClose: () => void
}

const MobileSidebar: React.FC<MobileSidebarProps> = ({ onClose }) => {
  const menuItems = [
    { icon: Home, label: 'Dashboard', href: '/dashboard' },
    { icon: Bell, label: 'Notifications', href: '/notifications' },
    { icon: Settings, label: 'Administration', href: '/administration' },
  ]

  return (
    <div className="flex flex-col h-full">
      {/* Sidebar Header */}
      <div className="flex items-center justify-between p-4 border-b">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center">
            <span className="text-primary-foreground font-bold text-sm">E</span>
          </div>
          <span className="font-semibold">ERP System</span>
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={onClose}
          className="p-2 h-auto"
        >
          <X className="h-5 w-5" />
        </Button>
      </div>

      {/* Navigation Menu */}
      <div className="flex-1 overflow-auto p-4">
        <nav className="space-y-2">
          {menuItems.map((item) => {
            const Icon = item.icon
            return (
              <Button
                key={item.href}
                variant="ghost"
                size="lg"
                className="w-full justify-start h-12"
                onClick={onClose}
              >
                <Icon className="h-5 w-5 mr-3" />
                {item.label}
              </Button>
            )
          })}
        </nav>
      </div>

      {/* Sidebar Footer */}
      <div className="p-4 border-t">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-muted rounded-full flex items-center justify-center">
            <User className="h-4 w-4" />
          </div>
          <div className="flex-1 truncate">
            <p className="text-sm font-medium">John Doe</p>
            <p className="text-xs text-muted-foreground">Admin User</p>
          </div>
        </div>
      </div>
    </div>
  )
}

export default MobileLayout