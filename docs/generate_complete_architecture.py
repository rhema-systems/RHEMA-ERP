#!/usr/bin/env python3
"""
ERP System - Complete Modular Monolith Production Architecture Diagram Generator
Shows detailed production deployment with load balancing, high availability, and all components
"""

import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.patches import FancyBboxPatch, Rectangle, Circle, Arrow
import numpy as np

def create_complete_architecture():
    """Creates comprehensive production architecture diagram"""
    # Create figure with larger canvas
    fig, ax = plt.subplots(1, 1, figsize=(28, 36))
    ax.set_xlim(0, 14)
    ax.set_ylim(0, 18)
    ax.axis('off')
    
    # Enhanced color scheme
    colors = {
        'internet': '#1E40AF',       # Blue 800
        'loadbalancer': '#D97706',   # Amber 600
        'frontend': '#2563EB',       # Blue 600
        'backend': '#059669',        # Emerald 600
        'database': '#DC2626',       # Red 600
        'cache': '#7C3AED',          # Violet 600
        'monitoring': '#065F46',     # Emerald 800
        'network': '#374151',        # Gray 700
        'light_bg': '#F3F4F6',       # Gray 100
        'text': '#111827',           # Gray 900
        'border': '#6B7280',         # Gray 500
        'success': '#10B981',        # Emerald 500
        'warning': '#F59E0B'         # Amber 500
    }
    
    # Main Title
    ax.text(7, 17.5, 'ERP SYSTEM - PRODUCTION MODULAR MONOLITH ARCHITECTURE', 
            ha='center', va='center', fontsize=20, fontweight='bold', color=colors['text'])
    ax.text(7, 17.1, 'High Availability • Load Balanced • Enterprise Scale • Multi-Instance Deployment', 
            ha='center', va='center', fontsize=12, style='italic', color=colors['text'])
    
    # Layer 1: Internet/Users
    internet_box = FancyBboxPatch((1, 16.2), 12, 0.7, 
                                  boxstyle="round,pad=0.1", 
                                  facecolor=colors['internet'], 
                                  edgecolor=colors['border'], 
                                  alpha=0.9)
    ax.add_patch(internet_box)
    ax.text(7, 16.55, '🌐 INTERNET / EXTERNAL USERS', ha='center', va='center', 
            fontsize=16, fontweight='bold', color='white')
    
    # Add user icons
    user_positions = [2.5, 4.5, 6.5, 9.5, 11.5]
    for pos in user_positions:
        user_circle = Circle((pos, 16.3), 0.15, facecolor='white', edgecolor=colors['internet'], alpha=0.8)
        ax.add_patch(user_circle)
        ax.text(pos, 16.3, '👤', ha='center', va='center', fontsize=10)
    
    # Layer 2: Load Balancer
    lb_box = FancyBboxPatch((2, 15), 10, 1, 
                            boxstyle="round,pad=0.1", 
                            facecolor=colors['loadbalancer'], 
                            edgecolor=colors['border'], 
                            alpha=0.9)
    ax.add_patch(lb_box)
    ax.text(7, 15.7, '⚖️ NGINX LOAD BALANCER', ha='center', va='center', 
            fontsize=16, fontweight='bold', color='white')
    ax.text(7, 15.4, 'Port 80/443 • SSL Termination • Health Checks', ha='center', va='center', 
            fontsize=11, color='white')
    ax.text(7, 15.1, 'Rate Limiting • Gzip • WebSocket Proxy • Automatic Failover', ha='center', va='center', 
            fontsize=10, color='white')
    
    # Layer 3: Application Instances
    # Frontend Instances
    frontend_y = 13.5
    
    # Frontend Primary
    frontend1_box = FancyBboxPatch((0.5, frontend_y-0.4), 3, 1.2, 
                                   boxstyle="round,pad=0.1", 
                                   facecolor=colors['frontend'], 
                                   edgecolor=colors['border'], 
                                   alpha=0.9)
    ax.add_patch(frontend1_box)
    ax.text(2, frontend_y+0.3, '🖥️ FRONTEND PRIMARY', ha='center', va='center', 
            fontsize=12, fontweight='bold', color='white')
    ax.text(2, frontend_y, 'Next.js + React + TypeScript', ha='center', va='center', 
            fontsize=9, color='white')
    ax.text(2, frontend_y-0.2, 'Port 3000 • SSR/SSG • PWA', ha='center', va='center', 
            fontsize=8, color='white')
    
    # Frontend Secondary  
    frontend2_box = FancyBboxPatch((4, frontend_y-0.4), 3, 1.2, 
                                   boxstyle="round,pad=0.1", 
                                   facecolor=colors['frontend'], 
                                   edgecolor=colors['border'], 
                                   alpha=0.9)
    ax.add_patch(frontend2_box)
    ax.text(5.5, frontend_y+0.3, '🖥️ FRONTEND SECONDARY', ha='center', va='center', 
            fontsize=12, fontweight='bold', color='white')
    ax.text(5.5, frontend_y, 'Next.js + React + TypeScript', ha='center', va='center', 
            fontsize=9, color='white')
    ax.text(5.5, frontend_y-0.2, 'Port 3000 • SSR/SSG • PWA', ha='center', va='center', 
            fontsize=8, color='white')
    
    # API Instances
    # API Primary
    api1_box = FancyBboxPatch((7.5, frontend_y-0.4), 3, 1.2, 
                              boxstyle="round,pad=0.1", 
                              facecolor=colors['backend'], 
                              edgecolor=colors['border'], 
                              alpha=0.9)
    ax.add_patch(api1_box)
    ax.text(9, frontend_y+0.3, '⚙️ API PRIMARY', ha='center', va='center', 
            fontsize=12, fontweight='bold', color='white')
    ax.text(9, frontend_y, 'ASP.NET Core 8 + C#', ha='center', va='center', 
            fontsize=9, color='white')
    ax.text(9, frontend_y-0.2, 'Port 5000 • REST • JWT', ha='center', va='center', 
            fontsize=8, color='white')
    
    # API Secondary
    api2_box = FancyBboxPatch((11, frontend_y-0.4), 3, 1.2, 
                              boxstyle="round,pad=0.1", 
                              facecolor=colors['backend'], 
                              edgecolor=colors['border'], 
                              alpha=0.9)
    ax.add_patch(api2_box)
    ax.text(12.5, frontend_y+0.3, '⚙️ API SECONDARY', ha='center', va='center', 
            fontsize=12, fontweight='bold', color='white')
    ax.text(12.5, frontend_y, 'ASP.NET Core 8 + C#', ha='center', va='center', 
            fontsize=9, color='white')
    ax.text(12.5, frontend_y-0.2, 'Port 5000 • REST • JWT', ha='center', va='center', 
            fontsize=8, color='white')
    
    # Layer 4: ERP Modules (Modular Monolith)
    modules_y = 11.5
    modules_box = FancyBboxPatch((1, modules_y-0.8), 12, 1.6, 
                                 boxstyle="round,pad=0.1", 
                                 facecolor=colors['success'], 
                                 edgecolor=colors['border'], 
                                 alpha=0.9)
    ax.add_patch(modules_box)
    ax.text(7, modules_y+0.4, '📦 MODULAR MONOLITH - BUSINESS MODULES', ha='center', va='center', 
            fontsize=16, fontweight='bold', color='white')
    
    # Module boxes
    module_data = [
        ('👥\nHR', 2, modules_y-0.1),
        ('💰\nFinance', 3.5, modules_y-0.1),
        ('📦\nInventory', 5, modules_y-0.1),
        ('🛒\nSales', 6.5, modules_y-0.1),
        ('📞\nMarketing', 8, modules_y-0.1),
        ('🛒\nProcurement', 9.5, modules_y-0.1),
        ('⚙️\nAdmin', 11, modules_y-0.1),
        ('📊\nReports', 12, modules_y-0.1)
    ]
    
    for module, x, y in module_data:
        module_box = FancyBboxPatch((x-0.4, y-0.3), 0.8, 0.6, 
                                    boxstyle="round,pad=0.05", 
                                    facecolor='white', 
                                    edgecolor=colors['success'], 
                                    alpha=0.9)
        ax.add_patch(module_box)
        ax.text(x, y, module, ha='center', va='center', fontsize=9, color=colors['text'])
    
    # Layer 5: Service Layer
    service_y = 9.5
    service_box = FancyBboxPatch((1, service_y-0.6), 12, 1.2, 
                                 boxstyle="round,pad=0.1", 
                                 facecolor=colors['network'], 
                                 edgecolor=colors['border'], 
                                 alpha=0.9)
    ax.add_patch(service_box)
    ax.text(7, service_y+0.2, '🔧 SERVICE LAYER - SHARED SERVICES', ha='center', va='center', 
            fontsize=14, fontweight='bold', color='white')
    ax.text(7, service_y-0.1, 'Authentication • Authorization • Logging • Caching • Email • LDAP', ha='center', va='center', 
            fontsize=10, color='white')
    ax.text(7, service_y-0.4, 'Current User Service • Audit Service • Security Service • Settings Service', ha='center', va='center', 
            fontsize=9, color='white')
    
    # Layer 6: Data Layer
    data_y = 8
    data_box = FancyBboxPatch((1, data_y-0.6), 12, 1.2, 
                              boxstyle="round,pad=0.1", 
                              facecolor=colors['warning'], 
                              edgecolor=colors['border'], 
                              alpha=0.9)
    ax.add_patch(data_box)
    ax.text(7, data_y+0.2, '🗃️ DATA ACCESS LAYER', ha='center', va='center', 
            fontsize=14, fontweight='bold', color='white')
    ax.text(7, data_y-0.1, 'Entity Framework Core • Repository Pattern • Unit of Work', ha='center', va='center', 
            fontsize=10, color='white')
    ax.text(7, data_y-0.4, 'Generic Repository • Tenant Repository • User Repository • Audit Repository', ha='center', va='center', 
            fontsize=9, color='white')
    
    # Layer 7: Infrastructure Services
    infra_y = 6
    
    # SQL Server
    sql_box = FancyBboxPatch((0.5, infra_y-0.6), 3.5, 1.2, 
                             boxstyle="round,pad=0.1", 
                             facecolor=colors['database'], 
                             edgecolor=colors['border'], 
                             alpha=0.9)
    ax.add_patch(sql_box)
    ax.text(2.25, infra_y+0.2, '🗄️ SQL SERVER', ha='center', va='center', 
            fontsize=12, fontweight='bold', color='white')
    ax.text(2.25, infra_y-0.1, 'Production Database', ha='center', va='center', 
            fontsize=10, color='white')
    ax.text(2.25, infra_y-0.4, 'Multi-Tenant • RBAC • Audit', ha='center', va='center', 
            fontsize=9, color='white')
    
    # Redis Cache
    redis_box = FancyBboxPatch((5, infra_y-0.6), 3.5, 1.2, 
                               boxstyle="round,pad=0.1", 
                               facecolor=colors['cache'], 
                               edgecolor=colors['border'], 
                               alpha=0.9)
    ax.add_patch(redis_box)
    ax.text(6.75, infra_y+0.2, '⚡ REDIS CACHE', ha='center', va='center', 
            fontsize=12, fontweight='bold', color='white')
    ax.text(6.75, infra_y-0.1, 'Distributed Caching', ha='center', va='center', 
            fontsize=10, color='white')
    ax.text(6.75, infra_y-0.4, 'Session State • Data Cache', ha='center', va='center', 
            fontsize=9, color='white')
    
    # Monitoring Stack
    monitor_box = FancyBboxPatch((10, infra_y-0.6), 3.5, 1.2, 
                                 boxstyle="round,pad=0.1", 
                                 facecolor=colors['monitoring'], 
                                 edgecolor=colors['border'], 
                                 alpha=0.9)
    ax.add_patch(monitor_box)
    ax.text(11.75, infra_y+0.2, '📊 MONITORING', ha='center', va='center', 
            fontsize=12, fontweight='bold', color='white')
    ax.text(11.75, infra_y-0.1, 'Prometheus + Grafana', ha='center', va='center', 
            fontsize=10, color='white')
    ax.text(11.75, infra_y-0.4, 'Health • Metrics • Alerts', ha='center', va='center', 
            fontsize=9, color='white')
    
    # Layer 8: Performance Characteristics
    perf_y = 4
    perf_box = FancyBboxPatch((1, perf_y-0.8), 12, 1.6, 
                              boxstyle="round,pad=0.1", 
                              facecolor=colors['light_bg'], 
                              edgecolor=colors['border'], 
                              alpha=0.9)
    ax.add_patch(perf_box)
    ax.text(7, perf_y+0.4, '⚡ PERFORMANCE CHARACTERISTICS', ha='center', va='center', 
            fontsize=14, fontweight='bold', color=colors['text'])
    
    perf_data = [
        ('🚀 1000+\nReq/Sec', 2.5, perf_y-0.1),
        ('🔗 400\nConnections', 4.5, perf_y-0.1),
        ('💾 Redis\nUnlimited', 6.5, perf_y-0.1),
        ('⚖️ Auto\nFailover', 8.5, perf_y-0.1),
        ('🔄 Zero\nDowntime', 10.5, perf_y-0.1)
    ]
    
    for perf, x, y in perf_data:
        perf_box_inner = FancyBboxPatch((x-0.6, y-0.35), 1.2, 0.7, 
                                        boxstyle="round,pad=0.05", 
                                        facecolor='white', 
                                        edgecolor=colors['success'], 
                                        alpha=0.9)
        ax.add_patch(perf_box_inner)
        ax.text(x, y, perf, ha='center', va='center', fontsize=9, color=colors['text'])
    
    # Layer 9: Technology Stack
    tech_y = 2
    tech_box = FancyBboxPatch((1, tech_y-1), 12, 2, 
                              boxstyle="round,pad=0.1", 
                              facecolor=colors['network'], 
                              edgecolor=colors['border'], 
                              alpha=0.9)
    ax.add_patch(tech_box)
    ax.text(7, tech_y+0.6, '🛠️ TECHNOLOGY STACK', ha='center', va='center', 
            fontsize=16, fontweight='bold', color='white')
    
    tech_stack = [
        'Frontend: Next.js 14 • React 18 • TypeScript • Tailwind CSS • React Query',
        'Backend: ASP.NET Core 8 • C# • Entity Framework Core • JWT Authentication',  
        'Database: SQL Server • Multi-Tenant • Connection Pooling • Retry Logic',
        'Cache: Redis • Distributed Caching • Session State • Health Monitoring',
        'Infrastructure: Docker • Nginx • Load Balancing • SSL/TLS • Health Checks',
        'Monitoring: Prometheus • Grafana • Serilog • Performance Metrics'
    ]
    
    for i, tech in enumerate(tech_stack):
        ax.text(7, tech_y+0.2 - i*0.2, tech, ha='center', va='center', 
                fontsize=9, color='white')
    
    # Add connection arrows
    # Internet to Load Balancer
    ax.annotate('', xy=(7, 15.9), xytext=(7, 16.2),
                arrowprops=dict(arrowstyle='->', lw=3, color=colors['internet'], alpha=0.8))
    
    # Load Balancer to Instances  
    # To Frontend 1
    ax.annotate('', xy=(2, 14.3), xytext=(5, 15),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['loadbalancer'], alpha=0.8))
    # To Frontend 2
    ax.annotate('', xy=(5.5, 14.3), xytext=(6, 15),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['loadbalancer'], alpha=0.8))
    # To API 1
    ax.annotate('', xy=(9, 14.3), xytext=(8, 15),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['loadbalancer'], alpha=0.8))
    # To API 2
    ax.annotate('', xy=(12.5, 14.3), xytext=(9, 15),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['loadbalancer'], alpha=0.8))
    
    # APIs to Services
    ax.annotate('', xy=(7, 10.1), xytext=(9, 12.7),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['backend'], alpha=0.8))
    ax.annotate('', xy=(7, 10.1), xytext=(12.5, 12.7),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['backend'], alpha=0.8))
    
    # Services to Data
    ax.annotate('', xy=(7, 8.6), xytext=(7, 8.9),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['network'], alpha=0.8))
    
    # Data to Infrastructure
    ax.annotate('', xy=(2.25, 6.6), xytext=(5, 7.4),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['warning'], alpha=0.8))
    ax.annotate('', xy=(6.75, 6.6), xytext=(8, 7.4),
                arrowprops=dict(arrowstyle='->', lw=2, color=colors['warning'], alpha=0.8))
    
    # Add deployment info box
    deploy_box = FancyBboxPatch((0.5, 0.2), 13, 0.6, 
                                boxstyle="round,pad=0.1", 
                                facecolor=colors['light_bg'], 
                                edgecolor=colors['border'], 
                                alpha=0.9)
    ax.add_patch(deploy_box)
    ax.text(7, 0.5, '🚀 DEPLOYMENT: docker-compose -f docs/docker-compose.monolith.yml up -d', 
            ha='center', va='center', fontsize=12, fontweight='bold', color=colors['text'])
    
    # Save the diagram
    plt.tight_layout()
    plt.savefig('C:/Users/micha/erp-system/docs/complete-production-architecture.png', 
                dpi=300, bbox_inches='tight', facecolor='white', edgecolor='none')
    print("✅ Complete production architecture diagram saved as 'complete-production-architecture.png'")
    
    plt.savefig('C:/Users/micha/erp-system/docs/complete-production-architecture.pdf', 
                bbox_inches='tight', facecolor='white', edgecolor='none')
    print("✅ Complete production architecture diagram also saved as 'complete-production-architecture.pdf'")
    
    plt.close()


