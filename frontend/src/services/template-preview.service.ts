import { emailTemplateService, type DatabaseColumn } from './email-template.service';

export interface PreviewData {
  html: string;
  plainText: string;
  variables: Record<string, any>;
}

export interface TemplateVariable {
  placeholder: string;
  value: string;
  dataType: string;
  description: string;
}

class TemplatePreviewService {
  private sampleDataCache: Map<string, Record<string, any>> = new Map();

  /**
   * Generate preview data for a template with realistic sample values
   */
  async generatePreview(
    htmlContent: string,
    plainTextContent: string,
    tableName?: string,
    selectedFields?: string[]
  ): Promise<PreviewData> {
    // Extract all placeholders from the content
    const placeholders = this.extractPlaceholders(htmlContent + ' ' + plainTextContent);
    
    // Generate sample data for each placeholder
    const variables: Record<string, any> = {};
    
    for (const placeholder of placeholders) {
      const value = await this.generateSampleValue(placeholder, tableName);
      variables[placeholder] = value;
    }

    // Replace placeholders in content
    const processedHtml = this.replacePlaceholders(htmlContent, variables);
    const processedPlainText = this.replacePlaceholders(plainTextContent, variables);

    return {
      html: processedHtml,
      plainText: processedPlainText,
      variables
    };
  }

  /**
   * Generate template variables with descriptions for editing
   */
  async generateTemplateVariables(
    content: string,
    tableName?: string
  ): Promise<TemplateVariable[]> {
    const placeholders = this.extractPlaceholders(content);
    const variables: TemplateVariable[] = [];

    // Get column information if table is specified
    let columns: DatabaseColumn[] = [];
    if (tableName) {
      try {
        columns = await emailTemplateService.getColumns(tableName);
      } catch (error) {
        console.warn('Failed to fetch column info:', error);
      }
    }

    for (const placeholder of placeholders) {
      const { table, field } = this.parsePlaceholder(placeholder);
      const column = columns.find(c => c.name.toLowerCase() === field.toLowerCase());
      
      variables.push({
        placeholder,
        value: await this.generateSampleValue(placeholder, tableName),
        dataType: column?.dataType || 'string',
        description: this.generateFieldDescription(table, field, column?.dataType)
      });
    }

    return variables;
  }

  /**
   * Extract all placeholders from content (e.g., {{Users.FirstName}})
   */
  private extractPlaceholders(content: string): string[] {
    const regex = /\{\{([^}]+)\}\}/g;
    const placeholders = new Set<string>();
    let match;

    while ((match = regex.exec(content)) !== null) {
      placeholders.add(match[0]); // Include the full {{}} syntax
    }

