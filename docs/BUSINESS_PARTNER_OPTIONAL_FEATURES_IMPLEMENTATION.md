# Business Partner Registration - Optional Features Implementation Plan

**Date:** 2025-11-26  
**Status:** Ready to Implement  
**Leveraging:** Existing system infrastructure

---

## Overview

The ERP system already has robust implementations for:
1. ✅ **Email Service** - Production-ready SMTP with templates
2. ✅ **File Upload Validation** - Comprehensive file validation and storage
3. ✅ **Export to Excel** - Multiple export formats (CSV, Excel, JSON)

We can leverage these existing implementations to add optional features to the Business Partner Registration system.

---

## 1. Email Notifications ✅ EXISTING INFRASTRUCTURE

### **Existing Email Service**

**Files:**
- `src/ErpSystem.Api/Services/ProductionEmailService.cs` - Production SMTP service
- `src/ErpSystem.Api/Services/IEmailService.cs` - Email service interface
- `src/ErpSystem.Core/Services/EmailTemplateService.cs` - Template processing
- `src/ErpSystem.Core/Entities/Settings.cs` - EmailSettings entity

**Features Already Available:**
- ✅ SMTP configuration (Office 365, Gmail, etc.)
- ✅ HTML email templates with styling
- ✅ Email template processing with placeholders
- ✅ Password reset emails (reference implementation)
- ✅ Welcome emails (reference implementation)
- ✅ Account locked emails (reference implementation)
- ✅ Bulk email sending
- ✅ Email logging and error handling

**Configuration:**
```json
// Already in database: EmailSettings table
{
  "SmtpHost": "smtp.office365.com",
  "SmtpPort": 587,
  "SmtpUsername": "your-email@company.com",
  "SmtpPassword": "encrypted-password",
  "UseTLS": true,
  "FromAddress": "noreply@company.com",
  "FromName": "ERP System"
}
```

### **Implementation Plan for Business Partner Emails**

#### **Step 1: Add Email Methods to IEmailService**

**File:** `src/ErpSystem.Api/Services/IEmailService.cs`

Add new methods:
```csharp
Task<bool> SendRegistrationSubmittedEmailAsync(string email, string companyName, string applicationNumber);
Task<bool> SendRegistrationApprovedEmailAsync(string email, string companyName, string partnerNumber);
Task<bool> SendRegistrationRejectedEmailAsync(string email, string companyName, string reason);
Task<bool> SendDocumentVerificationRequestEmailAsync(string email, string companyName, string documentType);
Task<bool> SendLicenseExpiryReminderEmailAsync(string email, string companyName, string licenseType, DateTime expiryDate);
```

#### **Step 2: Implement Email Templates in ProductionEmailService**

**File:** `src/ErpSystem.Api/Services/ProductionEmailService.cs`

Add template methods (similar to existing `GeneratePasswordResetEmailBody`):
```csharp
private string GenerateRegistrationSubmittedEmailBody(string companyName, string applicationNumber)
{
    return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Registration Submitted</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f8f9fa; }}
        .footer {{ padding: 20px; text-align: center; color: #6c757d; font-size: 14px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Registration Submitted Successfully</h1>
        </div>
        <div class=""content"">
            <p>Dear {companyName},</p>
            <p>Thank you for submitting your business partner registration application.</p>
            <p><strong>Application Number:</strong> {applicationNumber}</p>
            <p>Your application is now under review. We will notify you once the review is complete.</p>
            <p>You can track your application status in the external portal.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
}
```

#### **Step 3: Integrate Email Sending in BusinessPartnerRegistrationService**

**File:** `src/ErpSystem.Core/Services/Procurement/BusinessPartnerRegistrationService.cs`

Add email service injection and calls:
```csharp
private readonly IEmailService _emailService;

// In SubmitAsync method (after line 120):
await _emailService.SendRegistrationSubmittedEmailAsync(
    registration.ApplicantEmail,
    registration.ApplicantName,
    registration.RegistrationNumber
);

// In ApproveAsync method (after line 180):
await _emailService.SendRegistrationApprovedEmailAsync(
    registration.ApplicantEmail,
    registration.ApplicantName,
    businessPartner.PartnerNumber
);

// In RejectAsync method (after line 220):
await _emailService.SendRegistrationRejectedEmailAsync(
    registration.ApplicantEmail,
    registration.ApplicantName,
    rejectionReason
);
```

