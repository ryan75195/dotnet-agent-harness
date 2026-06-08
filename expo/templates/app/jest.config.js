module.exports = {
  preset: 'jest-expo',
  setupFiles: ['./jest.setup.js'],
  testTimeout: 15000,
  testMatch: ['**/__tests__/**/*.test.[jt]s?(x)'],
  collectCoverageFrom: [
    'src/lib/**/*.{ts,tsx}',
    'src/features/**/*.{ts,tsx}',
    '!**/__tests__/**'
  ],
  coverageThreshold: {
    global: {
      lines: 80,
      branches: 80
    }
  }
};
