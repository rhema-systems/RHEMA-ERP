# Frontend Notification System - Implementation Guide

## Overview

The frontend notification system provides a modern, real-time user interface for displaying and managing notifications sent from the backend. It is completely independent and focuses solely on UI/UX concerns.

---

## Architecture

### Frontend-Only Stack

- **Framework:** Next.js 14+ with TypeScript
- **State Management:** React Context
- **Real-time:** SignalR (via existing service)
- **UI Components:** shadcn/ui + Tailwind CSS
- **Icons:** Lucide React
- **HTTP Client:** Native Fetch API (via existing apiService)

### Core Components

1. **NotificationService** - REST API communication layer
2. **NotificationContext** - Global state management with React Context
3. **useNotifications Hook** - Custom hook combining Context + SignalR
4. **NotificationCenter** - Main browsing UI
5. **NotificationToast** - Real-time popup alerts
6. **Entity Navigation Router** - Client-side routing logic

### Location

```
frontend/src/
├── services/
│   └── notificationService.ts              (API calls to backend)
├── contexts/
│   └── NotificationContext.tsx             (State management)
├── hooks/
│   └── useNotifications.ts                 (Hook for components)
├── components/notifications/
│   ├── NotificationCenter.tsx              (Main UI)
│   ├── NotificationToast.tsx               (Real-time alerts)
│   └── ... other components
├── utils/
│   └── notificationNavigation.ts           (Client-side routing)
└── app/
    └── layout.tsx                          (Root provider integration)
```

---

## Core Concepts

### 1. NotificationService - REST API Layer

**File:** `src/services/notificationService.ts`

Handles all communication with backend `/api/notifications` endpoint.

**Methods:**
```typescript
getNotifications(params)                // Fetch notifications
markAsRead(id)                          // Mark single as read
markAllAsRead()                         // Mark all as read
deleteNotification(id)                  // Delete notification
getUnreadCount()                        // Get unread count
getNotificationPreferences()            // Get user preferences
updateNotificationPreference(category)  // Update preferences
sendNotification(type, recipientId, ...) // Send (admin)
```

**Example:**
```typescript
import notificationService from '@/services/notificationService';

// Fetch notifications
const result = await notificationService.getNotifications({
  page: 1,
  pageSize: 50,
  priority: 'High'
});

// Mark as read
await notificationService.markAsRead('notif-123');
```

### 2. NotificationContext - State Management

**File:** `src/contexts/NotificationContext.tsx`

Global React Context managing notification state.

**State:**
```typescript
notifications: Notification[]      // All notifications
unreadCount: number                // Unread count
loading: boolean                   // Loading state
error: string | null               // Error message
```

**Actions:**
```typescript
fetchNotifications(page?, pageSize?)    // Fetch from backend
markAsRead(id)                          // Mark read
markAllAsRead()                         // Mark all read
deleteNotification(id)                  // Delete
addNotification(notif)                  // Add (from SignalR)
clearError()                            // Clear error
```

**Usage:**
```typescript
import { useNotificationContext } from '@/contexts/NotificationContext';

function MyComponent() {
  const { notifications, unreadCount, markAsRead } = useNotificationContext();
  // Use state and actions
}
```

### 3. useNotifications Hook - Developer Interface

**File:** `src/hooks/useNotifications.ts`

Custom React hook combining Context + SignalR for developers.

**Returns:**
```typescript
{
  // State
  notifications: Notification[]
  unreadCount: number
  loading: boolean
  error: string | null
  
  // Actions
  fetchNotifications(page?, pageSize?)
  markAsRead(id)
  markAllAsRead()
  deleteNotification(id)
  clearError()
  
  // Helpers
  handleNotificationClick(id, actionUrl?)
  dismissNotification(id)
  filterNotifications(filter)
  getUnreadCount()
  
  // Real-time status
  isConnected: boolean
  connectionState: HubConnectionState
}
```

**Usage in Components:**
```typescript
'use client';
import { useNotifications } from '@/hooks/useNotifications';

export function NotificationBell() {
  const { unreadCount } = useNotifications();
  
  return (
    <div className="relative">
      <Bell className="h-6 w-6" />
      {unreadCount > 0 && (
        <span className="absolute top-0 right-0 bg-red-500 text-white text-xs rounded-full">
          {unreadCount}
        </span>
      )}
    </div>
  );
}
```

