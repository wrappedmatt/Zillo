import path from "path"
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    port: 57195,
    strictPort: true,
    host: true, // Listen on all addresses
    allowedHosts: [
      'localhost',
      '.ngrok-free.app', // Allow all ngrok domains
      '.ngrok.io', // Legacy ngrok domains
    ],
    proxy: {
      '/api': {
        target: 'https://localhost:7024',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
