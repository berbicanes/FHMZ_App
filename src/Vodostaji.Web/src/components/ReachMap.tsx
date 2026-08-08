import { useEffect, useRef, useState } from 'react'
import {
  LngLatBounds,
  Map as MapLibreMap,
  NavigationControl,
  Popup,
  ScaleControl,
  type ExpressionSpecification,
  type FilterSpecification,
  type GeoJSONSource,
  type MapGeoJSONFeature,
  type StyleSpecification,
} from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
// Mora prije prve instance mape.
import '../lib/maplibre-worker'
import type { Geometry } from 'geojson'
import type {
  ReachCollection,
  ReachProperties,
  StationCollection,
  StationProperties,
} from '../api/types'
import { createHatchPattern } from '../lib/hatch'
import type { BucketKey } from '../lib/levels'

const HATCH = 'hatch-no-data'

/** Pogled na cijelu BiH. Povratna tačka kad se detalj zatvori. */
const HOME = { center: [17.75, 44.17] as [number, number], zoom: 6.85 }

/**
 * Granice pomjeranja. Mapa jedne države koja se da odvući na Atlantik izgleda kao
 * nedovršen embed; ograničena izgleda kao proizvod.
 */
const MAX_BOUNDS: [[number, number], [number, number]] = [
  [14.4, 41.5],
  [21.1, 46.4],
]

/**
 * Podloga u tri sloja umjesto jednog.
 *
 * Ranije je ovo bio jedan `dark_all` raster na 55% providnosti — a u `dark_all` su imena
 * gradova **utisnuta u sliku**. To znači da su naši podaci nužno crtani *preko* Sarajeva i
 * Bihaća, pa su imena mjesta nestajala tačno tamo gdje su najpotrebnija: ispod obojene
 * dionice. Na 55% je uz to sve izgledalo isprano.
 *
 * Sada je podloga razdvojena na `dark_nolabels` (teren, ispod svega) i `dark_only_labels`
 * (samo imena, iznad naših površina a ispod naših tačaka). To je standardni kartografski
 * sendvič: teren → podaci → imena. Raster je uz to prigušen kroz `saturation`/`brightness`
 * umjesto kroz providnost, pa ostaje oštar a ne siv.
 */
function basemapStyle(): StyleSpecification {
  // Retina pločice su četiri puta veće u bajtovima. Na ekranu koji ih ne može prikazati to
  // je čista šteta za nekoga na 3G (UI.md §4), pa se traže samo kad imaju smisla.
  const suffix =
    typeof window !== 'undefined' && window.devicePixelRatio > 1.25 ? '@2x' : ''

  return {
    version: 8,
    glyphs: '/fonts/{fontstack}/{range}.pbf',
    sources: {
      'carto-base': {
        type: 'raster',
        tiles: [`https://basemaps.cartocdn.com/dark_nolabels/{z}/{x}/{y}${suffix}.png`],
        tileSize: 256,
        attribution: '© OpenStreetMap, © CARTO',
      },
      'carto-labels': {
        type: 'raster',
        tiles: [`https://basemaps.cartocdn.com/dark_only_labels/{z}/{x}/{y}${suffix}.png`],
        tileSize: 256,
      },
    },
    layers: [
      { id: 'background', type: 'background', paint: { 'background-color': '#05070b' } },
      {
        id: 'carto-base',
        type: 'raster',
        source: 'carto-base',
        paint: {
          'raster-opacity': 0.92,
          // Prigušenje ide kroz zasićenje i svjetlinu, ne kroz providnost — tako podloga
          // ostaje oštra, a boje statusa i dalje najsvjetlije na ekranu (UI.md §6).
          'raster-saturation': -0.3,
          'raster-contrast': 0.08,
          'raster-brightness-max': 0.78,
        },
      },
    ],
  }
}

/** Tačkasti izvori. Svaki dobija svoj sloj — nikad zajednički. */
export const POINT_SOURCES = ['avpjm', 'fhmzbih'] as const
export type PointSourceId = (typeof POINT_SOURCES)[number]