### 4. NotificationCenter Component - Main UI

**File:** `src/components/notifications/NotificationCenter.tsx`

Full-featured notification browsing interface.

**Features:**
- List all notifications
- Filter by Priority, Status, Read/Unread
- Full-text search
- Mark as read/delete
- Real-time updates via SignalR
- Click to navigate to entity

**Usage:**
```typescript
import { NotificationCenter } from '@/components/notifications/NotificationCenter';

export default function Page() {
  return (
    <div>
      <NotificationCenter />
    </div>
  );
}
```

### 5. NotificationToast Component - Real-time Alerts

**File:** `src/components/notifications/NotificationToast.tsx`

Displays notifications in real-time as they arrive.

**Features:**
- Auto-dismisses after 5 seconds
- Priority-based color coding
- Configurable position (top-right by default)
- Shows entity type and ID
- Manual dismiss option

**Usage:**
```typescript
import { NotificationToast } from '@/components/notifications/NotificationToast';

// Already added to root layout, no manual integration needed
<NotificationToast position="top-right" maxToasts={3} autoDismissDelay={5000} />
```

### 6. Entity Navigation Router - Smart Routing

**File:** `src/utils/notificationNavigation.ts`

Configuration-based client-side routing from notifications to pages.

**Functions:**
```typescript
getEntityNavigationUrl(entityType, entityId)   // Get URL for entity
getEntityDisplayName(entityType)               // Human-readable name
getEntityIconName(entityType)                  // Lucide icon
getEntityModule(entityType)                    // Module grouping
isNavigableEntity(entityType)                  // Check if navigable
getAvailableEntityTypes()                      // List all
getEntitiesByModule(module)                    // Get by module
registerEntity(type, config)                   // Register dynamic
getEntityConfiguration()                       // Get full config
```

**Configuration:**
```typescript
const ENTITY_CONFIG: Record<string, EntityConfig> = {
  'JobCard': {
    displayName: 'Job Card',
    iconName: 'FileText',
    routePattern: '/maintenance/job-cards/{id}',
    module: 'maintenance'
  },
  'Invoice': {
    displayName: 'Invoice',
    iconName: 'FileText',
    routePattern: '/finance/invoices/{id}',
    module: 'finance'
  },
  // ... more entities
};
```

---

## Data Flow

### Frontend Receives Notification

```
Backend UnifiedNotificationService
    ↓
SignalR Hub broadcasts NewNotification
    ↓
useNotifications hook receives via SignalR
    ↓
NotificationContext.addNotification() called
    ↓
State updated, components re-render
    ↓
NotificationToast displays real-time alert
NotificationCenter updates list
```

### User Clicks Notification

```
User clicks notification in UI
    ↓
handleNotificationClick() called
    ↓
markAsRead(id) sent to backend
    ↓
getEntityNavigationUrl() computes URL
    ↓
Navigate to page (e.g., /maintenance/job-cards/jc-123)
```

---

## Integration Points

### Root Layout (`src/app/layout.tsx`)

```typescript
import { NotificationProvider } from '@/contexts/NotificationContext';
import { NotificationToast } from '@/components/notifications/NotificationToast';

export default function RootLayout({ children }) {
  return (
    <html>
      <body>
        <NotificationProvider>
          {children}
          <NotificationToast position="top-right" />
        </NotificationProvider>
      </body>
    </html>
  );
}
```

### In Components

```typescript
'use client';
import { useNotifications } from '@/hooks/useNotifications';

export function MyComponent() {
  const { notifications, unreadCount, markAsRead } = useNotifications();
  
  return (
    <div>
      <h2>Notifications ({unreadCount} unread)</h2>
      {notifications.map(notif => (
        <div key={notif.id}>
          <h4>{notif.title}</h4>
          <p>{notif.message}</p>
          <button onClick={() => markAsRead(notif.id)}>
            Mark as read
          </button>
        </div>
      ))}
    </div>
  );
}
```

---

## Backend Integration Contract

### What Backend Sends

