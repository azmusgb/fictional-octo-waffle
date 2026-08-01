import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5087',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: mode === 'hosted' ? 'dist' : '../server/WorkbenchStudio.Api/wwwroot',
    emptyOutDir: true,
    sourcemap: true,
  },
}));