    return Array.from(placeholders);
  }

  /**
   * Parse placeholder to extract table and field names
   */
  private parsePlaceholder(placeholder: string): { table: string; field: string } {
    const cleaned = placeholder.replace(/[{}]/g, '');
    const parts = cleaned.split('.');
    
    if (parts.length >= 2) {
      return {
        table: parts[0],
        field: parts.slice(1).join('.') // Handle nested properties
      };
    }

    return {
      table: 'Unknown',
      field: cleaned
    };
  }

  /**
   * Replace placeholders in content with actual values
   */
  private replacePlaceholders(content: string, variables: Record<string, any>): string {
    let result = content;
    
    Object.entries(variables).forEach(([placeholder, value]) => {
      const regex = new RegExp(placeholder.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g');
      result = result.replace(regex, String(value));
    });

    return result;
  }

  /**
   * Generate realistic sample values based on field name and type
   */
  private async generateSampleValue(placeholder: string, tableName?: string): Promise<string> {
    const { table, field } = this.parsePlaceholder(placeholder);
    const lowerField = field.toLowerCase();

    // Use cached data if available
    const cacheKey = `${table}_${field}`;
    if (this.sampleDataCache.has(cacheKey)) {
      return this.sampleDataCache.get(cacheKey) as string;
    }

    let value: string;

    // Generate based on field name patterns
    if (lowerField.includes('firstname') || lowerField.includes('first_name')) {
      value = this.getRandomFirstName();
    } else if (lowerField.includes('lastname') || lowerField.includes('last_name')) {
      value = this.getRandomLastName();
    } else if (lowerField.includes('email')) {
      value = this.getRandomEmail();
    } else if (lowerField.includes('phone')) {
      value = this.getRandomPhone();
    } else if (lowerField.includes('address')) {
      value = this.getRandomAddress();
    } else if (lowerField.includes('city')) {
      value = this.getRandomCity();
    } else if (lowerField.includes('company') || lowerField.includes('organization')) {
      value = this.getRandomCompany();
    } else if (lowerField.includes('amount') || lowerField.includes('price') || lowerField.includes('cost')) {
      value = this.getRandomAmount();
    } else if (lowerField.includes('date')) {
      value = this.getRandomDate();
    } else if (lowerField.includes('id') || lowerField.includes('number')) {
      value = this.getRandomId();
    } else if (lowerField.includes('status')) {
      value = this.getRandomStatus();
    } else if (lowerField.includes('title') || lowerField.includes('position')) {
      value = this.getRandomJobTitle();
    } else {
      value = `Sample ${field}`;
    }

    // Cache the value
    this.sampleDataCache.set(cacheKey, value);
    return value;
  }

  /**
   * Generate human-readable description for template variables
   */
  private generateFieldDescription(table: string, field: string, dataType?: string): string {
    const tableFormatted = table.charAt(0).toUpperCase() + table.slice(1);
    const fieldFormatted = field.replace(/([A-Z])/g, ' $1').trim();
    const typeInfo = dataType ? ` (${dataType})` : '';
    
    return `${tableFormatted} ${fieldFormatted}${typeInfo}`;
  }

  // Sample data generators
  private getRandomFirstName(): string {
    const names = ['John', 'Jane', 'Michael', 'Sarah', 'David', 'Emily', 'Chris', 'Lisa', 'Robert', 'Anna'];
    return names[Math.floor(Math.random() * names.length)];
  }

  private getRandomLastName(): string {
    const names = ['Smith', 'Johnson', 'Williams', 'Brown', 'Jones', 'Garcia', 'Miller', 'Davis', 'Wilson', 'Moore'];
    return names[Math.floor(Math.random() * names.length)];
  }

  private getRandomEmail(): string {
    const domains = ['example.com', 'sample.org', 'demo.net', 'test.com'];
    const firstNames = ['john', 'jane', 'mike', 'sarah', 'david'];
    const lastNames = ['doe', 'smith', 'wilson', 'brown', 'johnson'];
    
    const firstName = firstNames[Math.floor(Math.random() * firstNames.length)];
    const lastName = lastNames[Math.floor(Math.random() * lastNames.length)];
    const domain = domains[Math.floor(Math.random() * domains.length)];
    
    return `${firstName}.${lastName}@${domain}`;
  }

  private getRandomPhone(): string {
    const formats = [
      '+1 (555) 123-4567',
      '(555) 987-6543',
      '+44 20 7123 4567',
      '555-123-4567'
    ];
    return formats[Math.floor(Math.random() * formats.length)];
  }

  private getRandomAddress(): string {
    const streets = ['Main St', 'Oak Ave', 'Pine Rd', 'Elm Way', 'Maple Dr'];
    const numbers = Math.floor(Math.random() * 9999) + 1;
    const street = streets[Math.floor(Math.random() * streets.length)];
    return `${numbers} ${street}`;
  }

  private getRandomCity(): string {
    const cities = ['New York', 'Los Angeles', 'Chicago', 'Houston', 'Phoenix', 'Philadelphia'];
    return cities[Math.floor(Math.random() * cities.length)];
  }

  private getRandomCompany(): string {
    const companies = ['Acme Corp', 'Tech Solutions Inc', 'Global Industries', 'Innovation Labs', 'Future Systems'];
    return companies[Math.floor(Math.random() * companies.length)];
  }

  private getRandomAmount(): string {
    const amount = (Math.random() * 10000).toFixed(2);
    return `$${amount}`;
  }

  private getRandomDate(): string {
    const date = new Date();
    date.setDate(date.getDate() + Math.floor(Math.random() * 365) - 182); // ±6 months
    return date.toLocaleDateString();
  }

  private getRandomId(): string {
    return Math.floor(Math.random() * 1000000).toString().padStart(6, '0');
  }

  private getRandomStatus(): string {
    const statuses = ['Active', 'Pending', 'Completed', 'In Progress', 'Approved', 'Canceled'];
    return statuses[Math.floor(Math.random() * statuses.length)];
  }

  private getRandomJobTitle(): string {
    const titles = ['Manager', 'Developer', 'Analyst', 'Coordinator', 'Specialist', 'Director'];
    return titles[Math.floor(Math.random() * titles.length)];
  }
}

export const templatePreviewService = new TemplatePreviewService();