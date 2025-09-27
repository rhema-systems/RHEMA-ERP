# 🔑 GitHub Secrets Setup Guide

## Overview
Your GitHub Actions workflows are designed to work with or without secrets. Setting up secrets enables rich notifications across multiple channels.

## 🔔 Notification Secrets (All Optional)

### Slack Integration
- **`SLACK_WEBHOOK`** - Slack webhook URL for notifications
  - Get from: Slack App → Incoming Webhooks
  - Format: `https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX`

### Microsoft Teams Integration  
- **`TEAMS_WEBHOOK`** - Teams webhook URL
  - Get from: Teams Channel → Connectors → Incoming Webhook
  - Format: `https://outlook.office.com/webhook/...`

### Discord Integration
- **`DISCORD_WEBHOOK`** - Discord webhook URL
  - Get from: Discord Server → Channel Settings → Integrations → Webhooks
  - Format: `https://discord.com/api/webhooks/...`

### Email Notifications
- **`EMAIL_HOST`** - SMTP server hostname (e.g., `smtp.gmail.com`)
- **`EMAIL_PORT`** - SMTP port (usually `587` or `465`)
- **`EMAIL_USERNAME`** - SMTP username
- **`EMAIL_PASSWORD`** - SMTP password or app password
- **`EMAIL_FROM`** - From email address
- **`NOTIFICATION_EMAIL`** - Recipient email address

## 🚢 Deployment Secrets (For Production)

### Production Environment Protection
- **`PRODUCTION_APPROVERS`** - GitHub usernames who can approve production deployments
  - Format: `username1,username2,username3`
  - Example: `micha,admin,devops-lead`

### Database Secrets (If using external databases)
- **`PROD_DB_CONNECTION_STRING`** - Production database connection
- **`STAGING_DB_CONNECTION_STRING`** - Staging database connection
- **`PROD_DB_PASSWORD`** - Production database password

### Server Access (If deploying to external servers)
- **`PROD_HOST`** - Production server hostname
- **`PROD_USER`** - SSH username for production server
- **`PROD_SSH_KEY`** - Private SSH key for server access
- **`PROD_PORT`** - SSH port (default: 22)

## 📋 Setup Priority

### 🥇 High Priority (Recommended)
1. **`PRODUCTION_APPROVERS`** - Essential for production safety
2. **`SLACK_WEBHOOK`** or **`TEAMS_WEBHOOK`** - For team notifications

### 🥈 Medium Priority (Nice to have)
1. **Email notifications** - For automated reporting
2. **`DISCORD_WEBHOOK`** - If your team uses Discord

### 🥉 Low Priority (Advanced)
1. Database connection strings (if using external DBs)
2. Server SSH keys (if using external servers)

## 🚀 Quick Start: Minimum Setup

For basic functionality, you only need:

1. **`PRODUCTION_APPROVERS`** (if you plan to deploy to production)
   ```
   your-github-username
   ```

2. **One notification channel** (choose your preferred):
   - Slack webhook, or
   - Teams webhook, or  
   - Email settings

## 🔧 Testing Your Setup

After adding secrets, test with:
1. Make a small change to your code
2. Push to master branch
3. Watch the Actions tab for workflow execution
4. Check your notification channels

## ⚠️ Important Notes

- **Secrets are encrypted** - GitHub encrypts all secrets
- **Not required** - All workflows work without secrets
- **Environment-specific** - You can set different secrets for different environments
- **Team access** - Only repository admins can view/edit secrets

## 🆘 If You Don't Want Notifications

If you prefer no notifications, you don't need to set up any secrets. The workflows will:
- ✅ Build and test your code
- ✅ Run security scans  
- ✅ Generate documentation
- ✅ Create deployments
- ⚪ Skip notification steps (no errors)

## 🔍 Checking Current Status

You can see which secrets are configured in:
**Repository Settings → Secrets and variables → Actions**

The secret names will be listed (values are hidden for security).