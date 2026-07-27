import { dirname } from "path";
import { fileURLToPath } from "url";
import { FlatCompat } from "@eslint/eslintrc";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  {
    ignores: [
      "node_modules/**",
      ".next/**",
      "out/**",
      "build/**",
      "public/syncfusion/**",
      "scripts/copy-syncfusion-pdfviewer-assets.js",
      "next-env.d.ts",
    ],
  },
  {
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
  },
];

export default eslintConfig;
