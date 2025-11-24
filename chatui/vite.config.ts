import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import type { UserConfig } from 'vite';

const target = process.env.CHATAPI_HTTPS || process.env.CHATAPI_HTTP;

export default defineConfig({
  plugins: [react()],
  server: {
    open: true,
    proxy: {
      '/api': {
        target,
        changeOrigin: true,
      },
      '/api/chat/stream': {
        target,
        ws: true,
        changeOrigin: true,
      }
    }
  }
} as UserConfig);