interface Props {
  data: ReachCollection | undefined
  points: Partial<Record<PointSourceId, ReachCollection>>
  stations: StationCollection | undefined
  showStations: boolean
  /** Imena nad dionicama i tačkama. Isključivo na zahtjev — gusta mapa se teško čita. */
  showLabels: boolean
  /** Prikazane grupe stanja. `null` znači sve; prazan skup bi značio praznu mapu, pa ga nema. */
  bucketFilter: BucketKey[] | null
  /** Šta je trenutno otvoreno — mapa to ističe i doleti do toga. */
  selected: { sourceId: string; key: string } | null
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
  showLabels,
  bucketFilter,
  selected,
  onSelect,
  onSelectStation,
  onError,
}: Props) {
  const container = useRef<HTMLDivElement>(null)
  const map = useRef<MapLibreMap | null>(null)
  const popup = useRef<Popup | null>(null)
  /** Pamti prethodni izbor da bi se povratak na pregled desio samo kad se detalj zatvori. */
  const hadSelection = useRef(false)

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
      style: basemapStyle(),
      center: HOME.center,
      zoom: HOME.zoom,
      minZoom: 5.8,
      maxZoom: 14,
      maxBounds: MAX_BOUNDS,
      attributionControl: { compact: true },
    })

    instance.addControl(new NavigationControl({ showCompass: false }), 'top-right')
    // Razmjernik: bez njega se ne zna da li je jedna dionica duga 5 ili 50 km, a te
    // površine su u prosjeku 339 km² — pogrešna predstava o veličini mijenja čitanje.
    instance.addControl(new ScaleControl({ maxWidth: 96, unit: 'metric' }), 'bottom-right')

    const hover = new Popup({
      closeButton: false,
      closeOnClick: false,
      offset: 14,
      maxWidth: '260px',
    })
    popup.current = hover

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

      const reachLayers = [
        'reaches-fill',
        'reaches-no-data',
        ...POINT_SOURCES.map(pointLayerId),
      ]

      for (const layer of reachLayers) {
        if (!instance.getLayer(layer)) continue

        instance.on('click', layer, (event) => {
          const feature = event.features?.[0] as MapGeoJSONFeature | undefined
          if (feature) onSelectRef.current(feature.properties as unknown as ReachProperties)
        })

        // Pregled na prelazak mišem. Bez ovoga se do imena i vrijednosti dolazi tek klikom,
        // pa se mapa čita kao slika u bojama umjesto kao spisak mjerenja.
        instance.on('mousemove', layer, (event) => {
          const feature = event.features?.[0] as MapGeoJSONFeature | undefined
          if (!feature) return
          instance.getCanvas().style.cursor = 'pointer'
          hover
            .setLngLat(event.lngLat)
            .setHTML(reachPopupHtml(feature.properties as unknown as ReachProperties))
            .addTo(instance)
        })

        instance.on('mouseleave', layer, () => {
          instance.getCanvas().style.cursor = ''
          hover.remove()
        })
      }

      if (instance.getLayer('stations-points')) {
        instance.on('click', 'stations-points', (event) => {
          const feature = event.features?.[0] as MapGeoJSONFeature | undefined
          if (feature) {
            onSelectStationRef.current(feature.properties as unknown as StationProperties)
          }
        })
        instance.on('mousemove', 'stations-points', (event) => {
          const feature = event.features?.[0] as MapGeoJSONFeature | undefined
          if (!feature) return
          instance.getCanvas().style.cursor = 'pointer'
          hover
            .setLngLat(event.lngLat)
            .setHTML(stationPopupHtml(feature.properties as unknown as StationProperties))
            .addTo(instance)
        })
        instance.on('mouseleave', 'stations-points', () => {
          instance.getCanvas().style.cursor = ''
          hover.remove()
        })
      }
    })

    map.current = instance

    return () => {
      hover.remove()
      popup.current = null
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

  // Filter po stupnju. Skriva se **samo prikaz**; podaci i tabela ostaju netaknuti, pa se
  // filtriranjem ne može doći u stanje u kojem korisnik misli da je nešto nestalo.
  useEffect(() => {
    const instance = map.current
    if (!layersReady || !instance) return

    for (const [id, base] of Object.entries(BASE_FILTERS)) {
      if (!instance.getLayer(id)) continue
      instance.setFilter(id, combineFilters(base, bucketFilter))
    }
  }, [layersReady, bucketFilter])

  // Isticanje odabranog i let do njega.
  //
  // Bez ovoga korisnik koji klikne red u tabeli ne vidi gdje je to na mapi, a onaj ko
  // otvori podijeljen link gleda cijelu državu umjesto svoje rijeke.
  useEffect(() => {
    const instance = map.current
    if (!layersReady || !instance) return

    const match: FilterSpecification = selected
      ? [
          'all',
          ['==', ['get', 'sourceId'], selected.sourceId],
          ['==', ['get', 'stationKey'], selected.key],
        ]
      : NOTHING

    for (const id of [
      'reaches-selected',
      'reaches-selected-glow',
      ...POINT_SOURCES.map(selectedPointLayerId),
    ]) {
      if (instance.getLayer(id)) instance.setFilter(id, match)
    }

    if (!selected) {
      // Povratak na pregled cijele države, ali samo ako je nešto **bilo** otvoreno. Bez te
      // provjere bi svako prvo učitavanje počinjalo suvišnim pomjeranjem mape.
      if (hadSelection.current) {
        instance.easeTo({
          center: HOME.center,
          zoom: HOME.zoom,
          duration: prefersReducedMotion() ? 0 : 600,
        })
        hadSelection.current = false
      }
      return
    }

    hadSelection.current = true

    const collection =
      selected.sourceId === 'avp-sava' ? data : points[selected.sourceId as PointSourceId]

    const feature = collection?.features.find((f) => f.properties.stationKey === selected.key)
    if (!feature?.geometry) return

    const bounds = new LngLatBounds()
    let count = 0
    walk(feature.geometry, (lon, lat) => {
      bounds.extend([lon, lat])
      count++
    })
    if (count === 0) return

    instance.fitBounds(bounds, {
      padding: { top: 110, right: 80, bottom: 110, left: 80 },
      maxZoom: 10.5,
      duration: prefersReducedMotion() ? 0 : 700,
    })
  }, [layersReady, selected, data, points])

  useEffect(() => {
    const instance = map.current
    // `setLayoutProperty` baca ako sloja nema, a pad ovdje bi obrisao cijeli ekran.
    if (!layersReady || !instance) return

    for (const id of ['stations-points', 'stations-labels']) {
      if (instance.getLayer(id)) {
        instance.setLayoutProperty(id, 'visibility', showStations ? 'visible' : 'none')
      }
    }
  }, [layersReady, showStations])

  useEffect(() => {
    const instance = map.current
    if (!layersReady || !instance) return

    for (const id of ['reaches-labels', ...POINT_SOURCES.map(pointLabelId)]) {
      if (instance.getLayer(id)) {
        instance.setLayoutProperty(id, 'visibility', showLabels ? 'visible' : 'none')
      }
    }
  }, [layersReady, showLabels])

  return (
    <div
      ref={container}
      className="h-full w-full"
      role="application"
      aria-label="Mapa stanja rijeka u Bosni i Hercegovini. Tabelarni pregled istih podataka je u ploči sa strane."
    />
  )
}

