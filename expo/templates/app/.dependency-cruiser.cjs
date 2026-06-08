module.exports = {
  forbidden: [
    {
      name: 'no-circular',
      severity: 'error',
      from: {},
      to: { circular: true }
    },
    {
      name: 'features-no-cross-import',
      severity: 'error',
      from: { path: '^src/features/([^/]+)/' },
      to: { path: '^src/features/', pathNot: '^src/features/$1/' }
    },
    {
      name: 'lib-no-upward-import',
      severity: 'error',
      from: { path: '^src/lib/' },
      to: { path: '^src/(app|features|components)/' }
    },
    {
      name: 'components-no-feature-import',
      severity: 'error',
      from: { path: '^src/components/' },
      to: { path: '^src/(app|features)/' }
    },
    {
      name: 'features-no-route-import',
      severity: 'error',
      from: { path: '^src/features/' },
      to: { path: '^src/app/' }
    },
  ],
  options: {
    doNotFollow: { path: 'node_modules' },
    tsConfig: { fileName: 'tsconfig.json' }
  }
};
