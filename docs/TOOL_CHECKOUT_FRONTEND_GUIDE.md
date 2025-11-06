# Tool Checkout Frontend Implementation

## Overview
The Tool Checkout frontend provides a complete user interface for managing tool inventory, checkout operations, and returns. It's built with Next.js 14, React, TypeScript, and Tailwind CSS.

---

## Files Created

### 1. Service Layer
**File**: `frontend/src/services/toolCheckoutService.ts`

**Purpose**: API communication layer

**Methods**:
- `getAvailableTools()` - Get all available tools
- `getAllTools()` - Get all tools regardless of status
- `getToolById(toolId)` - Get specific tool details
- `checkToolAvailability(toolId, startDate, endDate)` - Check tool availability
- `getToolHistory(toolId, limit)` - Get tool checkout history
- `checkoutTool(toolId, employeeId, dto)` - Checkout tool
- `returnTool(checkoutId, dto)` - Return tool
- `reportDamage(checkoutId, dto)` - Report tool damage
- `getActiveCheckouts(employeeId)` - Get active checkouts
- `getOverdueCheckouts()` - Get overdue checkouts
- `getEmployeeCheckoutHistory(employeeId, limit)` - Get employee history

### 2. Main Page
**File**: `frontend/src/app/maintenance/tools/page.tsx`

**Purpose**: Main tool management interface

**Features**:
- Tool inventory display with search
- Status badges (Available, In Use, Maintenance, Out of Service)
- Statistics dashboard (Total, Available, Active, Overdue)
- Three tabs: All Tools, Active Checkouts, Overdue
- Checkout dialog
- Return dialog with damage reporting
- Real-time status updates

---

## How to Access

1. **Navigate to Maintenance Module**:
   ```
   http://localhost:3000/maintenance
   ```

2. **Click "Tool Management" card** (cyan color with Package icon)

3. **Or navigate directly**:
   ```
   http://localhost:3000/maintenance/tools
   ```

---

## User Interface Features

### Dashboard Stats (Top)
- **Total Tools**: Count of all tools in inventory
- **Available**: Green badge showing available tools
- **Active Checkouts**: Blue badge showing currently checked-out tools
- **Overdue**: Red badge showing overdue returns

### Tab 1: All Tools
**Features**:
- Search bar (search by name, code, or category)
- Grid layout with tool cards
- Each card shows:
  - Tool name and code
  - Status badge
  - Category
  - Current location
  - Daily rental rate
  - Total usage days
  - Certification requirement badge (if applicable)
  - "Checkout Tool" button (only for available tools)

### Tab 2: Active Checkouts
**Features**:
- List of currently checked-out tools
- Each card shows:
  - Tool name
  - Checked out by (employee name)
  - Days out badge
  - Checkout date
  - Expected return date
  - Work order number (if linked)
  - "Return Tool" button

### Tab 3: Overdue
**Features**:
- Red-themed cards for overdue tools
- Shows days overdue
- "Return Tool Now" button (destructive styling)
- Empty state with green check icon when no overdue tools

---

## User Workflows

### Workflow 1: Checkout a Tool

1. **Navigate to "All Tools" tab**
2. **Search or browse** for the tool you need
3. **Click "Checkout Tool"** button on available tool
4. **Fill checkout form**:
   - Employee ID (GUID) *
   - Condition (Good/Fair/Damaged) *
   - Expected Return Date (optional)
   - Notes (optional)
5. **Click "Checkout"** button
6. **Success toast** appears
7. **Tool status** automatically updates to "In Use"
8. **Tool appears** in "Active Checkouts" tab

### Workflow 2: Return a Tool

1. **Navigate to "Active Checkouts"** or "Overdue" tab
2. **Find the tool** to return
3. **Click "Return Tool"** button
4. **Fill return form**:
   - Condition (Good/Fair/Damaged) *
   - Report Damage checkbox
   - If damage reported:
     - Damage Description *
     - Estimated Cost (optional)
   - Return Notes (optional)
5. **Click "Return Tool"** button
6. **Success toast** appears (shows overdue days if applicable)
7. **Tool status** automatically updates to "Available" (or "Maintenance" if damaged)
8. **Tool removed** from active checkouts

### Workflow 3: Search and Filter

1. **Use search bar** in "All Tools" tab
2. **Type** tool name, code, or category
3. **Results filter** in real-time
4. **Search is case-insensitive**

### Workflow 4: Monitor Overdue Tools

1. **Check statistics card** at top (red "Overdue" count)
2. **Navigate to "Overdue" tab**
3. **View all overdue tools** with red styling
4. **Days overdue** displayed in badge
5. **Quick return** via "Return Tool Now" button

---

## Status Management

### Tool Status Flow
```
Available → (Checkout) → InUse → (Return Good/Fair) → Available
                                → (Return Damaged) → Maintenance
```

### Visual Indicators
- **Available**: Default blue badge
- **In Use**: Secondary gray badge
- **Maintenance**: Destructive red badge
- **Out of Service**: Outline badge

### Automatic Updates
- Status changes immediately after checkout/return
- Real-time reflection across all tabs
- Active checkouts update automatically
- Overdue detection happens on page load

---

## Damage Reporting

### When to Report Damage
- Tool is damaged during use
- Tool requires repair
- Tool needs calibration
- Tool has visible defects

### Damage Report Process
1. **Check "Report Damage"** checkbox in return dialog
2. **Fill damage description** (required)
3. **Add estimated cost** (optional)
4. **Submit return**
5. **Tool automatically flagged** for maintenance
6. **Status changes to "Maintenance"**
7. **Tool removed** from available inventory

