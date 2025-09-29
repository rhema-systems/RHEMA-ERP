import { API_CONFIG } from '../config/api';

// Use the configured base URL directly (should already include /api)
const BASE_URL = API_CONFIG.BASE_URL;

// Types
export interface EmailTemplate {
  id?: string;
  name: string;
  module: string;
  tableName?: string;
  subject: string;
  htmlBody: string;
  plainTextBody?: string;
  selectedFields?: string;
  description?: string;
  category?: string;
  templateVariables?: string;
  createdAt?: string;
  modifiedAt?: string;
}

export interface DatabaseTable {
  name: string;
  schema: string;
  description?: string;
}

export interface DatabaseColumn {
  name: string;
  dataType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  description?: string;
}

class EmailTemplateService {
  private async fetch(url: string, options?: RequestInit): Promise<Response> {
    const token = typeof window !== 'undefined' ? localStorage.getItem('authToken') : null;
    
    const defaultOptions: RequestInit = {
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      },
    };

    const response = await fetch(`${BASE_URL}${url}`, {
      ...defaultOptions,
      ...options,
      headers: {
        ...defaultOptions.headers,
        ...options?.headers,
      },
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return response;
  }

  // Get all email templates
  async getTemplates(): Promise<EmailTemplate[]> {
    const response = await this.fetch('/emailtemplate');
    return response.json();
  }

  // Get template by ID
  async getTemplate(id: string): Promise<EmailTemplate> {
    const response = await this.fetch(`/emailtemplate/${id}`);
    return response.json();
  }

  // Create new template
  async createTemplate(template: Omit<EmailTemplate, 'id'>): Promise<EmailTemplate> {
    const response = await this.fetch('/emailtemplate', {
      method: 'POST',
      body: JSON.stringify(template),
    });
    return response.json();
  }

  // Update template
  async updateTemplate(id: string, template: Omit<EmailTemplate, 'id'>): Promise<EmailTemplate> {
    const response = await this.fetch(`/emailtemplate/${id}`, {
      method: 'PUT',
      body: JSON.stringify(template),
    });
    return response.json();
  }

  // Delete template
  async deleteTemplate(id: string): Promise<void> {
    await this.fetch(`/emailtemplate/${id}`, {
      method: 'DELETE',
    });
  }

  // Get available modules
  async getModules(): Promise<string[]> {
    const response = await this.fetch('/emailtemplate/modules');
    return response.json();
  }

  // Get database tables
  async getTables(): Promise<DatabaseTable[]> {
    const response = await this.fetch('/emailtemplate/database/tables');
    return response.json();
  }

  // Get table columns
  async getColumns(tableName: string): Promise<DatabaseColumn[]> {
    const response = await this.fetch(`/emailtemplate/database/tables/${tableName}/columns`);
    return response.json();
  }
}

export const emailTemplateService = new EmailTemplateService();