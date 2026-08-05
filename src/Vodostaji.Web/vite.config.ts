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
  // Namjerno **bez** `manualChunks`.
  //
  // Ranije je MapLibre bio guran u vlastiti chunk radi keširanja. To je oborilo mapu:
  // MapLibre parsira GeoJSON u Web Workeru i traži `maplibre-gl-worker.mjs` relativno u
  // odnosu na vlastiti chunk. Ručno grupisanje je pomjerilo kod a worker nije emitovan uz
  // njega, pa je stizao 404 — vektorski slojevi su ostajali prazni dok se raster podloga
  // uredno crtala. Mapa je izgledala kao da nigdje nema opasnosti.
  //
  // Vite ga ne može otkriti ni sam, jer MapLibre ime workera sklapa u vrijeme izvršavanja.
  // Zato ga uvozimo eksplicitno kroz `?worker&url` (vidi `src/lib/maplibre-worker.ts`), a
  // ovdje se traži ES format — MapLibre worker pravi kao modul.
  worker: { format: 'es' },
})