def create_deployment_flow_diagram():
    """Creates a deployment flow diagram showing the request flow"""
    fig, ax = plt.subplots(1, 1, figsize=(20, 12))
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 8)
    ax.axis('off')
    
    colors = {
        'user': '#3B82F6',
        'flow': '#10B981',
        'process': '#F59E0B',
        'data': '#DC2626',
        'text': '#111827'
    }
    
    ax.text(5, 7.5, 'ERP SYSTEM - REQUEST FLOW & DEPLOYMENT ARCHITECTURE', 
            ha='center', va='center', fontsize=16, fontweight='bold', color=colors['text'])
    
    # Request flow boxes
    flow_steps = [
        ('👤 User\nRequest', 1, 6, colors['user']),
        ('⚖️ Nginx\nLoad Balancer', 3, 6, colors['flow']),
        ('🖥️ Frontend\n(Next.js)', 5, 6.5, colors['process']),
        ('⚙️ API Server\n(ASP.NET Core)', 7, 6.5, colors['process']),
        ('🗄️ Database\n(SQL Server)', 9, 6, colors['data']),
        ('⚡ Redis\nCache', 5, 4.5, colors['flow']),
        ('📊 Monitoring\n(Prometheus)', 7, 4.5, colors['data'])
    ]
    
    for text, x, y, color in flow_steps:
        box = FancyBboxPatch((x-0.6, y-0.4), 1.2, 0.8, 
                             boxstyle="round,pad=0.1", 
                             facecolor=color, 
                             edgecolor='black', 
                             alpha=0.8)
        ax.add_patch(box)
        ax.text(x, y, text, ha='center', va='center', 
                fontsize=10, fontweight='bold', color='white')
    
    # Add arrows showing flow
    arrows = [
        ((1.6, 6), (2.4, 6)),      # User to LB
        ((3.6, 6), (4.4, 6.5)),    # LB to Frontend
        ((5.6, 6.5), (6.4, 6.5)),  # Frontend to API
        ((7.6, 6.5), (8.4, 6)),    # API to DB
        ((7, 6.1), (5.6, 4.9)),    # API to Cache
        ((7, 6.1), (7, 4.9)),      # API to Monitoring
    ]
    
    for start, end in arrows:
        ax.annotate('', xy=end, xytext=start,
                    arrowprops=dict(arrowstyle='->', lw=2, color='black', alpha=0.7))
    
    # Add deployment info
    deployment_info = [
        "🔄 High Availability: 2 Frontend + 2 API Instances",
        "⚖️ Load Balancing: Nginx with least_conn algorithm", 
        "🛡️ Security: JWT Auth + RBAC + Multi-Tenant",
        "📊 Monitoring: Health checks every 30 seconds",
        "💾 Persistence: Shared SQL Server + Redis Cache",
        "🚀 Performance: 1000+ requests/sec, 400 DB connections"
    ]
    
    for i, info in enumerate(deployment_info):
        ax.text(5, 3.5 - i*0.3, info, ha='center', va='center', 
                fontsize=10, color=colors['text'])
    
    plt.tight_layout()
    plt.savefig('C:/Users/micha/erp-system/docs/deployment-flow-diagram.png', 
                dpi=300, bbox_inches='tight', facecolor='white', edgecolor='none')
    print("✅ Deployment flow diagram saved as 'deployment-flow-diagram.png'")
    
    plt.close()