const pointSourceId = (id: PointSourceId) => `points-${id}`
const pointLayerId = (id: PointSourceId) => `points-${id}-circles`
const pointLabelId = (id: PointSourceId) => `points-${id}-labels`
const selectedPointLayerId = (id: PointSourceId) => `points-${id}-selected`

const NOTHING: FilterSpecification = ['==', ['get', 'stationKey'], ' nema']

const HAS_LEVEL: ExpressionSpecification = ['!=', ['get', 'level'], 'Unknown']
const NO_LEVEL: ExpressionSpecification = ['==', ['get', 'level'], 'Unknown']

/**
 * Osnovni filter svakog sloja, odvojen od korisničkog filtera po stupnju.
 *
 * Bez ovog registra bi filtriranje po stupnju pregazilo pravilo da dionice bez podatka idu
 * u vlastiti šrafirani sloj — a stopiti ih sa ostalima znači prikazati `Unknown` u boji
 * nekog stanja, što je zlatno pravilo 1.
 */
const BASE_FILTERS: Record<string, ExpressionSpecification | null> = {
  'reaches-fill': HAS_LEVEL,
  'reaches-edge': HAS_LEVEL,
  'reaches-glow': HAS_LEVEL,
  'reaches-no-data': NO_LEVEL,
  'reaches-no-data-outline': NO_LEVEL,
  'reaches-labels': null,
  'reaches-outline-stale': ['>', ['coalesce', ['get', 'ageRatio'], 0], 3] as ExpressionSpecification,
  ...Object.fromEntries(
    POINT_SOURCES.flatMap((id) => [
      [pointLayerId(id), null],
      [`${pointLayerId(id)}-halo`, null],
      [pointLabelId(id), null],
    ]),
  ),
}

