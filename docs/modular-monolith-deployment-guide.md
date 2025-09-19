# ERP System - Modular Monolith Deployment Guide

## 🎯 **Architecture Overview**

Your ERP system uses a **Modular Monolith** architecture, which provides:

- **Single deployable unit** with all ERP modules (HR, Finance, Inventory, Sales, etc.)
- **Modular internal structure** with clear boundaries between business domains
- **Shared database and services** for consistency and simplified transactions
- **High availability** through multiple identical instances behind a load balancer
- **Simplified deployment** compared to microservices while maintaining modularity

## 🏗️ **System Architecture**

```
Internet → Nginx Load Balancer → Multiple ERP Instances → Shared Database + Redis
                ↓
    [ERP-Primary, ERP-Secondary] (Identical Modular Monoliths)
                ↓
         Shared Redis Cache ← → Production SQL Server
                ↓
         Optional: Prometheus + Grafana Monitoring
```

## 🚀 **Quick Start Deployment**

### **1. Environment Configuration**

Create `.env` file in the project root:

```bash
# Database Configuration
DB_CONNECTION_STRING=Server=sqlserver,1433;Database=ErpSystemProd;User Id=sa;Password=YourStrongPassword123!;MultipleActiveResultSets=true;TrustServerCertificate=true;
SQL_SA_PASSWORD=YourStrongPassword123!

# Redis Configuration
REDIS_CONNECTION_STRING=redis:6379,password=YourRedisPassword123!
REDIS_PASSWORD=YourRedisPassword123!

# JWT Security
JWT_SECRET_KEY=YourVeryLongAndSecure256BitJWTKeyHere12345678901234567890123456789012345678901234567890
JWT_ISSUER=ErpSystem
JWT_AUDIENCE=ErpSystemUsers

# Frontend Configuration
NEXTAUTH_SECRET=YourNextAuthSecretKey123456789012345678901234567890
NEXTAUTH_URL=https://erp.company.com

# SSL Certificates (optional)
SSL_CERT_PATH=./ssl/erp.company.com.crt
SSL_KEY_PATH=./ssl/erp.company.com.key

# Monitoring (optional)
GRAFANA_ADMIN_PASSWORD=admin123!
```

### **2. Deploy with Docker Compose**

```bash
# Start the modular monolith deployment
docker-compose -f docs/docker-compose.monolith.yml up -d

# Check all services status
docker-compose -f docs/docker-compose.monolith.yml ps

# View logs from all services
docker-compose -f docs/docker-compose.monolith.yml logs -f

# View logs from specific service
docker-compose -f docs/docker-compose.monolith.yml logs -f api-primary
```

### **3. Verify Deployment**

```bash
# Test overall system health
curl -f http://localhost/health

# Test API endpoints directly
curl -f http://localhost/api/health

# Test database connectivity  
curl -f http://localhost/api/health/database

# Test Redis connectivity
curl -f http://localhost/api/health/redis

# Check monitoring (if enabled)
curl -f http://localhost:9090  # Prometheus metrics
curl -f http://localhost:3001  # Grafana dashboard
```

## 🔧 **Modular Monolith Features**

### **Business Modules**

Your ERP system includes these integrated modules:

- **HR Module**: Employee management, payroll, time tracking
- **Finance Module**: Accounting, invoicing, financial reporting
- **Inventory Module**: Stock management, warehousing, tracking
- **Sales Module**: CRM, orders, customer management
- **Procurement Module**: Purchasing, vendor management
- **Marketing Module**: Campaigns, leads, analytics
- **Administration Module**: User management, system configuration

All modules share:
- Common authentication and authorization
- Shared database with referential integrity
- Unified audit logging and security
- Consistent API patterns and error handling

### **Module Configuration**

Enable/disable modules via environment variables:

```bash
# In docker-compose.monolith.yml or .env
Modules__HR__Enabled=true
Modules__Finance__Enabled=true
Modules__Inventory__Enabled=true
Modules__Sales__Enabled=true
Modules__Procurement__Enabled=true
Modules__Marketing__Enabled=false  # Disable if not needed
Modules__Administration__Enabled=true
```

### **High Availability Setup**

The deployment includes:
- **2 identical ERP instances** (primary + secondary)
- **Nginx load balancer** with health checks
- **Automatic failover** if one instance fails
- **Shared Redis cache** for session state
- **Shared database** for data consistency

## 📊 **Health Monitoring**

### **Health Check Endpoints**

