import { defineConfig, loadEnv } from 'vite';
import vue from '@vitejs/plugin-vue';
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const proxy = { '/api': { target: env.API_PROXY_TARGET || 'https://localhost:7225', changeOrigin: true, secure: false } };
  return { plugins: [vue()], server: { port: 5174, strictPort: true, proxy }, preview: { port: 4174, strictPort: true, proxy } };
});