/**
 * Ima li obiljezje mjerenje. `valueCm` moze biti `null`, a `has` u MapLibre izrazima na
 * `null` i dalje vraca `true` — pa provjera ide preko `coalesce` sa nemogucom vrijednoscu.
 */
const IS_MEASURED: ExpressionSpecification = [
  '>',
  ['coalesce', ['get', 'valueCm'], -1e9],
  -1e8,
]

/**
 * Izraz za jednu grupu stanja.
 *
 * `Measured` i `NoData` se **ne daju izraziti samo preko `level`** — oba su `Unknown`.
 * Razlikuje ih postojanje vrijednosti, isto kao u `levels.ts`; da filter to ne poštuje,
 * gasenje grupe „Nema podatka" bi sa mape uklonilo i 32 izmjerene stanice.
 */
function bucketExpression(key: BucketKey): ExpressionSpecification {
  if (key === 'Measured') return ['all', NO_LEVEL, IS_MEASURED]
  if (key === 'NoData') return ['all', NO_LEVEL, ['!', IS_MEASURED]]
  return ['==', ['get', 'level'], key]
}

function combineFilters(
  base: ExpressionSpecification | null,
  buckets: BucketKey[] | null,
): FilterSpecification | null {
  const byBucket: ExpressionSpecification | null =
    buckets && buckets.length > 0
      ? (['any', ...buckets.map(bucketExpression)] as ExpressionSpecification)
      : null

  if (base && byBucket) return ['all', base, byBucket]
  // `null` uklanja filter — to je MapLibre nacin da se kaze "propusti sve".
  return base ?? byBucket
}

/** Poštuje `prefers-reduced-motion` (UI.md §5) — let je udobnost, ne informacija. */
function prefersReducedMotion() {
  return (
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  )
}

/** Prolazi kroz bilo koju GeoJSON geometriju i daje svaku tačku. */
function walk(geometry: Geometry, visit: (lon: number, lat: number) => void) {
  const coords = (value: unknown): void => {
    if (Array.isArray(value) && typeof value[0] === 'number' && typeof value[1] === 'number') {
      visit(value[0], value[1])
      return
    }
    if (Array.isArray(value)) value.forEach(coords)
  }

  if (geometry.type === 'GeometryCollection') {
    geometry.geometries.forEach((g) => walk(g, visit))
    return
  }

  coords((geometry as { coordinates?: unknown }).coordinates)
}

/**
 * Redoslijed natpisa pri gužvi.
 *
 * MapLibre pri sudaru zadržava natpis sa **manjim** `symbol-sort-key`. Poredak je zato po
 * ozbiljnosti: u poplavi se prvo ispisuju imena mjesta koja su u problemu, a ne ona koja su
 * mirna. Ovo ne mijenja nijedan podatak — samo bira šta stane na ekran kad ne stane sve.
 */
const SEVERITY_SORT: ExpressionSpecification = [
  'match',
  ['get', 'level'],
  'Emergency', 0,
  'Flood', 1,
  'Elevated', 2,
  'Normal', 3,
  4,
]

