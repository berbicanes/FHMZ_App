import { describe, expect, it } from 'vitest'
import { unusualChange } from './unusual'
import type { ReachProperties } from '../api/types'

const reach = (changeCm: number | null, thresholds: number[]): ReachProperties =>
  ({
    changeCm,
    thresholds: thresholds.map((valueCm) => ({ label: `t${valueCm}`, valueCm, level: null })),
  }) as unknown as ReachProperties

/**
 * Napomena o neuobičajenoj promjeni.
 *
 * Najvažniji test u ovom fajlu je onaj koji provjerava da se **stvarni poplavni talas ne
 * označava**. Oznaka koja se pali na pravu poplavu navela bi nekoga da je zanemari, a to je
 * jedini kvar gori od neoznačavanja.
 */
describe('unusualChange', () => {
  it('označava promjenu veću od cijelog raspona pragova', () => {
    // Fojnička rijeka, 2026-08-05: 160 → -28.3 za sat. Raspon pragova je 180–300, dakle 120.
    const result = unusualChange(reach(-188.3, [180, 230, 250, 300]))

    expect(result).not.toBeNull()
    expect(result?.spanCm).toBe(120)
    expect(result?.lowestCm).toBe(180)
    expect(result?.highestCm).toBe(300)
  })

  it('NE označava stvarni poplavni talas unutar raspona agencije', () => {
    // Zenica: pragovi 124–394, raspon 270. Rast od 200 cm za sat je ozbiljna poplava,
    // ali je unutar onoga što ta rijeka radi — oznaka bi je obezvrijedila.
    expect(unusualChange(reach(200, [124, 154, 344, 394]))).toBeNull()
  })

  it('ne označava uobičajena kolebanja', () => {
    expect(unusualChange(reach(40, [124, 154, 344, 394]))).toBeNull()
    expect(unusualChange(reach(-5, [180, 230, 250, 300]))).toBeNull()
  })

  it('ne označava dionicu bez pragova, jer nema skale za poređenje', () => {
    // Bez pragova nemamo mjeru; poređenje sa tuđom skalom bilo bi nagađanje.
    expect(unusualChange(reach(-500, []))).toBeNull()
    expect(unusualChange(reach(-500, [200]))).toBeNull()
  })

  it('ne označava ništa kad promjene nema', () => {
    expect(unusualChange(reach(null, [180, 230, 250, 300]))).toBeNull()
  })

  it('gleda apsolutnu vrijednost, pa i nagli pad dobija napomenu', () => {
    expect(unusualChange(reach(-130, [180, 300]))).not.toBeNull()
    expect(unusualChange(reach(130, [180, 300]))).not.toBeNull()
  })
})
