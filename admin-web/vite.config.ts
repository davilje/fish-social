import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// 生产由游戏服 :3001 托管在 /admin-web/，与运维平台同端口
export default defineConfig({
  plugins: [react()],
  base: '/admin-web/',
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_BASE ?? 'http://localhost:3001',
        changeOrigin: true,
      },
    },
  },
});
