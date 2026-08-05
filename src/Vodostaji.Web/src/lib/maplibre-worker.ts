import { setWorkerUrl } from 'maplibre-gl'
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'

/**
 * Govori MapLibreu gdje mu je worker.
 *
 * MapLibre parsira GeoJSON u Web Workeru, a ime tog fajla sklapa u vrijeme izvršavanja
 * (`new URL('./' + ime, ...)`), pa ga nijedan bundler ne može otkriti statički. Bez ovoga
 * worker vraća 404 i **vektorski slojevi ostaju prazni dok se raster podloga uredno crta** —
 * mapa izgleda kao mapa na kojoj nigdje nema opasnosti, bez ijedne greške u aplikaciji.
 *
 * `?worker&url` je bitan: worker nije samostalan, uvozi `maplibre-gl-shared.mjs`, pa mu
 * treba pakovanje sa zavisnostima a ne puko kopiranje fajla.
 *
 * Uvozi se prije nego se mapa napravi.
 */
setWorkerUrl(workerUrl)
