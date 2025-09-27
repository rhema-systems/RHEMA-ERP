# 📖 API Documentation

## Overview

This document provides comprehensive API documentation for the ERP System.

## Available Documentation Formats

- **[Interactive Swagger UI](api/html/index.html)** - Interactive API explorer
- **[Markdown Documentation](api/markdown/README.md)** - Readable markdown format
- **[OpenAPI Specification](api/swagger.json)** - Raw OpenAPI/Swagger specification

## Key API Endpoints

### Authentication
- `POST /api/auth/login` - User authentication
- `POST /api/auth/refresh` - Token refresh
- `POST /api/auth/logout` - User logout

### User Management
- `GET /api/user` - Get users with pagination
- `POST /api/user` - Create new user
- `PUT /api/user/{id}` - Update user
- `DELETE /api/user/{id}` - Delete user

### Tenant Management
- `GET /api/tenant` - Get tenants
- `POST /api/tenant` - Create tenant
- `PUT /api/tenant/{id}` - Update tenant branding and settings

### File Management
- `POST /api/upload` - Upload files (supports multiple storage providers)
- `GET /api/upload/{id}` - Get file information
- `DELETE /api/upload/{id}` - Delete file

### Settings
- `GET /api/settings` - Get system settings
- `PUT /api/settings` - Update settings
- `GET /api/settings/security` - Get security settings
- `PUT /api/settings/security` - Update security policies

## Storage Provider Configuration

The API supports multiple file storage providers:

- **Local Storage** - File system storage
- **Azure Blob Storage** - Cloud storage with CDN support
- **AWS S3** - Amazon cloud storage

See [Storage Abstraction Guide](STORAGE_ABSTRACTION.md) for configuration details.

## Authentication & Authorization

The API uses JWT Bearer tokens for authentication. Include the token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

## Error Responses

All API endpoints return consistent error responses:

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human readable error message",
    "details": "Additional error details"
  }
}
```

## Rate Limiting

API requests are rate limited to prevent abuse:
- 100 requests per minute per IP address
- 1000 requests per hour per authenticated user

---

*Last updated: $(date)*
*Generated from OpenAPI specification*
