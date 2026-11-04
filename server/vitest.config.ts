import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    // Every worker gets an isolated database before importing db.ts.
    // Individual rebuild tests may still create their own explicit temporary files.
    env: { DB_PATH: ':memory:' },
    include: ['src/**/*.test.ts'],
    testTimeout: 15000,
  },
});
