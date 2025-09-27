import { apiService } from './api.service';

export interface FileUploadResult {
  success: boolean;
  fileName: string;
  originalFileName: string;
  filePath: string;
  publicUrl: string;
  fileSize: number;
  contentType: string;
  category: string;
  tenantId?: string;
  uploadedAt: string;
}

export interface MultipleFileUploadResult {
  successfulUploads: FileUploadResult[];
  errors: string[];
  totalFiles: number;
  successfulCount: number;
  failedCount: number;
}

class FileUploadService {
  /**
   * Upload a single file to the backend
   */
  async uploadSingleFile(
    file: File,
    category: string = 'general',
    tenantId?: string
  ): Promise<FileUploadResult> {
    console.log('Uploading file:', file.name, 'category:', category, 'tenantId:', tenantId);
    
    const formData = new FormData();
    formData.append('file', file);
    formData.append('category', category);
    
    if (tenantId) {
      formData.append('tenantId', tenantId);
    }

    try {
      const result = await apiService.request<FileUploadResult>('/fileupload/single', {
        method: 'POST',
        body: formData,
        // Don't set Content-Type header for FormData - browser will set it with boundary
      });

      console.log('File uploaded successfully:', result.publicUrl);
      return result;
    } catch (error) {
      console.error('Failed to upload file:', file.name, error);
      throw error;
    }
  }

  /**
   * Upload multiple files to the backend
   */
  async uploadMultipleFiles(
    files: File[],
    category: string = 'general',
    tenantId?: string
  ): Promise<MultipleFileUploadResult> {
    console.log('Uploading multiple files:', files.length, 'files, category:', category);
    
    const formData = new FormData();
    
    files.forEach((file, index) => {
      formData.append('files', file);
    });
    
    formData.append('category', category);
    
    if (tenantId) {
      formData.append('tenantId', tenantId);
    }

    try {
      const result = await apiService.request<MultipleFileUploadResult>('/fileupload/multiple', {
        method: 'POST',
        body: formData,
      });

      console.log('Multiple files uploaded:', result.successfulCount, 'successful,', result.failedCount, 'failed');
      return result;
    } catch (error) {
      console.error('Failed to upload multiple files:', error);
      throw error;
    }
  }

  /**
   * Delete an uploaded file
   */
  async deleteFile(filePath: string): Promise<void> {
    console.log('Deleting file:', filePath);
    
    try {
      await apiService.request(`/fileupload?filePath=${encodeURIComponent(filePath)}`, {
        method: 'DELETE',
      });

      console.log('File deleted successfully:', filePath);
    } catch (error) {
      console.error('Failed to delete file:', filePath, error);
      throw error;
    }
  }

  /**
   * Upload tenant branding image (logo, favicon, cover)
   */
  async uploadTenantBrandingImage(
    file: File,
    imageType: 'logo' | 'favicon' | 'cover',
    tenantId?: string
  ): Promise<FileUploadResult> {
    return this.uploadSingleFile(file, `tenant-branding-${imageType}`, tenantId);
  }

  /**
   * Validate file before upload
   */
  validateFile(file: File, maxSizeMB: number = 10): { valid: boolean; error?: string } {
    // Check file size
    const maxSizeBytes = maxSizeMB * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      return {
        valid: false,
        error: `File size (${Math.round(file.size / 1024 / 1024 * 100) / 100}MB) exceeds maximum allowed size of ${maxSizeMB}MB`
      };
    }

    // Check file type for images
    const allowedImageTypes = [
      'image/jpeg',
      'image/png', 
      'image/gif',
      'image/bmp',
      'image/svg+xml',
      'image/webp',
      'image/x-icon',
      'image/vnd.microsoft.icon'
    ];

    if (file.type.startsWith('image/') && !allowedImageTypes.includes(file.type)) {
      return {
        valid: false,
        error: `Image type '${file.type}' is not allowed`
      };
    }

    // Check file extension
    const allowedExtensions = [
      '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.svg', '.webp', '.ico',
      '.pdf', '.doc', '.docx', '.txt', '.rtf'
    ];

    const extension = '.' + file.name.split('.').pop()?.toLowerCase();
    if (!allowedExtensions.includes(extension)) {
      return {
        valid: false,
        error: `File extension '${extension}' is not allowed`
      };
    }

    return { valid: true };
  }

  /**
   * Get file size in human readable format
   */
  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  /**
   * Check if URL is a valid image URL
   */
  isValidImageUrl(url: string): boolean {
    if (!url) return false;
    
    // Don't allow blob: or file: URLs for backend storage
    if (url.startsWith('blob:') || url.startsWith('file://')) {
      return false;
    }
    
    // Must be HTTP/HTTPS URL
    if (!url.startsWith('http://') && !url.startsWith('https://')) {
      return false;
    }
    
    // Should end with image extension
    const imageExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.svg', '.webp', '.ico'];
    const urlLower = url.toLowerCase();
    
    return imageExtensions.some(ext => urlLower.includes(ext));
  }
}

export const fileUploadService = new FileUploadService();