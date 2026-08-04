import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    // API stoji odvojeno u razvoju; u produkciji isti origin.
    proxy: {
      '/api': { target: 'http://localhost:5188', changeOrigin: true },
    },
  },
  build: {
    // Aplikacija mora raditi na telefonu iz 2019. na 3G (UI.md §4). MapLibre je težak,
    // pa ide u zaseban chunk koji se keširaju odvojeno od našeg koda.
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('maplibre-gl')) return 'maplibre'
          return undefined
        },
      },
    },
  },
})
