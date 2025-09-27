import type { User } from '../types';

class UserService {
  async searchUsers(query: string): Promise<User[]> {
    // Mock implementation - return some sample users
    const mockUsers = [
      {
        id: '1',
        username: 'admin',
        email: 'admin@example.com',
        firstName: 'System',
        lastName: 'Administrator',
        roles: ['SuperAdmin'],
        isActive: true,
        createdAt: new Date().toISOString(),
      },
      {
        id: '2',
        username: 'user1',
        email: 'user1@example.com',
        firstName: 'John',
        lastName: 'Doe',
        roles: ['User'],
        isActive: true,
        createdAt: new Date().toISOString(),
      },
    ];
    
    // Filter based on query
    if (!query) return mockUsers;
    const lowerQuery = query.toLowerCase();
    return mockUsers.filter(user => 
      user.username.toLowerCase().includes(lowerQuery) ||
      user.email.toLowerCase().includes(lowerQuery) ||
      (user.firstName + ' ' + user.lastName).toLowerCase().includes(lowerQuery)
    );
  }
}

export const userService = new UserService();
export default userService;