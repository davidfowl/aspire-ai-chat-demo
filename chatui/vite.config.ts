import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import type { UserConfig, ServerOptions } from 'vite';

const port = process.env.PORT ? parseInt(process.env.PORT) : undefined;

export default defineConfig({
  plugins: [react()],
  server: {
    open: true,
    port,
    proxy: {
      '/api': {
        target: process.env.CHATAPI_HTTPS || process.env.CHATAPI_HTTP,
        changeOrigin: true,
      },
      '/api/chat/stream': {
        target: process.env.CHATAPI_HTTPS || process.env.CHATAPI_HTTP,
        ws: true,
        changeOrigin: true,
      }
    }
  }
} as UserConfig);