**Estimated Time:** 2-3 hours  
**Complexity:** Low (leveraging existing infrastructure)

---

## 2. File Upload Improvements ✅ EXISTING INFRASTRUCTURE

### **Existing File Upload Service**

**Files:**
- `src/ErpSystem.Api/Controllers/FileUploadController.cs` - File upload API
- `frontend/src/services/file-upload.service.ts` - Frontend file validation
- `frontend/src/services/fileUploadService.ts` - File upload service
- `src/ErpSystem.Core/Interfaces/IFileStorageService.cs` - Storage abstraction

**Features Already Available:**
- ✅ File size validation (configurable, default 10MB)
- ✅ File type validation (extensions and MIME types)
- ✅ Allowed file types: Images, PDFs, Word docs, Excel, etc.
- ✅ File storage abstraction (Local, Azure Blob, AWS S3, Google Cloud)
- ✅ Thumbnail generation for images
- ✅ File metadata tracking
- ✅ Secure file paths with GUID naming

**Current Validation:**
```typescript
// Frontend: frontend/src/services/file-upload.service.ts (lines 126-170)
validateFile(file: File, maxSizeMB: number = 10): { valid: boolean; error?: string } {
  // Check file size
  const maxSizeBytes = maxSizeMB * 1024 * 1024;
  if (file.size > maxSizeBytes) {
    return { valid: false, error: `File size exceeds ${maxSizeMB}MB` };
  }

  // Check file type
  const allowedImageTypes = ['image/jpeg', 'image/png', 'image/gif', ...];
  const allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf', '.doc', '.docx', ...];
  
  // Validation logic
}
```

```csharp
// Backend: src/ErpSystem.Api/Controllers/FileUploadController.cs (lines 20-31)
private static readonly Dictionary<string, string[]> AllowedFileTypes = new()
{
    { "image", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico" } },
    { "document", new[] { ".pdf", ".doc", ".docx", ".txt", ".rtf" } }
};

private static readonly string[] AllowedMimeTypes = new[]
{
    "image/jpeg", "image/png", "image/gif", "image/bmp", "image/svg+xml", 
    "application/pdf", "application/msword", ...
};
```

### **Implementation Plan for Business Partner Documents**

#### **Step 1: Update DocumentUpload Component**

**File:** `frontend/src/components/procurement/registration/DocumentUpload.tsx`

**Current validation (line 41-46):**
```typescript
if (file.size > 10 * 1024 * 1024) {
  toast.error('File size must be less than 10MB');
  return;
}
```

**Enhanced validation:**
```typescript
import { fileUploadService } from '@/services/file-upload.service';

const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
  const file = e.target.files?.[0];
  if (!file) return;

  // Use existing validation service
  const validation = fileUploadService.validateFile(file, 10); // 10MB limit
  if (!validation.valid) {
    toast.error(validation.error || 'Invalid file');
    return;
  }

  // Additional document-specific validation
  const allowedDocTypes = ['.pdf', '.jpg', '.jpeg', '.png', '.doc', '.docx'];
  const extension = '.' + file.name.split('.').pop()?.toLowerCase();
  if (!allowedDocTypes.includes(extension)) {
    toast.error(`Only ${allowedDocTypes.join(', ')} files are allowed`);
    return;
  }

  setSelectedFile(file);
  toast.success(`File selected: ${file.name} (${fileUploadService.formatFileSize(file.size)})`);
};
```

#### **Step 2: Add Download Tracking**

**File:** `src/ErpSystem.Core/Services/Procurement/BusinessPartnerRegistrationService.cs`

