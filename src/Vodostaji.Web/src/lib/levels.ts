import type { ReachProperties } from '../api/types'

/**
 * Grupe stanja za sažetak i filter.
 *
 * Grupisanje **samo po `level` bi bilo netačno**. AVPJM i FHMZBIH ne objavljuju stupanj
 * opasnosti javnosti (SOURCES.md §2.1), pa im je `level` uvijek `Unknown` — i onda kad
 * vrijednost postoji. Backend tu razliku već pravi u natpisu i boji: „Izmjereno, bez ocjene
 * opasnosti" (#4a8fd4 / #3aa8a0) nije isto što i „Nema podatka" (#CCCCCC).
 *
 * Spojiti ih u jedan koš značilo bi na ekranu tvrditi da za 32 stanice nemamo podatak, a
 * imamo ga — samo nema ko da ga ocijeni. To je obrnut smjer zlatnog pravila 1, ali ista
 * greška: brisanje razlike između „ne znam" i „znam".
 */
export type BucketKey = 'Emergency' | 'Flood' | 'Elevated' | 'Normal' | 'Measured' | 'NoData'

/** Od najozbiljnijeg prema dolje. Ono što traži pažnju stoji prvo. */
export const BUCKET_ORDER: readonly BucketKey[] = [
  'Emergency',
  'Flood',
  'Elevated',
  'Normal',
  'Measured',
  'NoData',
] as const

/**
 * Natpis kad u grupi nema nijedne dionice iz koje bi se pročitao agencijski.
 *
 * Prvi izbor je uvijek **doslovan natpis izvora** — ovi se koriste samo kao rezerva, da
 * prazna grupa ne ostane bezimena.
 */
export const FALLBACK_LABEL: Record<BucketKey, string> = {
  Emergency: 'Značajne poplave',
  Flood: 'Poplave',
  Elevated: 'Izljevanje iz korita',
  Normal: 'Normalno',
  Measured: 'Izmjereno, bez ocjene opasnosti',
  NoData: 'Nema podatka',
}

const FALLBACK_COLOR: Record<BucketKey, string> = {
  Emergency: '#e60000',
  Flood: '#ffaa00',
  Elevated: '#ffff00',
  Normal: '#38a800',
  Measured: '#4a8fd4',
  NoData: '#cccccc',
}

export function hasMeasurement(reach: ReachProperties): boolean {
  return reach.valueCm !== null && reach.valueCm !== undefined
}

export function bucketOf(reach: ReachProperties): BucketKey {
  const level = reach.level

  if (level === 'Emergency' || level === 'Flood' || level === 'Elevated' || level === 'Normal') {
    return level
  }

  // Sve ostalo je `Unknown` — a unutar njega je jedina razlika koja postoji ta da li broj
  // uopšte imamo.
  return hasMeasurement(reach) ? 'Measured' : 'NoData'
}

export interface Bucket {
  key: BucketKey
  /** Doslovan natpis izvora kad ga ima. Ne prevodimo ga i ne skraćujemo (zlatno pravilo 3). */
  label: string
  color: string
  count: number
}

/**
 * Sažetak po grupama, u fiksnom redoslijedu, **bez praznih grupa**.
 *
 * Prazna grupa u traci se čita kao tvrdnja („nigdje nema poplava"), a to je tvrdnja koju
 * ovaj skup podataka ne podupire: tri agencije od četiri ne ocjenjuju ništa. Grupa se
 * pojavljuje tek kad u njoj nešto stvarno postoji.
 */
export function summarizeBuckets(reaches: ReachProperties[]): Bucket[] {
  const groups = new Map<BucketKey, ReachProperties[]>()

  for (const reach of reaches) {
    const key = bucketOf(reach)
    const existing = groups.get(key)
    if (existing) existing.push(reach)
    else groups.set(key, [reach])
  }

  return BUCKET_ORDER.flatMap((key) => {
    const members = groups.get(key)
    if (!members || members.length === 0) return []

    const named = members.find((r) => r.levelLabel && r.levelLabel.trim().length > 0)
    const colored = members.find((r) => r.color && r.color.trim().length > 0)

    return [
      {
        key,
        label: named?.levelLabel ?? FALLBACK_LABEL[key],
        color: colored?.color ?? FALLBACK_COLOR[key],
        count: members.length,
      },
    ]
  })
}
