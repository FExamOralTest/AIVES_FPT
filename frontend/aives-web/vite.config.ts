import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      // Forward API calls and uploaded media to the .NET backend in dev so browser sees same-origin requests
      proxy: {
        '/api': {
          target: env.VITE_API_PROXY_TARGET || 'https://localhost:7223',
          changeOrigin: true,
          secure: false,
        },
        '/uploads': {
          target: env.VITE_API_PROXY_TARGET || 'https://localhost:7223',
          changeOrigin: true,
          secure: false,
        },
      },
    },
  }
})
