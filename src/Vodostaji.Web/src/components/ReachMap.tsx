import { useEffect, useRef, useState } from 'react'
import {
  Map as MapLibreMap,
  NavigationControl,
  type GeoJSONSource,
  type MapGeoJSONFeature,
  type StyleSpecification,
} from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
// Mora prije prve instance mape.
import '../lib/maplibre-worker'
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
 * CARTO dark_all je besplatan raster uz obaveznu atribuciju. Vanjski host, pa je ovo odluka
 * koju vrijedi preispitati prije produkcije — ali nije izvor podataka o vodostaju, nego samo
 * pozadina, i zamjenjuje se jednim objektom ispod.
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

/** Tačkasti izvori. Svaki dobija svoj sloj — nikad zajednički. */
export const POINT_SOURCES = ['avpjm', 'fhmzbih'] as const
export type PointSourceId = (typeof POINT_SOURCES)[number]

interface Props {
  data: ReachCollection | undefined
  points: Partial<Record<PointSourceId, ReachCollection>>
  stations: StationCollection | undefined
  showStations: boolean
  onSelect: (properties: ReachProperties) => void
  onSelectStation: (properties: StationProperties) => void
  /** Mapa koja ne uspije mora to **reći**. Tiha prazna mapa izgleda kao mapa bez opasnosti. */
  onError: (message: string) => void
}

export function ReachMap({
  data,
  points,
  stations,
  showStations,
  onSelect,
  onSelectStation,
  onError,
}: Props) {
  const container = useRef<HTMLDivElement>(null)
  const map = useRef<MapLibreMap | null>(null)

  const onSelectRef = useRef(onSelect)
  onSelectRef.current = onSelect
  const onSelectStationRef = useRef(onSelectStation)
  onSelectStationRef.current = onSelectStation
  const onErrorRef = useRef(onError)
  onErrorRef.current = onError

  /**
   * Slojevi se prave tek kad stil bude učitan, pa se podaci ne smiju primijeniti prije toga.
   *
   * Ranije je to bilo riješeno sa `else instance.once('load', apply)`, i to je bio bug:
   * `load` se emituje jednom, pa pretplata napravljena nakon njega čeka zauvijek. React
   * `StrictMode` je to i garantovao — montira, demontira, pa montira ponovo, a effecti za
   * podatke zavise samo od podataka, koji se pri tom nisu promijenili.
   *
   * Zastavica u stanju je deterministična: cleanup je gasi, nova mapa je pali, i svaki
   * effect se ponovo izvrši nad onom mapom koja je trenutno živa.
   */
  const [layersReady, setLayersReady] = useState(false)

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

    // Greška iz MapLibre runtimea ide na ekran, ne samo u konzolu.
    instance.on('error', (event) => {
      const message = (event as { error?: Error }).error?.message
      if (message) onErrorRef.current(message)
    })

    instance.on('load', () => {
      try {
        buildLayers(instance)
        setLayersReady(true)
      } catch (error) {
        // Bez ovoga jedan izuzetak ostavi mapu bez ijednog sloja, a to na ekranu izgleda
        // kao "nigdje nema opasnosti" — najgori mogući ishod.
        onErrorRef.current(
          `Slojevi mape nisu napravljeni: ${
            error instanceof Error ? error.message : String(error)
          }`,
        )
        return
      }

      for (const layer of ['reaches-fill', 'reaches-no-data', ...POINT_SOURCES.map(pointLayerId)]) {
        if (!instance.getLayer(layer)) continue

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

      if (instance.getLayer('stations-points')) {
        instance.on('click', 'stations-points', (event) => {
          const feature = event.features?.[0] as MapGeoJSONFeature | undefined
          if (feature) {
            onSelectStationRef.current(feature.properties as unknown as StationProperties)
          }
        })
        instance.on('mouseenter', 'stations-points', () => {
          instance.getCanvas().style.cursor = 'pointer'
        })
        instance.on('mouseleave', 'stations-points', () => {
          instance.getCanvas().style.cursor = ''
        })
      }
    })

    map.current = instance

    return () => {
      instance.remove()
      map.current = null
      setLayersReady(false)
    }
  }, [])

  useEffect(() => {
    if (!layersReady || !data) return
    ;(map.current?.getSource('reaches') as GeoJSONSource | undefined)?.setData(data)
  }, [layersReady, data])

  useEffect(() => {
    if (!layersReady) return

    for (const id of POINT_SOURCES) {
      const collection = points[id]
      if (!collection) continue
      ;(map.current?.getSource(pointSourceId(id)) as GeoJSONSource | undefined)?.setData(collection)
    }
  }, [layersReady, points])

  useEffect(() => {
    if (!layersReady || !stations) return
    ;(map.current?.getSource('stations') as GeoJSONSource | undefined)?.setData(stations)
  }, [layersReady, stations])

  useEffect(() => {
    const instance = map.current
    // `setLayoutProperty` baca ako sloja nema, a pad ovdje bi obrisao cijeli ekran.
    if (!layersReady || !instance?.getLayer('stations-points')) return

    instance.setLayoutProperty(
      'stations-points',
      'visibility',
      showStations ? 'visible' : 'none',
    )
  }, [layersReady, showStations])

  return (
    <div
      ref={container}
      className="h-full w-full"
      role="application"
      aria-label="Mapa stanja rijeka u Bosni i Hercegovini. Tabelarni pregled istih podataka je ispod mape."
    />
  )
}

