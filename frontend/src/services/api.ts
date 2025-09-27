import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios';
import { API_CONFIG, HTTP_STATUS } from '../config/api';
import type { ApiResponse } from '../types';

class ApiService {
  private client: AxiosInstance;

  constructor() {
    this.client = axios.create({
      baseURL: API_CONFIG.BASE_URL,
      timeout: API_CONFIG.TIMEOUT,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    this.setupInterceptors();
  }

  private setupInterceptors() {
    // Request interceptor to add auth token
    this.client.interceptors.request.use(
      (config) => {
        if (typeof window !== 'undefined') {
          const token = localStorage.getItem('token');
          if (token) {
            config.headers.Authorization = `Bearer ${token}`;
          }
        }
        return config;
      },
      (error) => {
        return Promise.reject(error);
      }
    );

    // Response interceptor for error handling
    this.client.interceptors.response.use(
      (response) => response,
      async (error) => {
        if (error.response?.status === HTTP_STATUS.UNAUTHORIZED) {
          // Try to refresh token
          const refreshToken = localStorage.getItem('refreshToken');
          if (refreshToken) {
            try {
              const response = await this.refreshToken();
              if (response.token) {
                localStorage.setItem('token', response.token);
                localStorage.setItem('refreshToken', response.refreshToken);
                // Retry the original request
                return this.client(error.config);
              }
            } catch {
              // Refresh failed, redirect to login
              this.handleAuthError();
            }
          } else {
            this.handleAuthError();
          }
        }
        return Promise.reject(error);
      }
    );
  }

  private handleAuthError() {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('token');
      localStorage.removeItem('refreshToken');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
  }

  private async refreshToken() {
    const token = localStorage.getItem('token');
    const refreshToken = localStorage.getItem('refreshToken');
    
    if (!token || !refreshToken) {
      throw new Error('No tokens available');
    }

    const response = await axios.post(`${API_CONFIG.BASE_URL}/api/auth/refresh`, {
      token,
      refreshToken,
    });

    return response.data;
  }

  // HTTP Methods
  async get<T = unknown>(url: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.get<T>(url, config);
    return response.data;
  }

  async post<T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.post<T>(url, data, config);
    return response.data;
  }

  async put<T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.put<T>(url, data, config);
    return response.data;
  }

  async patch<T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.patch<T>(url, data, config);
    return response.data;
  }

  async delete<T = unknown>(url: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.delete<T>(url, config);
    return response.data;
  }

  // Convenience method for handling API responses
  async handleResponse<T>(promise: Promise<AxiosResponse<ApiResponse<T>>>): Promise<T> {
    try {
      const response = await promise;
      if (response.data.success) {
        return response.data.data as T;
      } else {
        throw new Error(response.data.message || 'API request failed');
      }
    } catch (error) {
      if (axios.isAxiosError(error)) {
        const message = error.response?.data?.message || error.message;
        throw new Error(message);
      }
      throw error;
    }
  }
}

export const apiService = new ApiService();
export default apiService;