# Frontend Notification System - Implementation Guide

## Quick Answer: Adding New Entities

**Yes, you need to add new entities to the configuration as they are developed.** However, we've made this simple and maintainable:

### For each new entity (Invoice, Customer, etc.):

Edit `src/utils/notificationNavigation.ts` and add to `ENTITY_CONFIG`:

```typescript
'Invoice': {
  displayName: 'Invoice',
  iconName: 'FileText',
  routePattern: '/finance/invoices/{id}',
  module: 'finance'
},
'Customer': {
  displayName: 'Customer',
  iconName: 'Users',
  routePattern: '/crm/customers/{id}',
  module: 'crm'
}
```

That's it! The system automatically handles:
- Navigation when clicking notifications
- Display names and icons
- Grouping by module

---

## Architecture Overview

### Core Components

1. **NotificationService** - API communication layer
2. **NotificationContext** - Global state management  
3. **useNotifications Hook** - React hook with SignalR integration
4. **NotificationCenter** - Full notification UI
5. **NotificationToast** - Real-time toast alerts
6. **Entity Navigation Router** - Smart routing to entities

### Data Flow

```
Backend NotificationService
    ↓
SignalR Hub (Real-time)
    ↓
useNotifications Hook
    ↓
NotificationContext (State)
    ↓
NotificationCenter + NotificationToast (UI)
    ↓
Entity Navigation Router → Smart routing
```

---

## Unified Notification Structure

All notifications follow this structure (aligned with backend):

```typescript
interface Notification {
  id: string;
  recipientId: string;
  notificationType: 'Email' | 'SMS' | 'Push' | 'InApp';
  title: string;
  message: string;
  priority: 'Low' | 'Normal' | 'High' | 'Critical';
  status: 'Pending' | 'Sent' | 'Failed' | 'Expired';
  entityId: string;              // e.g., "invoice-123", "cust-456"
  entityType: string;            // e.g., "Invoice", "Customer"
  actionUrl?: string;            // Computed by frontend from entityType+entityId
  isRead: boolean;
  createdAt: string;
  attemptCount: number;
  lastError?: string;
  additionalData?: Record<string, any>;
}
```

---

## Step-by-Step: Adding a New Module

### Example: Adding Financial/Invoice Module

#### 1. Register Entity Types

In `src/utils/notificationNavigation.ts`:

```typescript
const ENTITY_CONFIG: Record<string, EntityConfig> = {
  // ... existing entities ...
  
  // NEW: Financial Module
  'Invoice': {
    displayName: 'Invoice',
    iconName: 'FileText',
    routePattern: '/finance/invoices/{id}',
    module: 'finance'
  },
  'Payment': {
    displayName: 'Payment',
    iconName: 'CreditCard',
    routePattern: '/finance/payments/{id}',
    module: 'finance'
  },
  'PurchaseOrder': {
    displayName: 'Purchase Order',
    iconName: 'ShoppingCart',
    routePattern: '/procurement/purchase-orders/{id}',
    module: 'procurement'
  }
};
```

#### 2. Backend Sends Notifications

Backend's UnifiedNotificationService sends:

```csharp
await notificationService.CreateNotificationAsync(new CreateNotificationDto
{
  RecipientId = userId,
  Type = "InApp",
  Title = "Invoice Created",
  Message = "Invoice INV-001 has been created",
  Priority = "Normal",
  EntityType = "Invoice",      // Must match config key
  EntityId = "invoice-12345",  // Unique entity ID
  ActionUrl = null             // Frontend will compute it
});
```

#### 3. Frontend Automatically Handles

- **Notification Center** shows with FileText icon
- **Toast** pops up for real-time delivery
- **Click handling** → navigates to `/finance/invoices/invoice-12345`
- **Filtering** → groups with other finance notifications
- **Navigation** → smart routing based on entityType

---

## Available Entity Navigation Functions

Use these in your components:

```typescript
import { 
  getEntityNavigationUrl,
  getEntityDisplayName,
  getEntityIconName,
  getEntityModule,
  isNavigableEntity,
  getAvailableEntityTypes,
  getEntitiesByModule,
  registerEntity
} from '@/utils/notificationNavigation';

// Get URL for navigation
const url = getEntityNavigationUrl('Invoice', 'inv-123');
// Returns: '/finance/invoices/inv-123'

// Get display name
const name = getEntityDisplayName('Invoice');
// Returns: 'Invoice'

// Get icon name (Lucide)
const icon = getEntityIconName('Invoice');
// Returns: 'FileText'

// Get module
const module = getEntityModule('Invoice');
// Returns: 'finance'

// Check if navigable
const navigable = isNavigableEntity('Invoice');
// Returns: true

// Get all entities
const all = getAvailableEntityTypes();
// Returns: ['JobCard', 'WorkOrder', 'Invoice', 'Customer', ...]

// Get finance entities only
const finance = getEntitiesByModule('finance');
// Returns: ['Invoice', 'Payment', 'PurchaseOrder']

// Register entity dynamically (at runtime)
registerEntity('DynamicEntity', {
  displayName: 'Dynamic Entity',
  iconName: 'Box',
  routePattern: '/custom/{id}',
  module: 'custom'
});
```

---

## Using Notifications in Components

### Display Unread Count in Header