/**
 * Pravi sve izvore i slojeve.
 *
 * Izdvojeno iz handlera da bi se moglo obuhvatiti jednim `try`. Ranije je izuzetak u bilo
 * kojem koraku prekidao ostatak i ostavljao mapu bez svega — uključujući boje opasnosti.
 */
const pointSourceId = (id: PointSourceId) => `points-${id}`
const pointLayerId = (id: PointSourceId) => `points-${id}-circles`

function buildLayers(instance: MapLibreMap) {
  // Šrafura je obavezna (UI.md §2), ali ako platno zakaže, mapa se ne smije izgubiti.
  // Rezerva zadržava obrazac: siva ispuna uz isprekidanu ivicu.
  let hatch = false
  try {
    instance.addImage(HATCH, createHatchPattern(), { pixelRatio: 2 })
    hatch = true
  } catch {
    hatch = false
  }

  instance.addSource('reaches', {
    type: 'geojson',
    data: { type: 'FeatureCollection', features: [] },
  })

  // Ispuna je namjerno **slaba**, a granica jaka.
  //
  // Ovi poligoni nisu poplavljeno područje nego dionice — u prosjeku 339 km², najveća 1041
  // km², ukupno trećina BiH. Jedno očitanje na jednoj letvi opisuje cijelu tu površinu.
  // Puna ispuna u crvenom preko 769 km² čita se kao "sve ovo je pod vodom", a znači "rijeka
  // je na jednom mjerilu prešla prag". Razlika je nečija odluka o evakuaciji.
  //
  // Ispuna kaže "ovdje je voda". Obris kaže "na ovo područje se ocjena odnosi". Zadržavamo
  // boju agencije i njen doslovni natpis; mijenja se samo koliko glasno ispuna govori.
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
        ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.08,
        ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.13,
        0.18,
      ],
    },
  })

  // Granica dionice — nosi boju punom jačinom. Ovo je element koji oko treba da uhvati.
  instance.addLayer({
    id: 'reaches-edge',
    type: 'line',
    source: 'reaches',
    filter: ['!=', ['get', 'level'], 'Unknown'],
    paint: {
      'line-color': ['get', 'color'],
      'line-width': ['interpolate', ['linear'], ['zoom'], 6, 1.6, 10, 3],
      'line-opacity': [
        'case',
        ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.5,
        ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.75,
        0.95,
      ],
    },
  })

  // Dionice bez podatka nose šrafuru, ne samo sivu ispunu. Siva sama može izgledati kao
  // "mirno"; šrafura ne može (UI.md §2).
  instance.addLayer({
    id: 'reaches-no-data',
    type: 'fill',
    source: 'reaches',
    filter: ['==', ['get', 'level'], 'Unknown'],
    paint: hatch
      ? { 'fill-pattern': HATCH, 'fill-opacity': 0.35 }
      : { 'fill-color': '#cccccc', 'fill-opacity': 0.25 },
  })

  if (!hatch) {
    // Kad šrafure nema, obrazac nosi ivica — boja ne smije ostati jedini nosilac (UI.md §5).
    instance.addLayer({
      id: 'reaches-no-data-outline',
      type: 'line',
      source: 'reaches',
      filter: ['==', ['get', 'level'], 'Unknown'],
      paint: { 'line-color': '#5c6470', 'line-width': 1.2, 'line-dasharray': [3, 2] },
    })
  }

  // Isprekidana ivica za zastario podatak (UI.md §2). `line-dasharray` ne prima izraze po
  // podatku, pa zastarjele dionice dobijaju vlastiti sloj.
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

  // Tačkasti izvori — **jedan sloj po agenciji**, nikad zajednički i nikad stopljen sa
  // dionicama (CLAUDE.md → Šta NE raditi).
  //
  // Agencije se razlikuju **oblikom, ne samo bojom**. Prva verzija ih je razlikovala plavom
  // i tirkiznom; izmjereno je da im je odnos svjetline 1.18:1, pa su oku praktično iste — a
  // daltonisti sasvim. UI.md §5 ionako traži da boja nikad ne bude jedini nosilac. Zato
  // jedna agencija ima puni krug sa tamnom ivicom, druga krug sa **svijetlim prstenom**:
  // razlika koja preživi i crno-bijeli ekran.
  const ring: Record<PointSourceId, { stroke: string; width: number; radius: number }> = {
    avpjm: { stroke: '#0d1117', width: 1, radius: 0 },
    fhmzbih: { stroke: '#e6edf3', width: 2.5, radius: 1 },
  }

  for (const id of POINT_SOURCES) {
    instance.addSource(pointSourceId(id), {
      type: 'geojson',
      data: { type: 'FeatureCollection', features: [] },
    })

    instance.addLayer({
      id: pointLayerId(id),
      type: 'circle',
      source: pointSourceId(id),
      paint: {
        'circle-radius': [
          'interpolate', ['linear'], ['zoom'],
          6, 4 + ring[id].radius,
          10, 8 + ring[id].radius,
        ],
        'circle-color': ['get', 'color'],
        'circle-stroke-color': ring[id].stroke,
        'circle-stroke-width': ring[id].width,
        'circle-opacity': [
          'case',
          ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.45,
          ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.6,
          0.9,
        ],
      },
    })
  }

  instance.addSource('stations', {
    type: 'geojson',
    data: { type: 'FeatureCollection', features: [] },
  })

  // Mjerna mjesta, ne stanja.
  //
  // Namjerno bez ispune u boji. Registar kaže gdje se mjeri, a stanje na tim mjestima nemamo
  // — `HYDRO_ID` ne povezuje dionice sa ovim registrom (SOURCES.md §1.7). Ispunjen krug bilo
  // koje neutralne boje čitao bi se kao "ovdje je sve u redu", što je tačno ono što zlatno
  // pravilo 1 zabranjuje. Prazan prsten ne tvrdi ništa.
  instance.addLayer({
    id: 'stations-points',
    type: 'circle',
    source: 'stations',
    layout: { visibility: 'none' },
    paint: {
      'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 3, 10, 6],
      'circle-color': 'rgba(0,0,0,0)',
      'circle-stroke-color': '#e6edf3',
      'circle-stroke-width': 1.5,
    },
  })
}
