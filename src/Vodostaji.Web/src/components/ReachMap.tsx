import { useEffect, useRef } from 'react'
import {
  Map as MapLibreMap,
  NavigationControl,
  type GeoJSONSource,
  type MapGeoJSONFeature,
  type StyleSpecification,
} from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import type { ReachCollection, ReachProperties } from '../api/types'
import { createHatchPattern } from '../lib/hatch'

const HATCH = 'hatch-no-data'

/**
 * Podloga. Tamna i prigušena, da boje statusa budu najsvjetlija stvar na ekranu (UI.md §6).
 *
 * CARTO dark_all je besplatan raster uz obaveznu atribuciju. Vanjski host, pa je ovo
 * odluka koju vrijedi preispitati prije produkcije — ali nije izvor podataka o vodostaju,
 * nego samo pozadina, i zamjenjuje se jednim objektom ispod.
 */
const BASEMAP: StyleSpecification = {
  version: 8,
  sources: {
    carto: {
      type: 'raster',
      tiles: ['https://basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png'],
      tileSize: 256,
      attribution: '© OpenStreetMap, © CARTO',
    },
  },
  layers: [
    { id: 'background', type: 'background', paint: { 'background-color': '#0d1117' } },
    { id: 'carto', type: 'raster', source: 'carto', paint: { 'raster-opacity': 0.55 } },
  ],
}

interface Props {
  data: ReachCollection | undefined
  onSelect: (properties: ReachProperties) => void
}

export function ReachMap({ data, onSelect }: Props) {
  const container = useRef<HTMLDivElement>(null)
  const map = useRef<MapLibreMap | null>(null)
  const onSelectRef = useRef(onSelect)
  onSelectRef.current = onSelect

  useEffect(() => {
    if (!container.current || map.current) return

    const instance = new MapLibreMap({
      container: container.current,
      style: BASEMAP,
      center: [17.8, 44.3],
      zoom: 6.6,
      attributionControl: { compact: true },
    })

    instance.addControl(new NavigationControl({ showCompass: false }), 'top-right')

    instance.on('load', () => {
      instance.addImage(HATCH, createHatchPattern(), { pixelRatio: 2 })

      instance.addSource('reaches', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
      })

      // Ispuna po boji iz renderera agencije. Boja stiže u podatku; ovdje se ne bira.
      instance.addLayer({
        id: 'reaches-fill',
        type: 'fill',
        source: 'reaches',
        filter: ['!=', ['get', 'level'], 'Unknown'],
        paint: {
          'fill-color': ['get', 'color'],
          // Stariji podatak je vidljivo bljeđi (UI.md §2).
          'fill-opacity': [
            'case',
            ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.45,
            ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.6,
            0.85,
          ],
        },
      })

      // Dionice bez podatka nose šrafuru, ne samo sivu ispunu. Siva sama može izgledati
      // kao "mirno"; šrafura ne može (UI.md §2).
      instance.addLayer({
        id: 'reaches-no-data',
        type: 'fill',
        source: 'reaches',
        filter: ['==', ['get', 'level'], 'Unknown'],
        paint: { 'fill-pattern': HATCH, 'fill-opacity': 0.75 },
      })

      // Puna ivica za podatak koji nije zastario.
      instance.addLayer({
        id: 'reaches-outline',
        type: 'line',
        source: 'reaches',
        filter: ['<=', ['coalesce', ['get', 'ageRatio'], 0], 3],
        paint: { 'line-color': '#0d1117', 'line-width': 0.6, 'line-opacity': 0.8 },
      })

      // Isprekidana ivica za zastario podatak (UI.md §2). `line-dasharray` ne prima
      // izraze po podatku, pa zastarjele dionice dobijaju vlastiti sloj.
      instance.addLayer({
        id: 'reaches-outline-stale',
        type: 'line',
        source: 'reaches',
        filter: ['>', ['coalesce', ['get', 'ageRatio'], 0], 3],
        paint: {
          'line-color': '#e6edf3',
          'line-width': 1.4,
          'line-opacity': 0.75,
          'line-dasharray': [2, 2],
        },
      })

      for (const layer of ['reaches-fill', 'reaches-no-data']) {
        instance.on('click', layer, (event) => {
          const feature = event.features?.[0] as MapGeoJSONFeature | undefined
          if (feature) onSelectRef.current(feature.properties as unknown as ReachProperties)
        })
        instance.on('mouseenter', layer, () => {
          instance.getCanvas().style.cursor = 'pointer'
        })
        instance.on('mouseleave', layer, () => {
          instance.getCanvas().style.cursor = ''
        })
      }
    })

    map.current = instance
    return () => {
      instance.remove()
      map.current = null
    }
  }, [])

  useEffect(() => {
    const instance = map.current
    if (!instance || !data) return

    const apply = () => {
      const source = instance.getSource('reaches') as GeoJSONSource | undefined
      source?.setData(data)
    }

    if (instance.isStyleLoaded()) apply()
    else instance.once('load', apply)
  }, [data])

  return (
    <div
      ref={container}
      className="h-full w-full"
      role="application"
      aria-label="Mapa stanja rijeka u Bosni i Hercegovini. Tabelarni pregled istih podataka je ispod mape."
    />
  )
}
