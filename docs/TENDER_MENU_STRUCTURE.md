# TENDER MANAGEMENT - MENU STRUCTURE

**Date:** 2025-11-30

This document outlines the complete menu structure for Tender Management across all portals.

---

## 🌐 EXTERNAL PORTAL MENU (Suppliers/Contractors)

**Base URL:** `http://localhost:3000/external-portal`

### Sidebar Menu Items to Add

**File to Update:** `frontend/src/components/external-portal/external-sidebar.tsx`

```typescript
const menuItems: MenuItem[] = [
  {
    title: 'Dashboard',
    href: '/external-portal',
    icon: Home,
  },
  {
    title: 'Business Partner Registration',
    href: '/external-portal/business-partner',
    icon: Building2,
  },
  // ========== NEW TENDER SECTION ==========
  {
    title: 'Tenders',
    href: '/external-portal/tenders',
    icon: FileText,
  },
  {
    title: 'My Bids',
    href: '/external-portal/my-bids',
    icon: ClipboardList,
  },
  // ========================================
  {
    title: 'Permit Applications',
    href: '/external-portal/permits',
    icon: FileText,
    comingSoon: true,
  },
  {
    title: 'Land Registration',
    href: '/external-portal/land-registration',
    icon: MapPin,
    comingSoon: true,
  },
  {
    title: 'My Profile',
    href: '/external-portal/profile',
    icon: User,
  },
  {
    title: 'Notifications',
    href: '/external-portal/notifications',
    icon: Bell,
    badge: '3',
  },
];
```

### Dashboard Quick Actions to Add

**File to Update:** `frontend/src/app/external-portal/page.tsx`

```typescript
const quickActions = [
  {
    title: 'Register as Business Partner',
    description: 'Submit your company registration as a supplier or contractor',
    icon: Building2,
    href: '/external-portal/business-partner',
    color: 'bg-blue-500',
    available: true,
  },
  // ========== NEW TENDER QUICK ACTIONS ==========
  {
    title: 'Browse Tenders',
    description: 'View and bid on published tenders and RFQs',
    icon: FileText,
    href: '/external-portal/tenders',
    color: 'bg-orange-500',
    available: true,
  },
  {
    title: 'My Bids',
    description: 'Track your submitted bids and view results',
    icon: ClipboardList,
    href: '/external-portal/my-bids',
    color: 'bg-purple-500',
    available: true,
  },
  // ==============================================
  {
    title: 'Apply for Permit',
    description: 'Submit permit applications for various activities',
    icon: FileText,
    href: '/external-portal/permits',
    color: 'bg-green-500',
    available: false,
  },
  {
    title: 'Land Registration',
    description: 'Register land ownership and property details',
    icon: MapPin,
    href: '/external-portal/land-registration',
    color: 'bg-purple-500',
    available: false,
  },
];
```

---

## 💼 PROCUREMENT MODULE MENU (Internal Staff)

**Base URL:** `http://localhost:3000/procurement`

### Main Procurement Menu Structure

**File to Update:** `frontend/src/app/procurement/layout.tsx`

```typescript
const procurementMenuItems = [
  {
    title: 'Dashboard',
    href: '/procurement',
    icon: LayoutDashboard,
  },
  {
    title: 'Business Partners',
    href: '/procurement/business-partners',
    icon: Building2,
  },
  // ========== NEW TENDER SECTION ==========
  {
    title: 'Tenders',
    icon: FileText,
    children: [
      {
        title: 'All Tenders',
        href: '/procurement/tenders',
        icon: List,
      },
      {
        title: 'Create Tender',
        href: '/procurement/tenders/create',
        icon: Plus,
      },
      {
        title: 'Pending Evaluation',
        href: '/procurement/tenders?status=evaluation',
        icon: ClipboardCheck,
      },
      {
        title: 'Awarded Tenders',
        href: '/procurement/tenders?status=awarded',
        icon: Award,
      },
    ],
  },
  {
    title: 'Bid Management',
    icon: ClipboardList,
    children: [
      {
        title: 'All Bids',
        href: '/procurement/bids',
        icon: List,
      },
      {
        title: 'Pending Evaluation',
        href: '/procurement/bids?status=pending',
        icon: Clock,
      },
      {
        title: 'Evaluated Bids',
        href: '/procurement/bids?status=evaluated',
        icon: CheckCircle,
      },
    ],
  },
  // ========================================
  {
    title: 'Purchase Requisitions',
    href: '/procurement/requisitions',
    icon: FileText,
  },
  {
    title: 'Purchase Orders',
    href: '/procurement/orders',
    icon: ShoppingCart,
  },
  {
    title: 'Supplier Comparison',
    href: '/procurement/supplier-comparison',
    icon: BarChart,
  },
];
```

---

## ⚙️ ADMINISTRATION MENU (Admin Settings)

**Base URL:** `http://localhost:3000/administration`

### Administration Procurement Section

**File to Update:** `frontend/src/app/administration/layout.tsx` or create new procurement admin section

