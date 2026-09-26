import type { User } from '../types';
import { apiService } from './api.service';
import { workflowApiService } from './workflow-api.service';

class UserService {
  async searchAssignableUsers(query: string): Promise<User[]> {
    const users = await workflowApiService.getWorkflowDirectoryUsers(query);
    return users.map((user) => ({
      id: user.id,
      username: user.userName || '',
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email || '',
      isActive: true,
      roles: [],
    }));
  }

  async searchUsers(query: string): Promise<User[]> {
    try {
      // Call the real API endpoint
      const users = await apiService.request<User[]>(`/user/search?query=${encodeURIComponent(query)}`);
      return users;
    } catch (error) {
      console.error('Error searching users:', error);
      throw error;
    }
  }
}

export const userService = new UserService();
export default userService;
