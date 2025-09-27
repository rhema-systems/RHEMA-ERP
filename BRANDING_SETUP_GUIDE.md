# Tenant Branding Setup Guide

## How to Add Branding Images to Tenants

The tenant management system supports custom branding through logos, favicons, and cover images. Here's how to set them up:

### Option 1: Using Direct Image URLs (Recommended)

1. **Upload images to your web server or cloud storage** (e.g., AWS S3, Google Cloud Storage, Azure Blob Storage)
2. **Get the direct public URL** to the image (e.g., `https://yourdomain.com/images/logo.png`)
3. **Paste the URL into the text input field** below each image type
4. **Save the tenant** - the URL will be stored and the image will be accessible

**Example URLs:**
- Logo: `https://cdn.yourdomain.com/tenants/acme/logo.png`
- Favicon: `https://cdn.yourdomain.com/tenants/acme/favicon.ico` 
- Cover: `https://cdn.yourdomain.com/tenants/acme/cover.jpg`

### Option 2: Local File Selection (For Testing Only)

1. **Click the "Upload" button** next to each image type
2. **Select a local image file** from your computer
3. **The file name will appear** in the form (showing as `file://filename`)
4. **Note**: This is for testing/preview only - files are not uploaded to permanent storage

### Current Limitations

⚠️ **File Upload to Storage Not Yet Implemented**
- Local file uploads are for preview purposes only
- Files selected via "Upload" button are not permanently stored
- For production use, you must use direct image URLs (Option 1)

### Recommended Image Specifications

| Image Type | Dimensions | Format | Notes |
|------------|------------|---------|-------|
| **Logo** | 200x60px | PNG/JPG | Transparent background recommended |
| **Favicon** | 32x32px | ICO/PNG | Square aspect ratio |  
| **Cover Image** | 1920x1080px | JPG/PNG | High quality, landscape orientation |

### Implementation Notes

The tenant branding system properly handles:
- ✅ Direct HTTP/HTTPS image URLs
- ✅ Null/empty values (no image)
- ✅ Image preview and validation
- ❌ Blob URLs (temporary browser URLs)
- ❌ File:// URLs (local file references)

### Future Improvements

Planned enhancements include:
- File upload to cloud storage (AWS S3, Azure Blob, etc.)
- Image resizing and optimization
- Drag-and-drop image upload
- Image cropping and editing tools
- CDN integration for faster loading

### Troubleshooting

**Images not showing after save:**
- Ensure URLs are publicly accessible
- Check that URLs start with `http://` or `https://`
- Verify images aren't behind authentication
- Test URLs directly in browser

**File uploads not persisting:**
- This is expected behavior - use direct URLs instead
- Upload files to your web server/cloud storage first
- Then paste the permanent URL into the form