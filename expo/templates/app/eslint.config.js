const tseslint = require('typescript-eslint');
const localRules = require('./eslint-rules');

module.exports = tseslint.config(
  {
    ignores: [
      'node_modules/**',
      '.expo/**',
      'coverage/**',
      'eslint-rules/**',
      'scripts/**',
      '**/*.config.js',
      'app.config.js',
      'jest.setup.js',
      '.dependency-cruiser.cjs'
    ]
  },
  ...tseslint.configs.recommended,
  {
    files: ['**/*.ts', '**/*.tsx'],
    linterOptions: { noInlineConfig: true },
    plugins: { local: localRules },
    rules: {
      'local/no-comments': 'error',
      'local/one-component-per-file': 'error',
      '@typescript-eslint/no-explicit-any': 'error',
      'max-lines-per-function': ['error', { max: 60, skipBlankLines: true }],
      'max-params': ['error', 4],
      'max-lines': ['error', { max: 300, skipBlankLines: true }]
    }
  },
  {
    files: ['**/__tests__/**'],
    rules: {
      'max-lines-per-function': 'off'
    }
  }
);
