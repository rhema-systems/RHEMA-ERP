import { apiService } from './api.service';

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangePasswordResponse {
  success: boolean;
  message: string;
}

export interface UpdateProfileRequest {
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
}

export interface UpdateProfileResponse {
  success: boolean;
  message: string;
  user?: any; // Updated user data
}

export interface PasswordPolicy {
  minLength: number;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireDigits: boolean;
  requireSpecialChars: boolean;
  maxAge?: number;
  preventReuse?: number;
}

export class ProfileService {
  async changePassword(request: ChangePasswordRequest): Promise<ChangePasswordResponse> {
    try {
      // Basic validation
      if (request.newPassword.length < 8) {
        throw new Error('New password must be at least 8 characters long');
      }
      
      if (!request.currentPassword) {
        throw new Error('Current password is required');
      }
      
      const response = await apiService.silentRequest<any>('/User/change-password', {
        method: 'POST',
        body: JSON.stringify({
          currentPassword: request.currentPassword,
          newPassword: request.newPassword
        })
      });
      
      return {
        success: true,
        message: response.message || 'Password changed successfully'
      };
    } catch (error: any) {
      // Handle specific HTTP status codes with user-friendly messages
      if (error.status === 400) {
        // The backend returns "Current password is incorrect" for wrong passwords
        // Check if the error message contains this or similar patterns
        const errorMessage = error.message || '';
        
        // Check if this is the specific "Current password is incorrect" message from backend
        if (errorMessage === 'Current password is incorrect' ||
            errorMessage.toLowerCase().includes('current password is incorrect') ||
            errorMessage.toLowerCase().includes('current password') ||
            errorMessage.toLowerCase().includes('incorrect password') ||
            errorMessage.toLowerCase().includes('invalid password') ||
            errorMessage.toLowerCase().includes('wrong password')) {
          throw new Error('The current password you entered is incorrect. Please try again.');
        }
        
        // For other password-related 400 errors (policy violations, etc.)
        if (errorMessage.toLowerCase().includes('password')) {
          // If it's a policy error, show the specific message from backend
          if (errorMessage.toLowerCase().includes('policy') || 
              errorMessage.toLowerCase().includes('requirement') ||
              errorMessage.toLowerCase().includes('must contain')) {
            throw new Error(errorMessage);
          }
          // Otherwise assume it's current password issue
          throw new Error('The current password you entered is incorrect. Please try again.');
        }
        
        // Other 400 errors (validation, etc.)
        throw new Error(errorMessage || 'Invalid password change request. Please check your input.');
      }
      
      if (error.status === 401) {
        throw new Error('You are not authorized to change this password. Please log in again.');
      }
      
      if (error.status === 403) {
        throw new Error('You do not have permission to change this password.');
      }
      
      if (error.status === 429) {
        throw new Error('Too many password change attempts. Please try again later.');
      }
      
      if (error.status >= 500) {
        throw new Error('Server error occurred while changing password. Please try again later.');
      }
      
      // Default fallback for any other errors
      throw new Error(error.response?.message || error.message || 'Failed to change password. Please try again.');
    }
  }
  
  async updateProfile(request: UpdateProfileRequest): Promise<UpdateProfileResponse> {
    try {
      const response = await apiService.request<any>('/User/profile', {
        method: 'PUT',
        body: JSON.stringify({
          firstName: request.firstName,
          lastName: request.lastName,
          phoneNumber: request.phoneNumber
        })
      });
      
      // Update localStorage with the updated user data
      if (response && typeof window !== 'undefined') {
        const userInfo = localStorage.getItem('userInfo');
        if (userInfo) {
          const user = JSON.parse(userInfo);
          const updatedUser = {
            ...user,
            firstName: response.firstName ?? request.firstName,
            lastName: response.lastName ?? request.lastName,
            phoneNumber: response.phoneNumber ?? request.phoneNumber,
            updatedAt: new Date().toISOString()
          };
          localStorage.setItem('userInfo', JSON.stringify(updatedUser));
        }
      }
      
      return {
        success: true,
        message: 'Profile updated successfully',
        user: response
      };
    } catch (error: any) {
      // Handle specific HTTP status codes with user-friendly messages
      if (error.status === 400) {
        throw new Error(error.message || 'Invalid profile data. Please check your input.');
      }
      
      if (error.status === 401) {
        throw new Error('You are not authorized to update this profile. Please log in again.');
      }
      
      if (error.status === 403) {
        throw new Error('You do not have permission to update this profile.');
      }
      
      if (error.status >= 500) {
        throw new Error('Server error occurred while updating profile. Please try again later.');
      }
      
      throw new Error(error.message || 'Failed to update profile. Please try again.');
    }
  }
  
  async getPasswordPolicy(): Promise<PasswordPolicy> {
    try {
      const response = await apiService.get<PasswordPolicy>('/api/auth/password-policy');
      return response;
    } catch (error: any) {
      // Return default policy if API fails
      return {
        minLength: 8,
        requireUppercase: true,
        requireLowercase: true,
        requireDigits: true,
        requireSpecialChars: true,
        maxAge: 90,
        preventReuse: 5
      };
    }
  }
  
  async getUserSessions(): Promise<any[]> {
    try {
      // TODO: Replace with actual API endpoint
      await new Promise(resolve => setTimeout(resolve, 500));
      
      // Mock session data
      return [
        {
          id: '1',
          deviceInfo: 'Chrome on Windows',
          ipAddress: '192.168.1.100',
          location: 'Local Network',
          lastActivity: new Date().toISOString(),
          isCurrentSession: true
        }
      ];
    } catch (error: any) {
      throw new Error('Failed to load sessions');
    }
  }
}

export const profileService = new ProfileService();
export default profileService;