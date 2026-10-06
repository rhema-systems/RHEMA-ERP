/** @type {import('next').NextConfig} */
const isStandaloneBuild = process.env.NEXT_OUTPUT === 'standalone';

const nextConfig = {
  // Keep production compilation isolated from `next dev`. Both commands mutate
  // their output directory, so sharing `.next` can produce missing page modules.
  // An explicit directory still wins for parallel or isolated verification runs.
  distDir: process.env.NEXT_DIST_DIR || (process.env.NODE_ENV === 'development' ? '.next-dev' : '.next'),
  // `next start` is used for local UAT and needs the regular .next output. Docker opts into
  // standalone explicitly through NEXT_OUTPUT=standalone (see Dockerfile).
  output: isStandaloneBuild ? 'standalone' : undefined,
  
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
    // Reduces peak Webpack memory while compiling the large App Router route tree.
    webpackMemoryOptimizations: true,
  },
  
  // Turbopack configuration
  turbopack: {
    root: __dirname,
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
    if (!dev && !isServer) {
      // Build-only dependency: published next start packages omit build scripts.
      const { preservePdfiumWorker } = require('./scripts/preserve-pdfium-worker');
      preservePdfiumWorker(config);
    }
    if (!dev && !isServer && process.env.NEXT_DISABLE_CLIENT_MINIFY === 'true') {
      config.optimization.minimize = false;
    }

    return config;
  },
  
  // Headers configuration
  async headers() {
    return [
      {
        source: '/sw.js',
        headers: [
          {
            key: 'Cache-Control',
            value: 'no-store, no-cache, must-revalidate, max-age=0',
          },
          {
            key: 'Pragma',
            value: 'no-cache',
          },
          {
            key: 'Expires',
            value: '0',
          },
          {
            key: 'Service-Worker-Allowed',
            value: '/',
          },
        ],
      },
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
