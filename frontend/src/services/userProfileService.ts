import { apiService } from './api.service';

export interface UpdateProfileRequest {
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
}

export interface UserProfileDto {
  id: string;
  username: string;
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
  isActive: boolean;
  roles: string[];
  createdAt?: string | null;
  lastLoginAt?: string | null;
  tenantId?: string | null;
}

export const userProfileService = {
  async updateProfile(request: UpdateProfileRequest): Promise<UserProfileDto> {
    return apiService.request<UserProfileDto>('/user/profile', {
      method: 'PUT',
      body: JSON.stringify({
        firstName: request.firstName ?? null,
        lastName: request.lastName ?? null,
        email: request.email ?? null,
        phoneNumber: request.phoneNumber ?? null,
      }),
    });
  },
};

export default userProfileService;

