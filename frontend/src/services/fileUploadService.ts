import { apiService } from './api.service';

export interface UploadedFile {
  id: string;
  filename: string;
  originalName: string;
  mimeType: string;
  size: number;
  url: string;
  filePath?: string;
  publicUrl?: string;
  thumbnailUrl?: string;
  uploadedAt: string;
  uploadedBy: string;
}

export interface FileUploadProgress {
  filename: string;
  progress: number;
  status: 'uploading' | 'completed' | 'error';
  error?: string;
}

// Mock uploaded files for fallback
const mockUploadedFiles: UploadedFile[] = [];

class FileUploadService {
  private useBackend = true; // Set to false to force mock data

  async uploadFile(file: File, entityType: string, entityId: string, onProgress?: (progress: number) => void): Promise<UploadedFile> {
    try {
      if (this.useBackend) {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('category', entityType);

        // Use the correct endpoint /api/fileupload/single
        const response = await apiService.request<{
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
        }>('/fileupload/single', {
          method: 'POST',
          body: formData,
        });

        // Map the response to UploadedFile format
        // Prepend the backend base URL if the publicUrl is a relative path starting with /uploads
        const backendBaseUrl = (process.env.NEXT_PUBLIC_API_URL || '/api').replace('/api', '');
        const fullUrl = response.publicUrl.startsWith('/uploads')
          ? `${backendBaseUrl}${response.publicUrl}`
          : response.publicUrl;

        return {
          id: response.fileName,
          filename: response.fileName,
          originalName: response.originalFileName,
          mimeType: response.contentType,
          size: response.fileSize,
          url: fullUrl,
          filePath: response.filePath,
          publicUrl: response.publicUrl,
          thumbnailUrl: response.contentType?.startsWith('image/') ? fullUrl : undefined,
          uploadedAt: response.uploadedAt,
          uploadedBy: 'Current User'
        };
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock upload:', error);
    }

    // Mock upload for fallback
    return this.mockUpload(file, entityType, entityId, onProgress);
  }

  async uploadMultipleFiles(
    files: File[], 
    entityType: string, 
    entityId: string, 
    onProgress?: (filename: string, progress: number) => void
  ): Promise<UploadedFile[]> {
    const uploadPromises = files.map(async (file) => {
      return this.uploadFile(file, entityType, entityId, (progress) => {
        if (onProgress) {
          onProgress(file.name, progress);
        }
      });
    });

    return Promise.all(uploadPromises);
  }

  async deleteFile(fileId: string): Promise<void> {
    try {
      if (this.useBackend) {
        await apiService.request<void>(`/files/${fileId}`, {
          method: 'DELETE',
        });
        return;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock delete:', error);
    }

    // Mock delete for fallback
    const index = mockUploadedFiles.findIndex(f => f.id === fileId);
    if (index > -1) {
      mockUploadedFiles.splice(index, 1);
    }
  }

  async getFilesByEntity(entityType: string, entityId: string): Promise<UploadedFile[]> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<UploadedFile[]>(`/files/entity/${entityType}/${entityId}`);
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }

    // Mock data for fallback
    return mockUploadedFiles.filter(f => 
      f.url.includes(entityType) && f.url.includes(entityId)
    );
  }

  async downloadFile(fileId: string): Promise<Blob> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<Blob>(`/files/${fileId}/download`);
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, cannot download file:', error);
    }

    throw new Error('File download not available in mock mode');
  }

  private async mockUpload(
    file: File, 
    entityType: string, 
    entityId: string, 
    onProgress?: (progress: number) => void
  ): Promise<UploadedFile> {
    // Simulate upload progress
    return new Promise((resolve) => {
      let progress = 0;
      const interval = setInterval(() => {
        progress += 10;
        if (onProgress) {
          onProgress(progress);
        }
        
        if (progress >= 100) {
          clearInterval(interval);
          
          const mockFile: UploadedFile = {
            id: `mock-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
            filename: `${Date.now()}_${file.name}`,
            originalName: file.name,
            mimeType: file.type,
            size: file.size,
            url: URL.createObjectURL(file), // For preview in mock mode
            thumbnailUrl: file.type.startsWith('image/') ? URL.createObjectURL(file) : undefined,
            uploadedAt: new Date().toISOString(),
            uploadedBy: 'Current User'
          };
          
          mockUploadedFiles.push(mockFile);
          resolve(mockFile);
        }
      }, 100);
    });
  }

  isImageFile(file: File): boolean {
    return file.type.startsWith('image/');
  }

  isVideoFile(file: File): boolean {
    return file.type.startsWith('video/');
  }

  isPdfFile(file: File): boolean {
    return file.type === 'application/pdf';
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  validateFile(file: File, maxSizeBytes: number = 10 * 1024 * 1024, allowedTypes?: string[]): { isValid: boolean; error?: string } {
    if (file.size > maxSizeBytes) {
      return {
        isValid: false,
        error: `File size exceeds maximum allowed size of ${this.formatFileSize(maxSizeBytes)}`
      };
    }

    if (allowedTypes && !allowedTypes.includes(file.type)) {
      return {
        isValid: false,
        error: `File type ${file.type} is not allowed`
      };
    }

    return { isValid: true };
  }

  async createThumbnail(file: File, maxWidth: number = 200, maxHeight: number = 200): Promise<string> {
    if (!this.isImageFile(file)) {
      throw new Error('Thumbnails can only be created for image files');
    }

    return new Promise((resolve, reject) => {
      const canvas = document.createElement('canvas');
      const ctx = canvas.getContext('2d');
      const img = new Image();

      img.onload = () => {
        // Calculate scaled dimensions
        let { width, height } = img;
        if (width > height) {
          if (width > maxWidth) {
            height = (height * maxWidth) / width;
            width = maxWidth;
          }
        } else {
          if (height > maxHeight) {
            width = (width * maxHeight) / height;
            height = maxHeight;
          }
        }

        canvas.width = width;
        canvas.height = height;

        // Draw scaled image
        ctx?.drawImage(img, 0, 0, width, height);

        // Convert to data URL
        const dataUrl = canvas.toDataURL('image/jpeg', 0.8);
        resolve(dataUrl);
      };

      img.onerror = () => {
        reject(new Error('Failed to load image for thumbnail creation'));
      };

      img.src = URL.createObjectURL(file);
    });
  }

  getFileIcon(mimeType: string): string {
    if (mimeType.startsWith('image/')) return '📷';
    if (mimeType.startsWith('video/')) return '🎥';
    if (mimeType === 'application/pdf') return '📄';
    if (mimeType.includes('document') || mimeType.includes('word')) return '📝';
    if (mimeType.includes('spreadsheet') || mimeType.includes('excel')) return '📊';
    if (mimeType.includes('presentation') || mimeType.includes('powerpoint')) return '📽️';
    if (mimeType.startsWith('audio/')) return '🎵';
    if (mimeType.includes('zip') || mimeType.includes('compressed')) return '🗜️';
    return '📎';
  }
}

export const fileUploadService = new FileUploadService();
