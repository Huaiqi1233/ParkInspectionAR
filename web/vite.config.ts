import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Vite 配置：dev proxy 把 /api 转发到 Go 后端 :8080，
// 为什么用 proxy 而不是 CORS：原型阶段不改 Go 端，浏览器同源策略自然满足，
// 生产部署时由 nginx 反代或改 Go CORS（确认书决策：Vite proxy 绕 CORS）。
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
});
