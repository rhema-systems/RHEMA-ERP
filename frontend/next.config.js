/** @type {import('next').NextConfig} */
const nextConfig = {
  // Enable standalone output for Docker containers
  output: 'standalone',
  
  // ESLint configuration
  eslint: {
    // Warning: This allows production builds to successfully complete even if
    // your project has ESLint errors.
    ignoreDuringBuilds: true,
  },
  
  // TypeScript configuration
  typescript: {
    // Warning: This allows production builds to successfully complete even if
    // your project has TypeScript errors.
    ignoreBuildErrors: true,
  },
  
  // Server external packages (moved from experimental)
  serverExternalPackages: [],
  
  // Experimental features
  experimental: {
    // Add any experimental features here if needed
  },
  
  // Environment variables
  env: {
    CUSTOM_KEY: process.env.CUSTOM_KEY,
  },
  
  // Images configuration for external domains
  images: {
    domains: [],
    unoptimized: false,
  },
  
  // Webpack configuration
  webpack: (config, { buildId, dev, isServer, defaultLoaders, webpack }) => {
    return config;
  },
  
  // Headers configuration
  async headers() {
    return [
      {
        source: '/api/(.*)',
        headers: [
          {
            key: 'Access-Control-Allow-Origin',
            value: '*',
          },
          {
            key: 'Access-Control-Allow-Methods',
            value: 'GET, POST, PUT, DELETE, OPTIONS',
          },
          {
            key: 'Access-Control-Allow-Headers',
            value: 'Content-Type, Authorization',
          },
        ],
      },
    ];
  },
  
  // Redirects configuration
  async redirects() {
    return [
      // Add any redirects here if needed
    ];
  },
  
  // Rewrites configuration
  async rewrites() {
    return [
      // Add any rewrites here if needed
    ];
  },
};

module.exports = nextConfig;
