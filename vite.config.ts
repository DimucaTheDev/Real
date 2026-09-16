import { defineConfig } from 'vite';

export default defineConfig({
  root: 'dist',
  server: {
    port: 3000,
    host: '0.0.0.0',
    strictPort: true,
  },
  preview: {
    port: 3000,
    host: '0.0.0.0',
    strictPort: true,
  },
});
