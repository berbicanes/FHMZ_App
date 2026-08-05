import type { ReachProperties } from '../api/types'

/**
 * Prikaz starosti podatka, po tabeli iz UI.md §2.
 *
 * Pragovi su izraženi u **očekivanim intervalima te stanice**, ne u satima. Stanica koja
 * se javlja jednom dnevno i ona koja se javlja svakih 15 minuta ne stare istom brzinom,
 * pa bi fiksni prag jednu prikazivao kao zastarjelu a drugu kao svježu dok obje kasne.
 */
export type Freshness = 'fresh' | 'ageing' | 'stale' | 'unknown'

export function freshnessOf(properties: ReachProperties): Freshness {
  // Nema podatka je vlastito stanje, ne najstariji stepen starosti. Zlatno pravilo 1.
  if (properties.valueCm === null || properties.valueCm === undefined) return 'unknown'
  if (properties.measuredAt === null || properties.measuredAt === undefined) return 'unknown'

  const ratio = properties.ageRatio
  if (ratio === null || ratio === undefined) return 'unknown'

  if (ratio > 3) return 'stale'
  if (ratio >= 1) return 'ageing'
  return 'fresh'
}

/** Neprozirnost ispune. Stariji podatak je vidljivo bljeđi (UI.md §2). */
export function fillOpacityOf(freshness: Freshness): number {
  switch (freshness) {
    case 'fresh':
      return 0.85
    case 'ageing':
      return 0.6
    case 'stale':
      return 0.45
    case 'unknown':
      return 0.75
  }
}

/**
 * Tekst uz boju. Boja nikad nije jedini nosilac informacije (UI.md §5), pa svaka
 * dionica nosi i rečenicu koja kaže isto što i boja.
 */
export function freshnessLabel(properties: ReachProperties): string {
  const freshness = freshnessOf(properties)

  if (freshness === 'unknown') {
    return properties.noDataReason ? 'Nema podatka' : 'Nema podatka'
  }

  if (freshness === 'stale') return 'Podatak zastario'

  return relativeAge(properties.ageMinutes ?? 0)
}

export type Trend = 'rising' | 'falling' | 'steady' | 'unknown'

/**
 * Smjer promjene, izveden iz dva očitanja.
 *
 * `steady` je rezervisan za **tačno nula**. Svaki drugi prag bi bio naša odluka o tome šta
 * je "zanemarivo", a to je odluka koju nemamo osnov donijeti — zato uz strelicu uvijek ide
 * i tačan broj, pa korisnik sam vidi je li promjena od 0.2 cm bitna.
 */
export function trendOf(properties: ReachProperties): Trend {
  // Trend koji je izvor **objavio** ima prednost nad našim izvodom iz dva očitanja.
  // FHMZBIH ga objavljuje; tvrdnja agencije je jača od našeg računa (zlatno pravilo 3).
  switch (properties.publishedTrend) {
    case 'Rising':
      return 'rising'
    case 'Falling':
      return 'falling'
    case 'Steady':
      return 'steady'
    case 'Unknown':
      // Izvor je poslao oznaku koju ne prepoznajemo. To nije "nema promjene".
      return 'unknown'
    default:
      break
  }

  const change = properties.changeCm
  if (change === null || change === undefined) return 'unknown'
  if (change > 0) return 'rising'
  if (change < 0) return 'falling'
  return 'steady'
}

export function trendArrow(trend: Trend): string {
  switch (trend) {
    case 'rising':
      return '▲'
    case 'falling':
      return '▼'
    case 'steady':
      return '▬'
    case 'unknown':
      return ''
  }
}

export function trendLabel(trend: Trend): string {
  switch (trend) {
    case 'rising':
      return 'raste'
    case 'falling':
      return 'opada'
    case 'steady':
      return 'nepromijenjen'
    case 'unknown':
      return 'trend nepoznat'
  }
}

/**
 * Period preko kojeg je promjena mjerena. Ako je izostalo nekoliko očitanja, razlika nije
 * "za sat" nego "za pet sati" — strelica bez toga pogrešno sugeriše brzinu.
 */
export function changeWindow(minutes: number | null | undefined): string | null {
  if (minutes === null || minutes === undefined) return null
  if (minutes < 90) return 'u odnosu na prethodni sat'

  const hours = Math.round(minutes / 60)
  return `u odnosu na očitanje prije ${hours} h`
}

/**
 * Čitljiv timestamp, ne ISO string (UI.md §3).
 * Vrijeme mjerenja, nikad vrijeme dohvata.
 */
export function relativeAge(minutes: number): string {
  if (minutes < 0) return 'iz budućnosti'
  if (minutes < 60) return `prije ${minutes} min`

  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `prije ${hours} h`

  const days = Math.floor(hours / 24)
  return days === 1 ? 'prije 1 dan' : `prije ${days} dana`
}

/** Apsolutno vrijeme mjerenja, u lokalnoj zoni korisnika, čitljivo. */
export function formatMeasuredAt(iso: string | null | undefined): string | null {
  if (!iso) return null

  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return null

  const today = new Date()
  const sameDay =
    date.getFullYear() === today.getFullYear() &&
    date.getMonth() === today.getMonth() &&
    date.getDate() === today.getDate()

  const time = date.toLocaleTimeString('bs-BA', { hour: '2-digit', minute: '2-digit' })

  return sameDay
    ? `danas u ${time}`
    : `${date.toLocaleDateString('bs-BA', { day: 'numeric', month: 'numeric' })} u ${time}`
}
