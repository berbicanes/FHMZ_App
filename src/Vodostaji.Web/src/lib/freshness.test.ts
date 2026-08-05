import { describe, expect, it } from 'vitest'
import { fillOpacityOf, freshnessOf, relativeAge, trendOf } from './freshness'
import type { ReachProperties } from '../api/types'

const reach = (partial: Partial<ReachProperties>): ReachProperties =>
  partial as unknown as ReachProperties

/**
 * Prikaz starosti podatka (UI.md §2).
 *
 * `ageRatio` je broj **propuštenih ciklusa**, ne sirova starost — izvor objavljuje sa
 * zastojem, pa se starost mjeri od trenutka kad je podatak realno mogao stići.
 */
describe('freshnessOf', () => {
  it('nema podatka je vlastito stanje, ne najstariji stepen starosti', () => {
    // Zlatno pravilo 1: nemamo podatak i nema opasnosti su različite stvari.
    expect(freshnessOf(reach({ valueCm: null, measuredAt: null, ageRatio: null }))).toBe('unknown')
    expect(freshnessOf(reach({ valueCm: 100, measuredAt: null, ageRatio: 0 }))).toBe('unknown')
    expect(freshnessOf(reach({ valueCm: null, measuredAt: '2026-08-04T22:00:00Z', ageRatio: 0 })))
      .toBe('unknown')
  })

  it('do jednog propuštenog ciklusa je svježe', () => {
    expect(freshnessOf(reach({ valueCm: 100, measuredAt: 'x', ageRatio: 0 }))).toBe('fresh')
    expect(freshnessOf(reach({ valueCm: 100, measuredAt: 'x', ageRatio: 0.99 }))).toBe('fresh')
  })

  it('jedan do tri propuštena ciklusa su kašnjenje', () => {
    expect(freshnessOf(reach({ valueCm: 100, measuredAt: 'x', ageRatio: 1 }))).toBe('ageing')
    expect(freshnessOf(reach({ valueCm: 100, measuredAt: 'x', ageRatio: 3 }))).toBe('ageing')
  })

  it('preko tri je zastarjelo', () => {
    expect(freshnessOf(reach({ valueCm: 100, measuredAt: 'x', ageRatio: 3.01 }))).toBe('stale')
  })
})

describe('fillOpacityOf', () => {
  it('stariji podatak je vidljivo bljeđi', () => {
    expect(fillOpacityOf('fresh')).toBeGreaterThan(fillOpacityOf('ageing'))
    expect(fillOpacityOf('ageing')).toBeGreaterThan(fillOpacityOf('stale'))
  })
})

describe('trendOf', () => {
  it('nepromijenjen je tačno nula, ne raspon oko nje', () => {
    // Svaki drugi prag bio bi naša odluka o tome šta je zanemarivo.
    expect(trendOf(reach({ changeCm: 0 }))).toBe('steady')
    expect(trendOf(reach({ changeCm: 0.1 }))).toBe('rising')
    expect(trendOf(reach({ changeCm: -0.1 }))).toBe('falling')
  })

  it('bez prethodnog očitanja trend je nepoznat, ne nepromijenjen', () => {
    expect(trendOf(reach({ changeCm: null }))).toBe('unknown')
    expect(trendOf(reach({}))).toBe('unknown')
  })
})

describe('relativeAge', () => {
  it('negativna starost se imenuje, ne skriva', () => {
    // Podatak iz budućnosti je nalaz o pogrešnoj zoni, ne greška zaokruživanja.
    expect(relativeAge(-30)).toBe('iz budućnosti')
  })

  it('bira jedinicu prema veličini', () => {
    expect(relativeAge(45)).toBe('prije 45 min')
    expect(relativeAge(120)).toBe('prije 2 h')
    expect(relativeAge(60 * 24)).toBe('prije 1 dan')
    expect(relativeAge(60 * 24 * 3)).toBe('prije 3 dana')
  })
})
