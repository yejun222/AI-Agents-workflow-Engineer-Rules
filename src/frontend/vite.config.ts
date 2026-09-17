/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  server: {
    port: 5173,
    // 开发态同源直连后端，避免 CORS 与 Cookie 跨站问题（生产由网关同源代理）
    proxy: {
      '/api': {
        target: 'http://localhost:5180',
        changeOrigin: true
      }
    }
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/__tests__/**/*.spec.ts'],
    // 无匹配用例时必须失败：防止测试目录被误删/挪错后 CI 假绿（同 dotnet format 在 .slnx 上静默退出码 0 的坑）
    passWithNoTests: false
  }
})