/**
 * Natpis: ime, a ispod njega vrijednost u centimetrima kad je ima.
 *
 * `valueCm` može biti `null`, a `has` u MapLibre izrazima na `null` i dalje vraća `true` —
 * pa provjera ide preko `coalesce` sa nemogućom vrijednošću.
 */
const LABEL_FIELD: ExpressionSpecification = [
  'case',
  ['>', ['coalesce', ['get', 'valueCm'], -1e9], -1e8],
  [
    'format',
    ['coalesce', ['get', 'name'], ''],
    {},
    '\n',
    {},
    ['concat', ['number-format', ['get', 'valueCm'], { 'max-fraction-digits': 0 }], ' cm'],
    { 'font-scale': 0.88 },
  ],
  ['coalesce', ['get', 'name'], ''],
]

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
  instance.addLayer({
    id: 'reaches-fill',
    type: 'fill',
    source: 'reaches',
    filter: HAS_LEVEL,
    paint: {
      'fill-color': ['get', 'color'],
      // Stariji podatak je vidljivo bljeđi (UI.md §2).
      'fill-opacity': [
        'case',
        ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.07,
        ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.11,
        0.16,
      ],
    },
  })

  // Dionice bez podatka nose šrafuru, ne samo sivu ispunu. Siva sama može izgledati kao
  // "mirno"; šrafura ne može (UI.md §2).
  instance.addLayer({
    id: 'reaches-no-data',
    type: 'fill',
    source: 'reaches',
    filter: NO_LEVEL,
    paint: hatch
      ? { 'fill-pattern': HATCH, 'fill-opacity': 0.32 }
      : { 'fill-color': '#cccccc', 'fill-opacity': 0.22 },
  })

  if (!hatch) {
    // Kad šrafure nema, obrazac nosi ivica — boja ne smije ostati jedini nosilac (UI.md §5).
    instance.addLayer({
      id: 'reaches-no-data-outline',
      type: 'line',
      source: 'reaches',
      filter: NO_LEVEL,
      paint: { 'line-color': '#5c6470', 'line-width': 1.2, 'line-dasharray': [3, 2] },
    })
  }

  // Meki odsjaj ispod granice. Daje obrisu dubinu na tamnoj podlozi, a pošto je to ista
  // boja statusa razliven u prostoru, ne uvodi nijednu novu boju u paletu.
  instance.addLayer({
    id: 'reaches-glow',
    type: 'line',
    source: 'reaches',
    filter: HAS_LEVEL,
    paint: {
      'line-color': ['get', 'color'],
      'line-width': ['interpolate', ['linear'], ['zoom'], 6, 6, 11, 14],
      'line-blur': ['interpolate', ['linear'], ['zoom'], 6, 4, 11, 10],
      'line-opacity': [
        'case',
        ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.1,
        ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.16,
        0.26,
      ],
    },
  })

  // Granica dionice — nosi boju punom jačinom. Ovo je element koji oko treba da uhvati.
  instance.addLayer({
    id: 'reaches-edge',
    type: 'line',
    source: 'reaches',
    filter: HAS_LEVEL,
    paint: {
      'line-color': ['get', 'color'],
      'line-width': ['interpolate', ['linear'], ['zoom'], 6, 1.5, 11, 3.2],
      'line-opacity': [
        'case',
        ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.5,
        ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.75,
        0.95,
      ],
    },
  })

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

  // Odabrana dionica — širok mek oreol pa oštra bijela kontura iznad svega ostalog.
  instance.addLayer({
    id: 'reaches-selected-glow',
    type: 'line',
    source: 'reaches',
    filter: NOTHING,
    paint: {
      'line-color': '#ffffff',
      'line-width': ['interpolate', ['linear'], ['zoom'], 6, 10, 11, 22],
      'line-blur': 8,
      'line-opacity': 0.22,
    },
  })

  instance.addLayer({
    id: 'reaches-selected',
    type: 'line',
    source: 'reaches',
    filter: NOTHING,
    paint: {
      'line-color': '#ffffff',
      'line-width': ['interpolate', ['linear'], ['zoom'], 6, 2.4, 11, 4],
      'line-opacity': 0.95,
    },
  })

  /*
   * Imena mjesta sa podloge idu **iznad** naših površina.
   *
   * Ovo je jedina svrha razdvajanja podloge na dva rastera: obojena dionica preko Bihaća ne
   * smije progutati riječ "Bihać". Sloj je providan svuda osim na slovima, pa ne dira boje
   * ispod sebe.
   */
  instance.addLayer({
    id: 'carto-labels',
    type: 'raster',
    source: 'carto-labels',
    paint: { 'raster-opacity': 0.75 },
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
    avpjm: { stroke: '#05070b', width: 1.5, radius: 0 },
    fhmzbih: { stroke: '#e6edf3', width: 2.5, radius: 1 },
  }

  for (const id of POINT_SOURCES) {
    instance.addSource(pointSourceId(id), {
      type: 'geojson',
      data: { type: 'FeatureCollection', features: [] },
    })

    // Tamni oreol ispod tačke. Bez njega se svijetla tačka gubi kad padne na svijetli dio
    // podloge (jezero, ime grada), a tačka koja se gubi je mjerenje koje se ne vidi.
    instance.addLayer({
      id: `${pointLayerId(id)}-halo`,
      type: 'circle',
      source: pointSourceId(id),
      paint: {
        'circle-radius': [
          'interpolate', ['linear'], ['zoom'],
          6, 7 + ring[id].radius,
          11, 13 + ring[id].radius,
        ],
        'circle-color': '#05070b',
        'circle-opacity': 0.55,
        'circle-blur': 0.6,
      },
    })

    instance.addLayer({
      id: pointLayerId(id),
      type: 'circle',
      source: pointSourceId(id),
      paint: {
        'circle-radius': [
          'interpolate', ['linear'], ['zoom'],
          6, 4.5 + ring[id].radius,
          11, 9 + ring[id].radius,
        ],
        'circle-color': ['get', 'color'],
        'circle-stroke-color': ring[id].stroke,
        'circle-stroke-width': ring[id].width,
        'circle-opacity': [
          'case',
          ['>', ['coalesce', ['get', 'ageRatio'], 0], 3], 0.45,
          ['>=', ['coalesce', ['get', 'ageRatio'], 0], 1], 0.65,
          0.95,
        ],
      },
    })
  }

  for (const id of POINT_SOURCES) {
    instance.addLayer({
      id: selectedPointLayerId(id),
      type: 'circle',
      source: pointSourceId(id),
      filter: NOTHING,
      paint: {
        'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 10, 11, 16],
        'circle-color': 'rgba(0,0,0,0)',
        'circle-stroke-color': '#ffffff',
        'circle-stroke-width': 2,
        'circle-stroke-opacity': 0.9,
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
      'circle-radius': ['interpolate', ['linear'], ['zoom'], 6, 3, 11, 6],
      'circle-color': 'rgba(0,0,0,0)',
      'circle-stroke-color': '#e6edf3',
      'circle-stroke-width': 1.5,
      'circle-stroke-opacity': 0.8,
    },
  })

  // Natpisi naših podataka idu **na vrh**, iznad imena sa podloge. Ime rijeke sa vrijednošću
  // je razlog zbog kojeg je neko otvorio ovu mapu; ime naselja je kontekst.
  instance.addLayer({
    id: 'reaches-labels',
    type: 'symbol',
    source: 'reaches',
    layout: {
      visibility: 'none',
      'text-field': LABEL_FIELD,
      'text-font': ['Noto Sans Bold'],
      'text-size': ['interpolate', ['linear'], ['zoom'], 6, 10, 11, 13],
      'text-line-height': 1.15,
      'text-padding': 6,
      'text-max-width': 9,
      'symbol-sort-key': SEVERITY_SORT,
    },
    paint: {
      'text-color': '#f2f6fb',
      'text-halo-color': '#05070b',
      'text-halo-width': 1.6,
      'text-halo-blur': 0.4,
    },
  })

  for (const id of POINT_SOURCES) {
    instance.addLayer({
      id: pointLabelId(id),
      type: 'symbol',
      source: pointSourceId(id),
      layout: {
        visibility: 'none',
        'text-field': LABEL_FIELD,
        'text-font': ['Noto Sans Bold'],
        'text-size': ['interpolate', ['linear'], ['zoom'], 6, 10, 11, 12.5],
        'text-line-height': 1.15,
        'text-offset': [0, 1.1],
        'text-anchor': 'top',
        'text-padding': 4,
        'symbol-sort-key': SEVERITY_SORT,
      },
      paint: {
        'text-color': '#f2f6fb',
        'text-halo-color': '#05070b',
        'text-halo-width': 1.6,
        'text-halo-blur': 0.4,
      },
    })
  }

  instance.addLayer({
    id: 'stations-labels',
    type: 'symbol',
    source: 'stations',
    layout: {
      visibility: 'none',
      'text-field': ['coalesce', ['get', 'name'], ''],
      'text-font': ['Noto Sans Regular'],
      // Tek od bližeg zuma: 102 imena preko cijele države su mrlja, ne informacija.
      'text-size': ['interpolate', ['linear'], ['zoom'], 8.5, 0, 9, 11],
      'text-offset': [0, 0.9],
      'text-anchor': 'top',
      'text-padding': 4,
    },
    paint: {
      'text-color': '#aeb7c4',
      'text-halo-color': '#05070b',
      'text-halo-width': 1.4,
    },
  })
}

/** HTML u popupu ide kroz `setHTML`, pa svaka vrijednost iz izvora mora biti pobjegnuta. */
function escapeHtml(value: unknown): string {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
}

/**
 * Sadržaj pregleda na prelazak mišem.
 *
 * Nosi isto što i tabela: ime, vrijednost, doslovan natpis stanja i vrijeme **mjerenja**.
 * Vrijeme je obavezno i ovdje — vrijednost bez njega je tvrdnja o sadašnjosti koju nemamo
 * osnova iznijeti (zlatno pravilo 2).
 */
function reachPopupHtml(reach: ReachProperties): string {
  const value =
    reach.valueCm === null || reach.valueCm === undefined
      ? '<span style="color:#7c8798">nema podatka</span>'
      : `<span style="font-weight:600">${escapeHtml(reach.valueCm)}</span> <span style="color:#7c8798">cm</span>`

  const measured = reach.measuredAt
    ? new Date(reach.measuredAt).toLocaleString('bs-BA', {
        day: 'numeric',
        month: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      })
    : (reach.noDataReason ?? 'vrijeme mjerenja nije objavljeno')

  const river = reach.river
    ? `<div style="color:#7c8798;font-size:11px">Rijeka ${escapeHtml(reach.river)}</div>`
    : ''

  return `
    <div class="panel" style="padding:10px 12px;font-size:12px;line-height:1.45;color:#e9eef5">
      <div style="display:flex;align-items:center;gap:7px;margin-bottom:3px">
        <span style="width:9px;height:9px;border-radius:999px;flex:none;background:${escapeHtml(
          reach.color ?? '#cccccc',
        )};border:1px solid rgb(0 0 0 / .45)"></span>
        <span style="font-weight:600">${escapeHtml(reach.name)}</span>
      </div>
      ${river}
      <div style="margin-top:5px;font-size:15px">${value}</div>
      <div style="color:#aeb7c4;margin-top:3px">${escapeHtml(reach.levelLabel)}</div>
      <div style="color:#7c8798;margin-top:2px">${escapeHtml(measured)}</div>
      <div style="color:#7c8798;margin-top:5px;font-size:11px">${escapeHtml(reach.agencyName)}</div>
    </div>`
}

function stationPopupHtml(station: StationProperties): string {
  return `
    <div class="panel" style="padding:10px 12px;font-size:12px;line-height:1.45;color:#e9eef5">
      <div style="font-weight:600">${escapeHtml(station.name)}</div>
      <div style="color:#7c8798;margin-top:2px">Mjerno mjesto — bez vodostaja</div>
      <div style="color:#7c8798;margin-top:5px;font-size:11px">${escapeHtml(station.agencyName)}</div>
    </div>`
}
