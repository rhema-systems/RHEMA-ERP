#!/usr/bin/env python3
"""
ERP System - Complete Modular Monolith Architecture Diagram Generator
Generates comprehensive visual architecture diagrams in PNG and PDF formats
Includes production deployment, load balancing, and infrastructure details
"""

import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.patches import FancyBboxPatch, Rectangle, Circle
import numpy as np

def create_production_architecture_diagram():
    """Creates the main production architecture diagram"""
    # Create figure and axis
    fig, ax = plt.subplots(1, 1, figsize=(24, 30))
    ax.set_xlim(0, 12)
    ax.set_ylim(0, 16)
    ax.axis('off')
    
    # Enhanced color scheme for production architecture
    colors = {
        'internet': '#6366F1',       # Indigo
        'loadbalancer': '#F59E0B',   # Amber
        'frontend': '#3B82F6',       # Blue
        'backend': '#10B981',        # Green
        'database': '#DC2626',       # Red
        'cache': '#8B5CF6',          # Purple
        'monitoring': '#059669',     # Emerald
        'middleware': '#7C3AED',     # Violet
        'services': '#EF4444',       # Red
        'light_bg': '#F8FAFC',       # Light gray
        'text': '#1F2937',           # Dark gray
        'border': '#374151'          # Dark gray border
    }
    
    # Main Title
    ax.text(6, 15.5, 'ERP SYSTEM - PRODUCTION MODULAR MONOLITH ARCHITECTURE', 
            ha='center', va='center', fontsize=18, fontweight='bold', color=colors['text'])
    ax.text(6, 15.2, 'High Availability • Load Balanced • Enterprise Scale', 
            ha='center', va='center', fontsize=12, style='italic', color=colors['text'])
    
    # Internet/Users Layer (Top)
    internet_box = FancyBboxPatch((2, 14.2), 8, 0.6, 
                                  boxstyle="round,pad=0.1", 
                                  facecolor=colors['internet'], 
                                  edgecolor=colors['border'], 
                                  alpha=0.8)
    ax.add_patch(internet_box)
    ax.text(6, 14.5, '🌐 INTERNET / EXTERNAL USERS', ha='center', va='center', 
            fontsize=14, fontweight='bold', color='white')
    
    # Load Balancer Layer
    lb_box = FancyBboxPatch((2, 13.2), 8, 0.8, 
                            boxstyle="round,pad=0.1", 
                            facecolor=colors['loadbalancer'], 
                            edgecolor=colors['border'], 
                            alpha=0.8)
    ax.add_patch(lb_box)
    ax.text(6, 13.7, '⚖️ NGINX LOAD BALANCER', ha='center', va='center', 
            fontsize=14, fontweight='bold', color='white')
    ax.text(6, 13.4, 'SSL Termination • Rate Limiting • Health Checks • Failover', ha='center', va='center', 
            fontsize=10, color='white')
    
    # Frontend components
    components = [
        ('Dashboard\nComponents', 1.2, 10),
        ('Admin\nFeatures', 2.8, 10),
        ('Identity\nMgmt', 4.4, 10),
        ('Reports &\nAnalytics', 6, 10),
        ('UI Components\n& Forms', 7.6, 10)
    ]
    
    for comp, x, y in components:
        comp_box = FancyBboxPatch((x-0.4, y-0.2), 0.8, 0.4,
                                  boxstyle="round,pad=0.05",
                                  facecolor='white',
                                  edgecolor=colors['frontend'],
                                  alpha=0.9)
        ax.add_patch(comp_box)
        ax.text(x, y, comp, ha='center', va='center', fontsize=8, color=colors['text'])
    
    # Service Layer
    service_box = FancyBboxPatch((0.5, 8.5), 9, 0.8,
                                 boxstyle="round,pad=0.1",
                                 facecolor=colors['services'],
                                 edgecolor='black',
                                 alpha=0.8)
    ax.add_patch(service_box)
    ax.text(5, 9, 'SERVICE LAYER', ha='center', va='center',
            fontsize=12, fontweight='bold', color='white')
    ax.text(5, 8.7, 'API Services • Auth Service • TanStack Query • React Context', 
            ha='center', va='center', fontsize=10, color='white')
    
    # Backend API Layer (Middle)
    backend_box = FancyBboxPatch((0.5, 5.5), 9, 2.8,
                                 boxstyle="round,pad=0.1",
                                 facecolor=colors['backend'],
                                 edgecolor='black',
                                 alpha=0.8)
    ax.add_patch(backend_box)
    ax.text(5, 7.8, 'BACKEND API LAYER', ha='center', va='center',
            fontsize=14, fontweight='bold', color='white')
    ax.text(5, 7.5, 'ASP.NET Core 8 Web API', ha='center', va='center',
            fontsize=11, color='white')
    
    # Controllers
    controllers = [
        ('Auth\nController', 1.5, 7.2),
        ('Tenant\nController', 2.8, 7.2),
        ('User\nController', 4.1, 7.2),
        ('Role\nController', 5.4, 7.2),
        ('Settings\nController', 6.7, 7.2),
        ('Logs\nControllers', 8, 7.2)
    ]
    
    for ctrl, x, y in controllers:
        ctrl_box = FancyBboxPatch((x-0.35, y-0.2), 0.7, 0.4,
                                  boxstyle="round,pad=0.05",
                                  facecolor='white',
                                  edgecolor=colors['backend'],
                                  alpha=0.9)
        ax.add_patch(ctrl_box)
        ax.text(x, y, ctrl, ha='center', va='center', fontsize=8, color=colors['text'])
    
    # Middleware
    middleware_box = FancyBboxPatch((1, 6.5), 8, 0.4,
                                    boxstyle="round,pad=0.05",
                                    facecolor=colors['middleware'],
                                    edgecolor='black',
                                    alpha=0.7)
    ax.add_patch(middleware_box)
    ax.text(5, 6.7, 'MIDDLEWARE: CORS • JWT Auth • Request Logging • Error Handling',
            ha='center', va='center', fontsize=10, fontweight='bold', color='white')
    
    # Business Logic Services
    services = [
        ('Tenant\nService', 1.3, 6),
        ('User\nService', 2.4, 6),
        ('Role\nService', 3.5, 6),
        ('Settings\nService', 4.6, 6),
        ('Audit Log\nService', 5.7, 6),
        ('Security Log\nService', 6.8, 6),
        ('Current User\nService', 7.9, 6)
    ]
    
    for svc, x, y in services:
        svc_box = FancyBboxPatch((x-0.3, y-0.2), 0.6, 0.4,
                                 boxstyle="round,pad=0.05",
                                 facecolor='white',
                                 edgecolor=colors['backend'],
                                 alpha=0.9)
        ax.add_patch(svc_box)
        ax.text(x, y, svc, ha='center', va='center', fontsize=7, color=colors['text'])
    
    # Data Access Layer
    data_box = FancyBboxPatch((0.5, 3.5), 9, 1.8,
                              boxstyle="round,pad=0.1",
                              facecolor=colors['database'],
                              edgecolor='black',
                              alpha=0.8)
    ax.add_patch(data_box)
    ax.text(5, 4.8, 'DATA ACCESS LAYER', ha='center', va='center',
            fontsize=14, fontweight='bold', color='white')
    ax.text(5, 4.5, 'Entity Framework Core + Repository Pattern', ha='center', va='center',
            fontsize=11, color='white')
    
    # Repositories
    repos = [
        ('Generic\nRepository', 1.5, 4.2),
        ('Tenant\nRepository', 2.8, 4.2),
        ('User\nRepository', 4.1, 4.2),
        ('Role\nRepository', 5.4, 4.2),
        ('Audit\nRepository', 6.7, 4.2),
        ('Unit of Work', 8, 4.2)
    ]
    
    for repo, x, y in repos:
        repo_box = FancyBboxPatch((x-0.35, y-0.2), 0.7, 0.4,
                                  boxstyle="round,pad=0.05",
                                  facecolor='white',
                                  edgecolor=colors['database'],
                                  alpha=0.9)
        ax.add_patch(repo_box)
        ax.text(x, y, repo, ha='center', va='center', fontsize=8, color=colors['text'])
    
    # Entities
    entities = [
        ('Tenant', 1.8, 3.8),
        ('ApplicationUser', 3.2, 3.8),
        ('ApplicationRole', 4.6, 3.8),
        ('AuditLog', 6, 3.8),
        ('SecurityLog', 7.4, 3.8)
    ]
    
    for entity, x, y in entities:
        entity_box = FancyBboxPatch((x-0.3, y-0.15), 0.6, 0.3,
                                    boxstyle="round,pad=0.03",
                                    facecolor=colors['light_bg'],
                                    edgecolor=colors['database'],
                                    alpha=0.9)
        ax.add_patch(entity_box)
        ax.text(x, y, entity, ha='center', va='center', fontsize=7, color=colors['text'])
    
    # Database Layer (Bottom)
    db_box = FancyBboxPatch((0.5, 1.5), 9, 1.8,
                            boxstyle="round,pad=0.1",
                            facecolor='#374151',
                            edgecolor='black',
                            alpha=0.8)
    ax.add_patch(db_box)
    ax.text(5, 2.8, 'DATABASE LAYER', ha='center', va='center',
            fontsize=14, fontweight='bold', color='white')
    ax.text(5, 2.5, 'SQL Server LocalDB', ha='center', va='center',
            fontsize=11, color='white')
    
    # Database tables
    tables = [
        ('Tenants', 1.5, 2.2),
        ('AspNetUsers', 2.8, 2.2),
        ('AspNetRoles', 4.1, 2.2),
        ('AspNetUserRoles', 5.4, 2.2),
        ('AuditLogs', 6.7, 2.2),
        ('SecurityLogs', 8, 2.2)
    ]
    
    for table, x, y in tables:
        table_box = FancyBboxPatch((x-0.35, y-0.2), 0.7, 0.4,
                                   boxstyle="round,pad=0.05",
                                   facecolor='white',
                                   edgecolor='#374151',
                                   alpha=0.9)
        ax.add_patch(table_box)
        ax.text(x, y, table, ha='center', va='center', fontsize=8, color=colors['text'])
    
    # Features
    features = [
        ('Multi-Tenant\nArchitecture', 2, 1.8),
        ('RBAC\nSecurity', 4, 1.8),
        ('Audit\nLogging', 6, 1.8),
        ('Data\nIsolation', 8, 1.8)
    ]
    
    for feature, x, y in features:
        feature_box = FancyBboxPatch((x-0.4, y-0.15), 0.8, 0.3,
                                     boxstyle="round,pad=0.03",
                                     facecolor=colors['light_bg'],
                                     edgecolor='#374151',
                                     alpha=0.9)
        ax.add_patch(feature_box)
        ax.text(x, y, feature, ha='center', va='center', fontsize=7, color=colors['text'])
    
    # Connection arrows
    # Frontend to Backend
    ax.annotate('', xy=(5, 8.3), xytext=(5, 9.5),
                arrowprops=dict(arrowstyle='->', lw=2, color='black', alpha=0.7))
    ax.text(5.2, 8.9, 'HTTP/HTTPS\nREST API', ha='left', va='center', fontsize=8, color=colors['text'])
    
    # Backend to Data
    ax.annotate('', xy=(5, 5.3), xytext=(5, 5.5),
                arrowprops=dict(arrowstyle='->', lw=2, color='black', alpha=0.7))
    ax.text(5.2, 5.4, 'Entity\nFramework', ha='left', va='center', fontsize=8, color=colors['text'])
    
    # Data to Database
    ax.annotate('', xy=(5, 3.3), xytext=(5, 3.5),
                arrowprops=dict(arrowstyle='->', lw=2, color='black', alpha=0.7))
    ax.text(5.2, 3.4, 'SQL\nConnection', ha='left', va='center', fontsize=8, color=colors['text'])
    
    # Technology Stack Info
    tech_box = FancyBboxPatch((0.5, 0.2), 9, 1,
                              boxstyle="round,pad=0.1",
                              facecolor=colors['light_bg'],
                              edgecolor='black',
                              alpha=0.9)
    ax.add_patch(tech_box)
    ax.text(5, 1, 'TECHNOLOGY STACK', ha='center', va='center',
            fontsize=12, fontweight='bold', color=colors['text'])
    
    tech_info = [
        'Frontend: Next.js 14 • React 18 • TypeScript • TanStack Query • React Hook Form',
        'Backend: ASP.NET Core 8 • C# • Entity Framework Core • JWT Auth • Serilog',
        'Database: SQL Server LocalDB • Multi-Tenant • RBAC • Audit Logging',
        'State: TanStack Query (Server) • React Context (Client) • LocalStorage (Persist)'
    ]
    
    for i, info in enumerate(tech_info):
        ax.text(5, 0.8 - i*0.15, info, ha='center', va='center', 
                fontsize=9, color=colors['text'])
    
    # Save the diagram
    plt.tight_layout()
    plt.savefig('C:/Users/micha/erp-system/docs/erp-architecture-diagram.png', 
                dpi=300, bbox_inches='tight', facecolor='white', edgecolor='none')
    print("Architecture diagram saved as 'erp-architecture-diagram.png'")
    
    # Also save as PDF for better quality
    plt.savefig('C:/Users/micha/erp-system/docs/erp-architecture-diagram.pdf', 
                bbox_inches='tight', facecolor='white', edgecolor='none')
    print("Architecture diagram also saved as 'erp-architecture-diagram.pdf'")

if __name__ == "__main__":
    create_architecture_diagram()