```json
{
  "id": "notif-123",
  "recipientId": "user-456",
  "notificationType": "InApp",
  "title": "Job Card Submitted",
  "message": "Job Card JC-001 submitted for approval",
  "priority": "High",
  "status": "Sent",
  "entityId": "jc-789",
  "entityType": "JobCard",
  "actionUrl": null,
  "isRead": false,
  "createdAt": "2025-11-07T18:59:00Z",
  "attemptCount": 0,
  "tenantId": "tenant-1"
}
```

### Frontend Requirements

- **entityType** must match `ENTITY_CONFIG` key exactly (PascalCase)
- **entityId** must be URL-safe string
- **actionUrl** should be null (frontend computes from entityType + entityId)
- **priority** determines UI color and urgency
- **status** determines display behavior

---

## Frontend Notification Data Structure

```typescript
interface Notification {
  id: string;
  recipientId: string;
  notificationType: 'Email' | 'SMS' | 'Push' | 'InApp';
  title: string;
  message: string;
  priority: 'Low' | 'Normal' | 'High' | 'Critical';
  status: 'Pending' | 'Sent' | 'Failed' | 'Expired' | 'Cancelled';
  entityId: string;
  entityType: string;
  actionUrl?: string;
  isRead: boolean;
  readAt?: string;
  dismissedAt?: string;
  createdAt: string;
  scheduledFor?: string;
  sentAt?: string;
  expiresAt?: string;
  attemptCount: number;
  lastError?: string;
  deliveryMethods?: string[];
  emailAddress?: string;
  phoneNumber?: string;
  additionalData?: Record<string, any>;
  tenantId: string;
}
```

---

## Common Tasks

### Display Unread Count in Header

```typescript
'use client';
import { useNotifications } from '@/hooks/useNotifications';
import { Bell } from 'lucide-react';

export function NotificationIcon() {
  const { unreadCount } = useNotifications();
  
  return (
    <button className="relative">
      <Bell />
      {unreadCount > 0 && (
        <span className="absolute -top-1 -right-1 bg-red-600 text-white text-xs rounded-full px-1">
          {unreadCount}
        </span>
      )}
    </button>
  );
}
```

### Show Notification Center in Modal

```typescript
import { NotificationCenter } from '@/components/notifications/NotificationCenter';
import { Dialog, DialogContent } from '@/components/ui/dialog';
import { useState } from 'react';

export function NotificationsModal() {
  const [open, setOpen] = useState(false);
  
  return (
    <>
      <button onClick={() => setOpen(true)}>View Notifications</button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-2xl">
          <NotificationCenter />
        </DialogContent>
      </Dialog>
    </>
  );
}
```

### Filter Notifications by Entity Type

```typescript
'use client';
import { useNotifications } from '@/hooks/useNotifications';

export function InvoiceNotifications() {
  const { notifications } = useNotifications();
  
  const invoiceNotifications = notifications.filter(
    n => n.entityType === 'Invoice'
  );
  
  return (
    <div>
      <h3>Invoice Notifications ({invoiceNotifications.length})</h3>
      {invoiceNotifications.map(n => (
        <div key={n.id}>
          <p>{n.title}</p>
        </div>
      ))}
    </div>
  );
}
```

---

## Adding New Entity Types

### When Backend Adds New Module

**Example: Adding Invoice notifications**

**Step 1:** Backend dev adds Invoice to notification system
- Sends notifications with `entityType: "Invoice"`

**Step 2:** Frontend dev adds to entity config

File: `src/utils/notificationNavigation.ts`

```typescript
const ENTITY_CONFIG: Record<string, EntityConfig> = {
  // ... existing ...
  'Invoice': {
    displayName: 'Invoice',
    iconName: 'FileText',
    routePattern: '/finance/invoices/{id}',
    module: 'finance'
  }
};
```

**Step 3:** Done! Frontend automatically:
- Shows Invoice notifications
- Displays FileText icon
- Navigates to `/finance/invoices/{id}` on click
- Groups with other finance notifications

---

## Styling & Theming

### Priority Colors

```typescript
// In components/notifications/NotificationCenter.tsx
const getPriorityColor = (priority: string) => {
  switch (priority) {
    case 'Critical': return 'destructive';      // Red
    case 'High': return 'secondary';            // Yellow
    case 'Normal': return 'default';            // Blue
    case 'Low': return 'outline';               // Gray
    default: return 'outline';
  }
};
```

