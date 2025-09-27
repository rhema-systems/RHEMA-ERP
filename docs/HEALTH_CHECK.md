# ERP System Health Check Endpoints

This document describes the health check endpoints available in the ERP System for monitoring and deployment verification.

## API Health Check

**Endpoint:** `GET /health`  
**Purpose:** Verify API server is running and responsive  
**Response:** 
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "version": "1.0.0",
  "environment": "production"
}
```

## Database Health Check

**Endpoint:** `GET /health/database`  
**Purpose:** Verify database connectivity and performance  
**Response:**
```json
{
  "status": "healthy",
  "connection": "active",
  "responseTime": "15ms",
  "migrations": "up-to-date"
}
```

## Frontend Health Check

**Endpoint:** `GET /api/health` (from Next.js)  
**Purpose:** Verify frontend application is serving correctly  
**Response:**
```json
{
  "status": "healthy",
  "buildTime": "2024-01-15T09:00:00Z",
  "nextVersion": "15.5.3",
  "apiConnection": "active"
}
```

## Usage in CI/CD

These endpoints are automatically used by the deployment pipeline to:
- ✅ Verify successful deployment
- ✅ Run smoke tests after deployment  
- ✅ Monitor application health
- ✅ Trigger rollback if health checks fail

## Monitoring Integration

Health checks are integrated with:
- **Docker healthcheck commands**
- **Kubernetes readiness/liveness probes** 
- **Load balancer health checks**
- **Application monitoring systems**

---

*Last updated: $(date)*
*This file is automatically referenced by deployment workflows*