| Endpoint | Purpose | Response |
|----------|---------|----------|
| `/health` | Overall system health | 200 if healthy |
| `/health/ready` | Application readiness | 200 when ready |
| `/health/live` | Application liveness | 200 if alive |
| `/health/database` | Database connectivity | 200 if DB healthy |
| `/health/redis` | Redis connectivity | 200 if Redis healthy |

### **Service Health Monitoring**

```bash
# Check all container health
docker-compose -f docs/docker-compose.monolith.yml ps

# Individual service health
docker inspect erp-api-primary --format='{{.State.Health.Status}}'
docker inspect erp-sqlserver --format='{{.State.Health.Status}}'
docker inspect erp-redis --format='{{.State.Health.Status}}'

# Load balancer upstream status
curl http://localhost:8080/nginx_status
```

## ⚖️ **Load Balancer Configuration**

### **Nginx Configuration**

The load balancer routes traffic between your ERP instances:

```nginx
upstream erp_backend {
    server api-primary:5000 weight=1 max_fails=3 fail_timeout=30s;
    server api-secondary:5000 weight=1 max_fails=3 fail_timeout=30s;
    least_conn;  # Route to instance with fewer connections
}

upstream erp_frontend {
    server frontend-primary:3000 weight=1 max_fails=3 fail_timeout=30s;
    server frontend-secondary:3000 weight=1 max_fails=3 fail_timeout=30s;
    least_conn;
}
```

### **Scaling Options**

To add more instances for higher load:

1. **Add new services** in docker-compose.monolith.yml:
```yaml
api-tertiary:
  # Copy from api-primary configuration
  container_name: erp-api-tertiary
  # ... same config as primary
```

2. **Update nginx upstream** configuration:
```nginx
upstream erp_backend {
    server api-primary:5000 weight=1;
    server api-secondary:5000 weight=1;
    server api-tertiary:5000 weight=1;  # New instance
    least_conn;
}
```

3. **Redeploy**:
```bash
docker-compose -f docs/docker-compose.monolith.yml up -d --scale api-primary=3
```

## 🔒 **Security Configuration**

### **Database Security**
- SQL Server authentication with strong password
- Network isolation via Docker networks
- Encrypted connections with TrustServerCertificate

### **API Security**
- JWT authentication with configurable expiration
- HTTPS termination at load balancer
- Rate limiting and DDoS protection via Nginx

### **Redis Security**
- Password-protected Redis instance
- Network isolation
- Encrypted data in transit

## 🗄️ **Database Management**

### **Backup Strategy**

```bash
# Create database backup
docker exec erp-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SQL_SA_PASSWORD} -Q "BACKUP DATABASE ErpSystemProd TO DISK = '/var/opt/mssql/backup/ErpSystemProd.bak'"

# Restore database backup
docker exec erp-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SQL_SA_PASSWORD} -Q "RESTORE DATABASE ErpSystemProd FROM DISK = '/var/opt/mssql/backup/ErpSystemProd.bak'"
```

### **Database Optimization**

For the final todo item (Phase 2.3), you can optimize:

1. **Connection Pooling**: Already configured in EF Core
2. **Indexing**: Add database indices for frequent queries
3. **Read Replicas**: Add read-only SQL Server replicas for reporting
4. **Query Optimization**: Use EF Core query performance tools

## 🚀 **Production Deployment Checklist**

- [ ] Configure production SQL Server instance
- [ ] Set strong passwords for all services
- [ ] Configure SSL certificates for HTTPS
- [ ] Set up database backups
- [ ] Configure monitoring and alerting
- [ ] Test failover scenarios
- [ ] Set up log aggregation
- [ ] Configure firewall rules
- [ ] Test load balancer health checks
- [ ] Document disaster recovery procedures

## 📈 **Performance Tuning**

### **API Performance**
- **Thread pool tuning**: `ThreadPool__MinWorkerThreads=50`
- **Memory limits**: 2GB per API instance
- **CPU allocation**: 1.0 CPU per instance

### **Database Performance**
- **Connection pooling**: Max 100 connections per API instance
- **Memory allocation**: 4GB for SQL Server
- **Index optimization**: Regular index maintenance

### **Cache Performance**
- **Redis memory**: 1GB allocated
- **Cache hit ratio monitoring**: via Prometheus metrics
- **TTL optimization**: Based on data access patterns

This modular monolith approach gives you enterprise-scale capabilities while maintaining deployment simplicity and operational efficiency.