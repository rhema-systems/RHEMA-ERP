import type { User } from '../types';
import { apiService } from './api.service';

class UserService {
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