---

## Data Validation

### Required Fields
**Checkout**:
- Employee ID
- Condition

**Return**:
- Condition
- Damage Description (if damage reported)

### Field Validation
- Employee ID must be valid GUID
- Dates must be in correct format
- Damage cost must be numeric
- All required fields checked before submission

---

## Error Handling

### API Errors
- Toast notifications for all errors
- Descriptive error messages
- User-friendly error descriptions

### Common Errors
| Error | Cause | Solution |
|-------|-------|----------|
| "Tool not available" | Tool already checked out or in maintenance | Check tool status |
| "Tool not found" | Invalid tool ID | Verify tool exists |
| "Checkout not found" | Invalid checkout ID | Check active checkouts |
| "Failed to load data" | API connection issue | Check network/backend |

### Error Display
- Red toast notification
- Error title and description
- Automatic dismissal after 5 seconds
- Non-blocking (user can continue working)

---

## Configuration

### Environment Variables
**Required**: `NEXT_PUBLIC_API_URL`

**Default**: `https://localhost:7095/api`

**Set in**: `.env.local`
```bash
NEXT_PUBLIC_API_URL=https://your-api-url/api
```

### Authentication
- Uses JWT Bearer token from localStorage
- Token key: `authToken`
- Automatically included in all API requests
- 401 errors if token invalid/expired

---

## Styling & Theming

### Color Scheme
- **Primary**: Blue (checkouts, active state)
- **Success**: Green (available, success)
- **Danger**: Red (overdue, errors)
- **Warning**: Yellow (warnings)
- **Info**: Cyan (tool management card)

### Responsive Design
- **Mobile**: Single column layout
- **Tablet**: 2 columns for tool cards
- **Desktop**: 3 columns for tool cards
- **Stats**: 1 column (mobile) → 4 columns (desktop)

### Components Used
- **shadcn/ui**: Card, Button, Input, Label, Badge, Dialog, Select, Textarea, Tabs
- **lucide-react**: Icons (Package, CheckCircle2, Clock, AlertTriangle, etc.)
- **sonner**: Toast notifications

---

## Performance Optimization

### Data Loading
- Parallel API calls on page load (Promise.all)
- Single loading state for all data
- Refresh button for manual updates

### Search Performance
- Client-side filtering (instant)
- No API calls during search
- Case-insensitive matching

### State Management
- React useState hooks
- Minimal re-renders
- Optimized component structure

---

## Testing the UI

### Manual Testing Checklist

**Initial Load**:
- [ ] Page loads without errors
- [ ] Statistics display correctly
- [ ] All tabs render
- [ ] Tools display in grid

**Checkout Flow**:
- [ ] Can open checkout dialog
- [ ] Employee ID validation works
- [ ] Condition dropdown works
- [ ] Date picker functional
- [ ] Success toast appears
- [ ] Tool status updates
- [ ] Dialog closes after checkout

**Return Flow**:
- [ ] Can open return dialog
- [ ] Condition dropdown works
- [ ] Damage checkbox toggles fields
- [ ] Success toast with overdue info
- [ ] Tool status updates
- [ ] Dialog closes after return

**Search & Filter**:
- [ ] Search bar filters tools
- [ ] Case-insensitive search works
- [ ] Results update instantly
- [ ] Empty state handled

**Overdue Handling**:
- [ ] Overdue tab shows correct tools
- [ ] Red styling applied
- [ ] Days overdue calculated correctly
- [ ] Empty state shows when no overdue

---

## Future Enhancements

### Planned Features
- [ ] Barcode scanner integration
- [ ] QR code generation for tools
- [ ] Tool reservation system
- [ ] Batch checkout/return
- [ ] Advanced filters (category, location, status)
- [ ] Tool usage analytics dashboard
- [ ] Export to Excel/PDF
- [ ] Print tool labels
- [ ] Notification system for overdue tools
- [ ] Employee autocomplete
- [ ] Work order integration
- [ ] Tool maintenance scheduler

### UI Improvements
- [ ] Drag-and-drop tool assignment
- [ ] Calendar view for checkouts
- [ ] Photo upload for damage reports
- [ ] Tool availability calendar
- [ ] Real-time updates with WebSockets
- [ ] Dark mode support
- [ ] Accessibility improvements (WCAG 2.1)

---

## Troubleshooting

### Issue: Tools not loading
**Solution**: 
1. Check API URL in `.env.local`
2. Verify backend is running
3. Check browser console for errors
4. Verify authentication token exists

### Issue: Checkout fails
**Solution**:
1. Verify employee ID is valid GUID
2. Check tool is actually available
3. Check API logs for errors
4. Verify all required fields filled

### Issue: Search not working
**Solution**:
1. Check search term format
2. Verify tools array has data
3. Clear browser cache
4. Refresh page

### Issue: Styling broken
**Solution**:
1. Run `npm install` to ensure dependencies
2. Verify Tailwind CSS configuration
3. Check shadcn/ui components installed
4. Clear Next.js cache: `rm -rf .next`

---

## Support

For issues or questions:
1. Check backend API logs
2. Check browser console errors
3. Verify API endpoint responses
4. Review this documentation
5. Check `/docs/TOOL_CHECKOUT_API_GUIDE.md` for API details

---

**Last Updated**: November 4, 2025  
**Version**: 1.0.0  
**Status**: Production Ready ✅