```typescript
'use client';
import { useNotifications } from '@/hooks/useNotifications';
import { Bell } from 'lucide-react';

export function NotificationBell() {
  const { unreadCount } = useNotifications();
  
  return (
    <div className="relative">
      <Bell className="h-6 w-6" />
      {unreadCount > 0 && (
        <span className="absolute top-0 right-0 bg-red-500 text-white text-xs rounded-full w-5 h-5 flex items-center justify-center">
          {unreadCount}
        </span>
      )}
    </div>
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
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent className="max-w-2xl max-h-96">
        <NotificationCenter />
      </DialogContent>
    </Dialog>
  );
}
```

### Filter Notifications by Entity Type

```typescript
'use client';
import { useNotifications } from '@/hooks/useNotifications';

export function InvoiceNotifications() {
  const { notifications } = useNotifications();
  
  const invoiceNotifications = notifications.filter(n => n.entityType === 'Invoice');
  
  return (
    <div>
      <h3>Invoice Notifications ({invoiceNotifications.length})</h3>
      {invoiceNotifications.map(notif => (
        <div key={notif.id} className="p-4 border">
          <h4>{notif.title}</h4>
          <p>{notif.message}</p>
        </div>
      ))}
    </div>
  );
}
```

---

## Best Practices

### Entity Type Naming
- Use **PascalCase**: `Invoice`, `Customer`, `PurchaseOrder`
- Not snake_case or kebab-case
- Must be unique across system

### Entity ID Format
- Use **URL-safe strings**: `inv-20251107-001`, `cust-uuid-here`
- Avoid special characters
- IDs are URL-encoded automatically

### Priority Levels
- **Critical** - System errors, security issues
- **High** - Important approvals, rejections
- **Normal** - Standard workflows
- **Low** - Informational, reminders

### Lucide Icons
Available icon names for entity types:
- FileText, Wrench, Package, LogIn, LogOut, Shield
- Users, Target, TrendingUp, Box, ArrowRightLeft
- User, Calendar, CreditCard, ShoppingCart, Settings
- See [Lucide icons](https://lucide.dev) for full list

---

## Entity Configuration Reference

```typescript
interface EntityConfig {
  displayName: string;      // Human-readable name
  iconName: string;         // Lucide icon name
  routePattern: string;     // Route with {id} placeholder
  module?: string;          // Module grouping (e.g., 'finance', 'crm')
}
```

### Current Entities

**Maintenance Module:**
- JobCard → `/maintenance/job-cards/{id}`
- WorkOrder → `/maintenance/work-orders/{id}`
- Asset → `/maintenance/assets/{id}`
- AssetAdmission → `/maintenance/asset-admission/{id}`
- AssetDischarge → `/maintenance/asset-discharge/{id}`
- Quality → `/maintenance/quality-control/{id}`

**Financial Module (ready to implement):**
- Invoice → `/finance/invoices/{id}`
- Payment → `/finance/payments/{id}`

**CRM Module (ready to implement):**
- Customer → `/crm/customers/{id}`
- Lead → `/crm/leads/{id}`
- Opportunity → `/crm/opportunities/{id}`

**Procurement Module (ready to implement):**
- PurchaseOrder → `/procurement/purchase-orders/{id}`

---

## Common Tasks

### List All Finance Module Entities
```typescript
import { getEntitiesByModule } from '@/utils/notificationNavigation';

const financeEntities = getEntitiesByModule('finance');
// Returns: ['Invoice', 'Payment']
```

### Get URL for Unknown Entity
```typescript
import { getEntityNavigationUrl, isNavigableEntity } from '@/utils/notificationNavigation';

function navigateToEntity(entityType: string, entityId: string) {
  if (isNavigableEntity(entityType)) {
    const url = getEntityNavigationUrl(entityType, entityId);
    window.location.href = url;
  } else {
    console.warn(`Entity type '${entityType}' not configured`);
  }
}
```

### Register Plugin/Dynamic Module
```typescript
import { registerEntity } from '@/utils/notificationNavigation';

// When plugin initializes
registerEntity('CustomReport', {
  displayName: 'Custom Report',
  iconName: 'FileText',
  routePattern: '/plugins/custom-reports/{id}',
  module: 'plugins'
});
```

---

## Integration with Root Layout

Already added to `src/app/layout.tsx`:

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

No additional setup needed - notifications work globally!

---

## Troubleshooting

### Notifications Not Showing

1. **Check entity type is configured:**
   ```typescript
   import { isNavigableEntity } from '@/utils/notificationNavigation';
   console.log(isNavigableEntity('MyEntity')); // Should be true
   ```

2. **Verify backend is sending:**
   - Check backend notification service is called
   - Verify entityType matches config key (case-sensitive)
   - Check entityId is not null

3. **Check SignalR connection:**
   ```typescript
   import signalRService from '@/services/signalr.service';
   console.log('Connected:', signalRService.isConnected);
   ```

### Navigation Not Working

- Entity type must match config key exactly
- Route pattern must be valid
- Entity ID must be URL-safe

### Missing Icon

- Check icon name exists in Lucide
- Use any icon from [lucide.dev](https://lucide.dev)
- Default is 'Package' if not found

---

## Summary

The notification system is **fully implemented and ready to use**. To add new entities:

1. **Add to `ENTITY_CONFIG`** in `src/utils/notificationNavigation.ts`
2. **Backend sends notifications** with matching entityType
3. **Everything works automatically** - no additional code needed

The system is designed to scale as your ERP grows!
