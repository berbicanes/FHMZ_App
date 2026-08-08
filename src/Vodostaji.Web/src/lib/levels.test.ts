import { describe, expect, it } from 'vitest'
import { bucketOf, summarizeBuckets } from './levels'
import type { ReachProperties } from '../api/types'

const reach = (properties: Partial<ReachProperties>): ReachProperties =>
  properties as unknown as ReachProperties

describe('bucketOf', () => {
  it('ocijenjena dionica ide u grupu svog stupnja', () => {
    expect(bucketOf(reach({ level: 'Normal', valueCm: 37 }))).toBe('Normal')
    expect(bucketOf(reach({ level: 'Emergency', valueCm: 400 }))).toBe('Emergency')
  })

  it('izmjereno bez ocjene NIJE isto što i nema podatka', () => {
    // AVPJM i FHMZBIH ne objavljuju stupanj opasnosti javnosti (SOURCES.md §2.1). Njihovih
    // 32 stanice imaju broj; spojiti ih sa praznima znači na ekranu izbrisati taj podatak.
    expect(bucketOf(reach({ level: 'Unknown', valueCm: 17.7 }))).toBe('Measured')
    expect(bucketOf(reach({ level: 'Unknown', valueCm: null }))).toBe('NoData')
    expect(bucketOf(reach({ level: 'Unknown' }))).toBe('NoData')
  })

  it('nepoznat stupanj iz izvora ne postaje Normal', () => {
    // Zlatno pravilo 1, u obliku u kojem se najlakše prekrši: nova oznaka koju ne poznajemo.
    expect(bucketOf(reach({ level: 'NestoNovo', valueCm: 50 }))).toBe('Measured')
    expect(bucketOf(reach({ level: null, valueCm: null }))).toBe('NoData')
  })
})

describe('summarizeBuckets', () => {
  it('broji po grupama i drži redoslijed od najozbiljnijeg', () => {
    const result = summarizeBuckets([
      reach({ level: 'Normal', valueCm: 10, levelLabel: 'Normalno', color: '#38a800' }),
      reach({ level: 'Normal', valueCm: 12, levelLabel: 'Normalno', color: '#38a800' }),
      reach({ level: 'Emergency', valueCm: 400, levelLabel: 'Značajne poplave', color: '#e60000' }),
      reach({ level: 'Unknown', valueCm: 17, levelLabel: 'Izmjereno, bez ocjene opasnosti', color: '#4a8fd4' }),
      reach({ level: 'Unknown', valueCm: null, levelLabel: 'Nema podatka', color: '#cccccc' }),
    ])

    expect(result.map((b) => [b.key, b.count])).toEqual([
      ['Emergency', 1],
      ['Normal', 2],
      ['Measured', 1],
      ['NoData', 1],
    ])
  })

  it('preuzima doslovan natpis i boju izvora, ne svoje', () => {
    const [bucket] = summarizeBuckets([
      reach({ level: 'Elevated', valueCm: 200, levelLabel: 'Izljevanje iz korita', color: '#FFFF00' }),
    ])

    expect(bucket.label).toBe('Izljevanje iz korita')
    expect(bucket.color).toBe('#FFFF00')
  })

  it('ne prikazuje prazne grupe', () => {
    // Prazna grupa "Poplave" u traci se čita kao "nigdje nema poplava" — a to je tvrdnja
    // koju ovi podaci ne podupiru, jer tri agencije od četiri ništa ne ocjenjuju.
    const result = summarizeBuckets([reach({ level: 'Normal', valueCm: 10, levelLabel: 'Normalno' })])

    expect(result).toHaveLength(1)
    expect(result[0].key).toBe('Normal')
  })

  it('prazan ulaz daje praznu traku', () => {
    expect(summarizeBuckets([])).toEqual([])
  })
})