def create_module_architecture_diagram():
    """Creates a detailed modular monolith internal architecture diagram"""
    fig, ax = plt.subplots(1, 1, figsize=(24, 16))
    ax.set_xlim(0, 12)
    ax.set_ylim(0, 10)
    ax.axis('off')
    
    colors = {
        'hr': '#EF4444',        # Red
        'finance': '#F59E0B',   # Amber  
        'inventory': '#10B981', # Emerald
        'sales': '#3B82F6',     # Blue
        'marketing': '#8B5CF6', # Violet
        'procurement': '#06B6D4', # Cyan
        'admin': '#6B7280',     # Gray
        'shared': '#374151',    # Dark gray
        'text': '#111827'
    }
    
    ax.text(6, 9.5, 'ERP SYSTEM - MODULAR MONOLITH INTERNAL ARCHITECTURE', 
            ha='center', va='center', fontsize=16, fontweight='bold', color=colors['text'])
    ax.text(6, 9.1, 'Single Deployable Unit with Modular Business Domains', 
            ha='center', va='center', fontsize=12, style='italic', color=colors['text'])
    
    # Shared Infrastructure Layer (Bottom)
    shared_box = FancyBboxPatch((1, 0.5), 10, 1.5, 
                                boxstyle="round,pad=0.1", 
                                facecolor=colors['shared'], 
                                edgecolor='black', 
                                alpha=0.9)
    ax.add_patch(shared_box)
    ax.text(6, 1.6, '🔧 SHARED INFRASTRUCTURE & SERVICES', 
            ha='center', va='center', fontsize=14, fontweight='bold', color='white')
    ax.text(6, 1.3, 'Authentication • Authorization • Logging • Caching • Database', 
            ha='center', va='center', fontsize=10, color='white')
    ax.text(6, 1.0, 'JWT Service • Current User • Audit Log • Security Log • Email Service', 
            ha='center', va='center', fontsize=9, color='white')
    ax.text(6, 0.7, 'Entity Framework • Repository Pattern • Unit of Work • Multi-Tenant Context', 
            ha='center', va='center', fontsize=9, color='white')
    
    # Business Module Boxes
    modules = [
        ('👥 HR\nMODULE', 2, 7, colors['hr'], ['Employee Mgmt', 'Payroll', 'Time Tracking', 'Performance']),
        ('💰 FINANCE\nMODULE', 6, 7, colors['finance'], ['Accounting', 'Invoicing', 'Budgeting', 'Reporting']),
        ('📦 INVENTORY\nMODULE', 10, 7, colors['inventory'], ['Stock Mgmt', 'Warehousing', 'Tracking', 'Orders']),
        ('🛒 SALES\nMODULE', 2, 5, colors['sales'], ['CRM', 'Orders', 'Customers', 'Analytics']),
        ('📞 MARKETING\nMODULE', 6, 5, colors['marketing'], ['Campaigns', 'Leads', 'Analytics', 'Automation']),
        ('🛒 PROCUREMENT\nMODULE', 10, 5, colors['procurement'], ['Purchasing', 'Vendors', 'Contracts', 'Approval']),
        ('⚙️ ADMIN\nMODULE', 4, 3, colors['admin'], ['User Mgmt', 'Roles', 'Tenants', 'Settings']),
        ('📊 REPORTS\nMODULE', 8, 3, colors['admin'], ['Dashboards', 'Analytics', 'Export', 'Scheduling'])
    ]
    
    for module_name, x, y, color, features in modules:
        # Main module box
        module_box = FancyBboxPatch((x-0.8, y-0.6), 1.6, 1.2, 
                                    boxstyle="round,pad=0.1", 
                                    facecolor=color, 
                                    edgecolor='black', 
                                    alpha=0.9)
        ax.add_patch(module_box)
        ax.text(x, y+0.2, module_name, ha='center', va='center', 
                fontsize=10, fontweight='bold', color='white')
        
        # Feature boxes
        for i, feature in enumerate(features):
            feature_box = FancyBboxPatch((x-0.7 + (i%2)*0.7, y-0.4 + (i//2)*0.3), 0.6, 0.25, 
                                         boxstyle="round,pad=0.02", 
                                         facecolor='white', 
                                         edgecolor=color, 
                                         alpha=0.8)
            ax.add_patch(feature_box)
            ax.text(x-0.4 + (i%2)*0.7, y-0.275 + (i//2)*0.3, feature, 
                    ha='center', va='center', fontsize=7, color=colors['text'])
        
        # Connection to shared layer
        ax.annotate('', xy=(x, 2.1), xytext=(x, y-0.6),
                    arrowprops=dict(arrowstyle='->', lw=1, color='gray', alpha=0.6))
    
    plt.tight_layout()
    plt.savefig('C:/Users/micha/erp-system/docs/modular-monolith-internal.png', 
                dpi=300, bbox_inches='tight', facecolor='white', edgecolor='none')
    print("✅ Modular monolith internal architecture saved as 'modular-monolith-internal.png'")
    
    plt.close()


if __name__ == "__main__":
    print("🎨 Generating comprehensive ERP architecture diagrams...")
    
    try:
        create_complete_architecture()
        create_deployment_flow_diagram() 
        create_module_architecture_diagram()
        print("\n✅ All architecture diagrams generated successfully!")
        print("\n📁 Generated files:")
        print("  • complete-production-architecture.png/pdf - Main production architecture")
        print("  • deployment-flow-diagram.png - Request flow and deployment")
        print("  • modular-monolith-internal.png - Internal module architecture")
        
    except Exception as e:
        print(f"❌ Error generating diagrams: {e}")
        print("Please ensure matplotlib is installed: pip install matplotlib")