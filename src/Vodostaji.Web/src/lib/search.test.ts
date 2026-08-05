import { describe, expect, it } from 'vitest'
import { matches, normalise, searchReaches, searchStations } from './search'
import type { ReachProperties, StationProperties } from '../api/types'

/**
 * Pretraga po rijeci i po mjestu (UI.md §4).
 *
 * Ovo nije kozmetika. Korisnik iz Maglaja koji ukuca "maglaj" i ne dobije ništa nema drugi
 * način da nađe svoju rijeku — nazivi dionica su oblika "Bosna-Maglaj", pa je pretraga
 * jedini put od mjesta do podatka.
 */
describe('normalise', () => {
  it('skida dijakritiku jer korisnik kuca bez nje', () => {
    expect(normalise('Goražde')).toBe('gorazde')
    expect(normalise('Bosna-Žepče')).toBe('bosna-zepce')
    expect(normalise('Krušnica')).toBe('krusnica')
    expect(normalise('HS Ilidža')).toBe('hs ilidza')
  })

  it('svodi đ na d, jer NFD taj znak ne rastavlja', () => {
    expect(normalise('Đurđevik')).toBe('durdevik')
    expect(normalise('između')).toBe('izmedu')
  })

  it('prazna i nedostajuća vrijednost daju prazan string', () => {
    expect(normalise(null)).toBe('')
    expect(normalise(undefined)).toBe('')
    expect(normalise('   ')).toBe('')
  })
})

describe('matches', () => {
  it('nalazi ime napisano sa dijakritikom kad je upit bez nje', () => {
    expect(matches('HS Goražde', normalise('gorazde'))).toBe(true)
    expect(matches('Bosna-Žepče', normalise('zepce'))).toBe(true)
  })

  it('nalazi đ kad korisnik kuca dj', () => {
    // Neko iz Đurđevika kuca "djurdjevik". Bez ovoga ne dobije ništa.
    expect(matches('HS Đurđevik', normalise('djurdjevik'))).toBe(true)
    expect(matches('HS Đurđevik', normalise('durdevik'))).toBe(true)
  })

  it('ne gubi riječi koje legitimno sadrže dj', () => {
    // Sažimanje dj→d smije samo dodati poklapanja, nikad oduzeti postojeća.
    expect(matches('kod mosta između ušća', normalise('izmedju'))).toBe(true)
    expect(matches('kod mosta između ušća', normalise('izmedu'))).toBe(true)
  })

  it('prazan upit ne poklapa ništa', () => {
    expect(matches('bilo šta', '')).toBe(false)
  })

  it('prazan tekst se ne poklapa ni sa čim', () => {
    expect(matches(null, 'a')).toBe(false)
    expect(matches(undefined, 'a')).toBe(false)
  })
})

const reach = (name: string, river: string | null): ReachProperties =>
  ({ name, river, stationKey: name, levelLabel: 'Normalno' }) as unknown as ReachProperties

const station = (name: string, location: string | null): StationProperties =>
  ({ name, location, stationKey: name }) as unknown as StationProperties

describe('searchReaches', () => {
  const reaches = [
    reach('Bosna-Maglaj', 'Bosna'),
    reach('Bosna-Zenica', 'Bosna'),
    reach('Sana-Ključ', 'Sana'),
  ]

  it('nalazi po imenu mjesta, ne samo po rijeci', () => {
    // Korisnik iz Maglaja kuca "Maglaj", ne "Bosna" (UI.md §4).
    expect(searchReaches(reaches, 'maglaj').map((h) => h.title)).toEqual(['Bosna-Maglaj'])
  })

  it('nalazi sve dionice jedne rijeke', () => {
    expect(searchReaches(reaches, 'bosna')).toHaveLength(2)
  })

  it('prazan upit ne vraća sve nego ništa', () => {
    expect(searchReaches(reaches, '   ')).toEqual([])
  })
})

describe('searchStations', () => {
  const stations = [
    station('HS Goražde', 'uzvodno do pješačkog gradskog mosta'),
    station('HS Bihać', 'kod mosta'),
  ]

  it('traži i po opisu lokacije', () => {
    // Agencija tu piše toponime kojih nema u nazivu stanice.
    expect(searchStations(stations, 'pjesack').map((h) => h.title)).toEqual(['HS Goražde'])
    expect(searchStations(stations, 'gradskog mosta').map((h) => h.title)).toEqual(['HS Goražde'])
  })

  it('poklapanje je po podnizu, pa drugi padež ne nalazi — poznato ograničenje', () => {
    // Tekst nosi "pješačkog"; upit "pjesacki" je drugi padež i ne poklapa se.
    // Rješenje bi tražilo stemmer za bosanski, što je zaseban posao. Zapisano da se zna
    // da je ovo izbor, ne previd.
    expect(searchStations(stations, 'pjesacki')).toEqual([])
  })

  it('nosi opis lokacije kao podnaslov rezultata', () => {
    expect(searchStations(stations, 'bihac')[0].subtitle).toBe('kod mosta')
  })
})