Add download tracking method:
```csharp
public async Task TrackDocumentDownloadAsync(Guid documentId, Guid userId)
{
    var document = await _registrationDocumentRepository.GetByIdAsync(documentId);
    if (document != null)
    {
        // Log download event
        _logger.LogInformation(
            "Document {DocumentId} downloaded by user {UserId} at {Timestamp}",
            documentId, userId, DateTime.UtcNow
        );
        
        // Could add download count to entity if needed
    }
}
```

**Estimated Time:** 1-2 hours  
**Complexity:** Very Low (already implemented, just need to use it)

---

## 3. Export to Excel ✅ EXISTING INFRASTRUCTURE

### **Existing Export Service**

**Files:**
- `frontend/src/components/ui/DataTable/DataTableExport.tsx` - Reusable export component
- `frontend/src/components/admin/data-table.tsx` - CSV export implementation
- `src/ErpSystem.Data/Services/DatabaseReportsService.cs` - Backend export service

**Features Already Available:**
- ✅ Export to CSV
- ✅ Export to Excel (XLSX)
- ✅ Export to JSON
- ✅ Column selection
- ✅ Filtered data export
- ✅ Auto-adjust column widths
- ✅ File download with proper naming

**Dependencies:**
```json
// frontend/package.json
{
  "xlsx": "^0.18.5",        // Excel generation
  "file-saver": "^2.0.5"    // File download
}
```

### **Implementation Plan for Business Partner Export**

#### **Step 1: Add Export to Business Partners Page**

**File:** `frontend/src/app/procurement/business-partners/page.tsx`

Import export component:
```typescript
import { DataTableExport } from '@/components/ui/DataTable/DataTableExport';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';
```

Add export function:
```typescript
const exportToExcel = () => {
  const exportData = partners.map(partner => ({
    'Partner Number': partner.partnerNumber,
    'Company Name': partner.companyName,
    'Partner Type': partner.partnerType,
    'Status': partner.status,
    'Email': partner.email,
    'Phone': partner.phone,
    'City': partner.city,
    'Country': partner.country,
    'Registration Date': new Date(partner.createdAt).toLocaleDateString(),
  }));

  const ws = XLSX.utils.json_to_sheet(exportData);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Business Partners');

  // Auto-adjust column widths
  const colWidths = Object.keys(exportData[0] || {}).map(key => ({ wch: Math.max(key.length, 15) }));
  ws['!cols'] = colWidths;

  XLSX.writeFile(wb, `business_partners_${new Date().toISOString().split('T')[0]}.xlsx`);
  toast.success('Business partners exported successfully');
};
```

Add export button to UI (after filters):
```typescript
<Button onClick={exportToExcel} variant="outline">
  <Download className="mr-2 h-4 w-4" />
  Export to Excel
</Button>
```

#### **Step 2: Add Export to Registrations Page**

**File:** `frontend/src/app/administration/procurement/registrations/page.tsx`

Similar implementation for registration exports.

**Estimated Time:** 1 hour  
**Complexity:** Very Low (copy existing pattern)

---

## Summary

| Feature | Status | Existing Code | Effort | Priority |
|---------|--------|---------------|--------|----------|
| **Email Notifications** | ✅ Infrastructure Ready | ProductionEmailService, EmailTemplateService | 2-3 hours | HIGH |
| **File Upload Validation** | ✅ Already Implemented | FileUploadController, file-upload.service.ts | 1-2 hours | LOW |
| **Export to Excel** | ✅ Already Implemented | DataTableExport, XLSX library | 1 hour | MEDIUM |

**Total Estimated Time:** 4-6 hours  
**All features leverage existing, production-ready infrastructure!**

---

## Next Steps

1. **Email Notifications** (Priority 1)
   - Add email methods to IEmailService interface
   - Implement email templates in ProductionEmailService
   - Integrate email calls in BusinessPartnerRegistrationService
   - Test email sending with configured SMTP

2. **Export to Excel** (Priority 2)
   - Add export button to business partners page
   - Add export button to registrations page
   - Test export functionality

3. **File Upload Enhancements** (Priority 3)
   - Update DocumentUpload component to use existing validation service
   - Add download tracking (optional)
   - Test file validation

---

**All features can be implemented quickly by leveraging existing infrastructure!** 🚀