### Dark Mode Support

All components use shadcn/ui which supports dark mode automatically:

```typescript
// Components automatically respond to dark/light mode
<div className="bg-white dark:bg-gray-900">
  {/* Content */}
</div>
```

---

## Performance Optimization

### Pagination

```typescript
// Load 50 notifications per page
const { notifications } = useNotifications();

// Fetch next page
await fetchNotifications(2, 50);
```

### Memoization

NotificationCenter uses `useMemo` for filters:

```typescript
const filteredNotifications = useMemo(() => {
  // Expensive filtering logic
}, [notifications, filters]);
```

### Lazy Loading

Notifications load on mount, but filtered/searched results are client-side:

```typescript
useEffect(() => {
  fetchNotifications();  // Initial load
}, []);

// Search is client-side
const filtered = notifications.filter(n =>
  n.title.includes(searchQuery)
);
```

---

## Debugging

### Check Connected to SignalR

```typescript
import signalRService from '@/services/signalr.service';

console.log('SignalR connected:', signalRService.isConnected);
```

### View All Notifications

```typescript
import { useNotifications } from '@/hooks/useNotifications';

function DebugComponent() {
  const { notifications } = useNotifications();
  console.log('All notifications:', notifications);
}
```

### Check Entity Configuration

```typescript
import { getEntityConfiguration } from '@/utils/notificationNavigation';

console.log('Entity config:', getEntityConfiguration());
```

### Verify Navigation URL

```typescript
import { getEntityNavigationUrl } from '@/utils/notificationNavigation';

const url = getEntityNavigationUrl('Invoice', 'inv-123');
console.log('Navigation URL:', url); // /finance/invoices/inv-123
```

---

## File Structure

```
frontend/src/
├── services/
│   └── notificationService.ts              (~300 lines)
│       └── REST API calls to backend
│
├── contexts/
│   └── NotificationContext.tsx             (~170 lines)
│       └── Global state management
│
├── hooks/
│   └── useNotifications.ts                 (~160 lines)
│       └── Custom hook with SignalR
│
├── components/notifications/
│   ├── NotificationCenter.tsx              (~350 lines)
│   │   └── Main notification hub UI
│   ├── NotificationToast.tsx               (~180 lines)
│   │   └── Real-time toast alerts
│   └── ... other notification components
│
├── utils/
│   └── notificationNavigation.ts           (~250 lines)
│       └── Entity routing configuration
│
└── app/
    └── layout.tsx                          (2 lines modified)
        └── Root provider integration

Documentation:
├── FRONTEND_NOTIFICATIONS.md               (This file - Frontend only)
├── BACKEND_NOTIFICATIONS.md                (Backend only)
└── NOTIFICATIONS_QUICK_REFERENCE.txt       (Both)
```

---

## Browser Support

- ✅ Chrome/Edge - Full support
- ✅ Firefox - Full support
- ✅ Safari - Full support
- ⚠️ IE11 - Not tested

---

## Testing

### Using NotificationCenter

```typescript
import { NotificationCenter } from '@/components/notifications/NotificationCenter';

export default function NotificationsPage() {
  return <NotificationCenter />;
}
```

### Manual Test with Mock Data

```typescript
const { addNotification } = useNotifications();

addNotification({
  id: 'test-1',
  recipientId: 'user-1',
  notificationType: 'InApp',
  title: 'Test Notification',
  message: 'This is a test',
  priority: 'High',
  status: 'Sent',
  entityType: 'JobCard',
  entityId: 'jc-123',
  isRead: false,
  createdAt: new Date().toISOString(),
  attemptCount: 0,
  tenantId: 'tenant-1'
});
```

---

## Summary

The frontend notification system:

✅ Displays real-time notifications
✅ Manages notification state
✅ Provides smart entity navigation
✅ Filters and searches
✅ Handles read/unread status
✅ Integrates with backend via REST + SignalR
✅ Follows existing frontend patterns
✅ Fully typed with TypeScript
✅ Responsive and accessible
✅ Scales with new entities via configuration

All without needing to understand backend implementation!
