import { useEffect, useRef } from 'react'
import {
  Map as MapLibreMap,
  NavigationControl,
  type GeoJSONSource,
  type MapGeoJSONFeature,
  type StyleSpecification,
} from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import type {
  ReachCollection,
  ReachProperties,
  StationCollection,
  StationProperties,
} from '../api/types'
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
  avpjm: ReachCollection | undefined
  stations: StationCollection | undefined
  showStations: boolean
  onSelect: (properties: ReachProperties) => void
  onSelectStation: (properties: StationProperties) => void
}

export function ReachMap({
  data,
  avpjm,
  stations,
  showStations,
  onSelect,
  onSelectStation,
}: Props) {
  const container = useRef<HTMLDivElement>(null)
  const map = useRef<MapLibreMap | null>(null)
  const onSelectRef = useRef(onSelect)
  onSelectRef.current = onSelect
  const onSelectStationRef = useRef(onSelectStation)
  onSelectStationRef.current = onSelectStation

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

      // Jadranski sliv — **zaseban izvor i zaseban sloj**. Nikad stopljen sa dionicama:
      // AVP Sava daje ocjenu opasnosti, AVPJM je ne daje, i jedna legenda za oboje bi
      // morala izmisliti nešto za jednu od agencija (CLAUDE.md → Šta NE raditi).
      instance.addSource('avpjm', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
      })

      instance.addLayer({
        id: 'avpjm-points',
        type: 'circle',
        source: 'avpjm',
        paint: {
          'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 4, 10, 8],
          'circle-color': ['get', 'color'],
          'circle-stroke-color': '#0d1117',
          'circle-stroke-width': 1,
          'circle-opacity': [
            'case',
            ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.45,
            ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.6,
            0.9,
          ],
        },
      })

      instance.on('click', 'avpjm-points', (event) => {
        const feature = event.features?.[0] as MapGeoJSONFeature | undefined
        if (feature) onSelectRef.current(feature.properties as unknown as ReachProperties)
      })
      instance.on('mouseenter', 'avpjm-points', () => {
        instance.getCanvas().style.cursor = 'pointer'
      })
      instance.on('mouseleave', 'avpjm-points', () => {
        instance.getCanvas().style.cursor = ''
      })

      instance.addSource('stations', {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
      })

      // Mjerna mjesta, ne stanja.
      //
      // Namjerno bez ispune u boji. Registar kaže gdje se mjeri, a stanje na tim mjestima
      // nemamo — `HYDRO_ID` ne povezuje dionice sa ovim registrom (SOURCES.md §1.7).
      // Ispunjen krug bilo koje neutralne boje čitao bi se kao "ovdje je sve u redu", što je
      // tačno ono što zlatno pravilo 1 zabranjuje. Prazan prsten ne tvrdi ništa.
      instance.addLayer({
        id: 'stations-points',
        type: 'circle',
        source: 'stations',
        layout: { visibility: 'none' },
        paint: {
          'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 3, 10, 6],
          'circle-color': 'transparent',
          'circle-stroke-color': '#e6edf3',
          'circle-stroke-width': 1.5,
          'circle-opacity': 1,
        },
      })

      instance.on('click', 'stations-points', (event) => {
        const feature = event.features?.[0] as MapGeoJSONFeature | undefined
        if (feature) onSelectStationRef.current(feature.properties as unknown as StationProperties)
      })
      instance.on('mouseenter', 'stations-points', () => {
        instance.getCanvas().style.cursor = 'pointer'
      })
      instance.on('mouseleave', 'stations-points', () => {
        instance.getCanvas().style.cursor = ''
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

  useEffect(() => {
    const instance = map.current
    if (!instance || !avpjm) return

    const apply = () => {
      const source = instance.getSource('avpjm') as GeoJSONSource | undefined
      source?.setData(avpjm)
    }

    if (instance.isStyleLoaded()) apply()
    else instance.once('load', apply)
  }, [avpjm])

  useEffect(() => {
    const instance = map.current
    if (!instance || !stations) return

    const apply = () => {
      const source = instance.getSource('stations') as GeoJSONSource | undefined
      source?.setData(stations)
    }

    if (instance.isStyleLoaded()) apply()
    else instance.once('load', apply)
  }, [stations])

  useEffect(() => {
    const instance = map.current
    if (!instance) return

    const apply = () => {
      if (!instance.getLayer('stations-points')) return
      instance.setLayoutProperty(
        'stations-points',
        'visibility',
        showStations ? 'visible' : 'none',
      )
    }

    if (instance.isStyleLoaded()) apply()
    else instance.once('load', apply)
  }, [showStations])

  return (
    <div
      ref={container}
      className="h-full w-full"
      role="application"
      aria-label="Mapa stanja rijeka u Bosni i Hercegovini. Tabelarni pregled istih podataka je ispod mape."
    />
  )
}
