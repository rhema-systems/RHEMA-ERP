module.exports = {
  extends: [
    'next/core-web-vitals',
    '@next/next/recommended'
  ],
  ignorePatterns: [
    'public/syncfusion/**',
    'scripts/copy-syncfusion-pdfviewer-assets.js'
  ],
  rules: {
    // Disable problematic rules for development
    '@typescript-eslint/no-unused-vars': 'off',
    '@typescript-eslint/no-explicit-any': 'off',
    '@typescript-eslint/no-empty-object-type': 'off',
    'react/no-unescaped-entities': 'off',
    '@next/next/no-img-element': 'off',
    'jsx-a11y/alt-text': 'off',
    'react-hooks/exhaustive-deps': 'off',
    
    // Keep important rules active
    '@typescript-eslint/no-non-null-assertion': 'error',
    'react/jsx-key': 'error',
    'react-hooks/rules-of-hooks': 'error',
  },
  parserOptions: {
    ecmaVersion: 2020,
    sourceType: 'module',
    ecmaFeatures: {
      jsx: true
    }
  },
  settings: {
    react: {
      version: 'detect'
    }
  }
};