```typescript
const administrationMenuItems = [
  // ... existing admin menu items ...
  
  {
    title: 'Procurement',
    icon: ShoppingCart,
    children: [
      {
        title: 'Business Partner Registrations',
        href: '/administration/procurement/registrations',
        icon: UserCheck,
      },
      {
        title: 'Partner Categories',
        href: '/administration/procurement/categories',
        icon: Tags,
      },
      {
        title: 'Contractor Specializations',
        href: '/administration/procurement/specializations',
        icon: Wrench,
      },
      {
        title: 'License Types',
        href: '/administration/procurement/license-types',
        icon: Award,
      },
      // ========== NEW TENDER ADMIN SECTION ==========
      {
        title: 'Tender Templates',
        href: '/administration/procurement/tender-templates',
        icon: FileText,
      },
      {
        title: 'Evaluation Criteria',
        href: '/administration/procurement/evaluation-criteria',
        icon: ClipboardCheck,
      },
      {
        title: 'Tender Settings',
        href: '/administration/procurement/tender-settings',
        icon: Settings,
      },
      // ==============================================
    ],
  },
];
```

---

## 📁 COMPLETE FILE STRUCTURE

### External Portal Routes
```
frontend/src/app/external-portal/
├── tenders/
│   ├── page.tsx (Tender Listing)
│   └── [id]/
│       ├── page.tsx (Tender Detail)
│       ├── submit/
│       │   └── page.tsx (Bid Submission Wizard)
│       └── clarifications/
│           └── page.tsx (Q&A)
└── my-bids/
    ├── page.tsx (My Bids Dashboard)
    └── [id]/
        └── page.tsx (Bid Detail)
```

### Procurement Module Routes (Internal)
```
frontend/src/app/procurement/
├── tenders/
│   ├── page.tsx (Tender List/Dashboard)
│   ├── create/
│   │   └── page.tsx (Tender Creation Wizard)
│   └── [id]/
│       ├── page.tsx (Tender Detail/Management)
│       ├── edit/
│       │   └── page.tsx (Edit Tender)
│       ├── evaluate/
│       │   └── page.tsx (Bid Evaluation)
│       └── award/
│           └── page.tsx (Award Management)
└── bids/
    ├── page.tsx (All Bids List)
    └── [id]/
        └── page.tsx (Bid Detail)
```

### Administration Routes
```
frontend/src/app/administration/procurement/
├── tender-templates/
│   ├── page.tsx (Template Management)
│   └── [id]/
│       └── page.tsx (Template Editor)
├── evaluation-criteria/
│   └── page.tsx (Criteria Configuration)
└── tender-settings/
    └── page.tsx (General Tender Settings)
```

---

## 🎨 MENU ICONS

### Import Required Icons

```typescript
import {
  FileText,        // Tenders
  ClipboardList,   // Bids
  Plus,            // Create
  List,            // List view
  ClipboardCheck,  // Evaluation
  Award,           // Awards
  Clock,           // Pending
  CheckCircle,     // Completed
  Settings,        // Settings
  Tags,            // Categories
  LayoutDashboard, // Dashboard
} from 'lucide-react';
```

---

## 🔐 PERMISSIONS

### External Portal (No special permissions needed)
- All authenticated external users can:
  - Browse published tenders
  - Submit bids
  - View their own bids
  - Ask clarifications

### Procurement Module (Internal)
- **procurement.tenders.view** - View tenders
- **procurement.tenders.create** - Create tenders
- **procurement.tenders.edit** - Edit tenders
- **procurement.tenders.publish** - Publish tenders
- **procurement.tenders.evaluate** - Evaluate bids
- **procurement.tenders.award** - Award tenders
- **procurement.bids.view** - View bids
- **procurement.bids.evaluate** - Evaluate bids

### Administration
- **procurement.admin.templates** - Manage templates
- **procurement.admin.criteria** - Manage evaluation criteria
- **procurement.admin.settings** - Manage tender settings

---

## 📊 DASHBOARD WIDGETS

### External Portal Dashboard
Add these widgets to `frontend/src/app/external-portal/page.tsx`:

```typescript
// Active Tenders Widget
{
  title: 'Active Tenders',
  value: activeTendersCount,
  icon: FileText,
  color: 'text-orange-600',
  bgColor: 'bg-orange-100',
}

// My Bids Widget
{
  title: 'My Bids',
  value: myBidsCount,
  icon: ClipboardList,
  color: 'text-purple-600',
  bgColor: 'bg-purple-100',
}

// Pending Evaluation Widget
{
  title: 'Under Evaluation',
  value: pendingEvaluationCount,
  icon: Clock,
  color: 'text-yellow-600',
  bgColor: 'bg-yellow-100',
}
```

### Procurement Dashboard
Add these widgets to `frontend/src/app/procurement/page.tsx`:

```typescript
// Active Tenders Widget
{
  title: 'Active Tenders',
  value: activeTendersCount,
  icon: FileText,
  color: 'text-blue-600',
  bgColor: 'bg-blue-100',
  href: '/procurement/tenders?status=published',
}

// Pending Evaluation Widget
{
  title: 'Pending Evaluation',
  value: pendingEvaluationCount,
  icon: ClipboardCheck,
  color: 'text-orange-600',
  bgColor: 'bg-orange-100',
  href: '/procurement/tenders?status=evaluation',
}

// Total Bids Widget
{
  title: 'Total Bids',
  value: totalBidsCount,
  icon: ClipboardList,
  color: 'text-purple-600',
  bgColor: 'bg-purple-100',
  href: '/procurement/bids',
}

// Awarded This Month Widget
{
  title: 'Awarded This Month',
  value: awardedThisMonthCount,
  icon: Award,
  color: 'text-green-600',
  bgColor: 'bg-green-100',
  href: '/procurement/tenders?status=awarded',
}
```


