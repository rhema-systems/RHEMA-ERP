# 🎨 UI Components Documentation

## Overview

This document describes the UI components and features implemented in the ERP System frontend.

## Enhanced Data Tables

### Features
- **Striped Rows**: Alternating background colors for better readability
- **Compact Design**: Reduced row height and padding for large datasets
- **Clickable Selection**: Click rows to select, supports multi-selection
- **Export Functionality**: Export data to CSV, Excel, PDF formats
- **Pagination**: Handle large datasets efficiently
- **Sorting**: Click column headers to sort data
- **Filtering**: Search and filter table data

### Usage Example
```tsx
import { DataTable } from '@/components/admin/data-table';

<DataTable
  data={users}
  columns={userColumns}
  onRowClick={(row) => handleRowSelect(row)}
  enableExport={true}
  enableSelection={true}
/>
```

### Props
- `data`: Array of data objects
- `columns`: Column definitions with header, accessor, and formatting
- `onRowClick`: Callback for row selection
- `enableExport`: Show export buttons
- `enableSelection`: Allow row selection

## Authentication Components

### Login Form
- **Multi-tenant Support**: Tenant selection dropdown
- **OTP Verification**: Two-factor authentication support
- **Remember Me**: Persistent login sessions
- **Error Handling**: User-friendly error messages
- **Success Messages**: Persistent success notifications

### Features
- Form validation with real-time feedback
- Password strength indicator
- Account lockout protection
- CAPTCHA support for security

## Phone Input Component

### Features
- **Country Code Selection**: Dropdown with country flags
- **Format Validation**: Automatic phone number formatting
- **International Support**: Support for various phone formats

### Usage
```tsx
import { PhoneInput } from '@/components/ui/phone-input';

<PhoneInput
  value={phoneNumber}
  onChange={setPhoneNumber}
  placeholder="Enter phone number"
  defaultCountry="US"
/>
```

## Password Components

### Password Meter
- **Strength Indicators**: Visual password strength feedback
- **Policy Validation**: Real-time policy compliance checking
- **Suggestions**: Helpful suggestions for stronger passwords

## Tenant Branding

### Features
- **Logo Upload**: Custom tenant logos with preview
- **Color Customization**: Brand color selection
- **Favicon Support**: Custom tenant favicons
- **Background Images**: Tenant-specific background images

## Navigation & Layout

### Sidebar Navigation
- **Collapsible**: Minimize sidebar for more screen space
- **Role-based**: Menu items based on user permissions
- **Responsive**: Mobile-friendly navigation
- **Icons**: Clear visual indicators for menu items

### Responsive Design
- **Mobile-first**: Optimized for mobile devices
- **Tablet Support**: Enhanced tablet experience
- **Desktop**: Full-featured desktop interface

## Form Components

### Enhanced Forms
- **Real-time Validation**: Immediate feedback on form fields
- **Error States**: Clear error messaging and styling
- **Loading States**: Visual feedback during form submission
- **Auto-save**: Automatic draft saving for long forms

## Styling & Theme

### Design System
- **Tailwind CSS**: Utility-first CSS framework
- **Consistent Colors**: Standardized color palette
- **Typography**: Consistent font sizes and spacing
- **Components**: Reusable UI components

### Dark Mode Support
- **Theme Toggle**: Switch between light and dark modes
- **System Preference**: Respect user's system theme
- **Consistent**: All components support both themes

## Accessibility

### Features
- **Keyboard Navigation**: Full keyboard support
- **Screen Reader**: ARIA labels and descriptions
- **High Contrast**: Support for high contrast mode
- **Focus Management**: Clear focus indicators

## Performance Optimizations

### Features
- **Code Splitting**: Lazy loading of components
- **Image Optimization**: Next.js image optimization
- **Caching**: Efficient API response caching
- **Bundle Size**: Optimized bundle sizes

---

*For detailed component API documentation, see the inline TypeScript definitions.*
*Last updated: $(date)*
## Component Usage Statistics

| Component | Files | Usage Count |
|-----------|-------|-------------|
| DataTable | 13 | 16 |
| Button | 269 | 270 |
| Input | 196 | 200 |
| Select | 141 | 141 |
| Card | 250 | 257 |
| Badge | 211 | 211 |
| PhoneInput | 3 | 3 |
