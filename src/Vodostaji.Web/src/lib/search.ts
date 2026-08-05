import type { ReachProperties, StationProperties } from '../api/types'

/**
 * Pretraga po imenu rijeke **i** po imenu mjesta (UI.md §4).
 *
 * Korisnik iz Maglaja kuca "Maglaj", ne "Bosna" — i kuca "gorazde", ne "Goražde". Dijakritika
 * se zato uklanja s obje strane poređenja. Traži se i po opisu lokacije, jer agencija tu piše
 * stvari poput "uzvodno od ušća Krivaje", a to je često jedini toponim koji korisnik zna.
 */
export function normalise(value: string | null | undefined): string {
  if (!value) return ''

  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    // Đ i đ nemaju rastavljeni oblik, pa ih NFD ne dotakne.
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim()
}

/**
 * `đ` se u našim podacima svodi na `d`, ali korisnik ga često kuca kao `dj` —
 * "djurdjevik" za "Đurđevik". Zato se pored običnog poređenja pokušava i ono u kojem se
 * `dj` iz upita sažme na `d`.
 *
 * Provjerava se **oboje**, ne samo sažeto: da sažimanje ne bi pojelo imena koja legitimno
 * sadrže `dj` (npr. "između"), gdje bi "izmedju" → "izmedu" prestalo da se poklapa.
 */
export function matches(haystack: string | null | undefined, needle: string): boolean {
  if (!needle) return false

  const target = normalise(haystack)
  if (!target) return false

  return target.includes(needle) || target.includes(needle.replace(/dj/g, 'd'))
}

export interface SearchHit {
  kind: 'reach' | 'station'
  key: string
  title: string
  subtitle: string | null
}

export function searchReaches(reaches: ReachProperties[], query: string): SearchHit[] {
  const needle = normalise(query)
  if (!needle) return []

  return reaches
    .filter((reach) => [reach.name, reach.river].some((f) => matches(f, needle)))
    .map((reach) => ({
      kind: 'reach' as const,
      key: reach.stationKey ?? '',
      title: reach.name ?? '',
      subtitle: reach.levelLabel ?? null,
    }))
}

export function searchStations(stations: StationProperties[], query: string): SearchHit[] {
  const needle = normalise(query)
  if (!needle) return []

  return stations
    .filter((station) => [station.name, station.location].some((f) => matches(f, needle)))
    .map((station) => ({
      kind: 'station' as const,
      key: station.stationKey ?? '',
      title: station.name ?? '',
      subtitle: station.location ?? null,
    }))
}
