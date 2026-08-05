import { describe, expect, it } from 'vitest'
import { parseRoute, routeToPath } from './router'

/**
 * Deep linkovi su po UI.md §4 glavni kanal distribucije — novinari i lokalne grupe dijele
 * link na konkretnu dionicu. Link koji se pokvari je izgubljen posjetilac koji je već bio
 * jedan klik daleko.
 *
 * Ruta nosi i izvor jer ključ nije globalan. To se vidjelo tek sa drugim izvorom: AVP Sava
 * ima dionicu `1`, AVPJM ima stanicu `1`, i ruta bez izvora bi jednu od njih učinila
 * nedostupnom a drugu otvarala pod pogrešnim imenom.
 */
describe('parseRoute', () => {
  it('čita rute dionice i stanice sa izvorom', () => {
    expect(parseRoute('/dionica/avp-sava/1')).toEqual({
      kind: 'reach',
      sourceId: 'avp-sava',
      key: '1',
    })
    expect(parseRoute('/stanica/avp-sava/71')).toEqual({
      kind: 'station',
      sourceId: 'avp-sava',
      key: '71',
    })
  })

  it('razlikuje isti ključ kod različitih izvora', () => {
    // Ovo je cijeli razlog zbog kojeg izvor stoji u ruti.
    const sava = parseRoute('/dionica/avp-sava/1')
    const jadran = parseRoute('/dionica/avpjm/1')

    expect(sava).not.toEqual(jadran)
    expect(jadran).toEqual({ kind: 'reach', sourceId: 'avpjm', key: '1' })
  })

  it('podnosi kosu crtu na kraju', () => {
    expect(parseRoute('/dionica/avp-sava/1/')).toEqual({
      kind: 'reach',
      sourceId: 'avp-sava',
      key: '1',
    })
  })

  it('dekodira ključ iz URL-a', () => {
    expect(parseRoute('/stanica/avp-sava/HS%20Gora%C5%BEde')).toEqual({
      kind: 'station',
      sourceId: 'avp-sava',
      key: 'HS Goražde',
    })
  })

  it('nepotpuna ili nepoznata putanja vodi na mapu, ne u prazan ekran', () => {
    expect(parseRoute('/')).toEqual({ kind: 'map' })
    expect(parseRoute('/nesto/drugo/trece')).toEqual({ kind: 'map' })
    expect(parseRoute('/dionica')).toEqual({ kind: 'map' })
    expect(parseRoute('/dionica/avp-sava')).toEqual({ kind: 'map' })
    expect(parseRoute('/dionica/avp-sava/')).toEqual({ kind: 'map' })
  })
})

describe('routeToPath', () => {
  it('gradi putanju koja se može podijeliti', () => {
    expect(routeToPath({ kind: 'reach', sourceId: 'avp-sava', key: '1' })).toBe(
      '/dionica/avp-sava/1',
    )
    expect(routeToPath({ kind: 'station', sourceId: 'avp-sava', key: '71' })).toBe(
      '/stanica/avp-sava/71',
    )
    expect(routeToPath({ kind: 'map' })).toBe('/')
  })

  it('kodira ključ sa razmakom i dijakritikom', () => {
    expect(routeToPath({ kind: 'station', sourceId: 'avp-sava', key: 'HS Goražde' })).toBe(
      '/stanica/avp-sava/HS%20Gora%C5%BEde',
    )
  })

  it('putanja preživi krug tamo i nazad', () => {
    const cases = [
      { sourceId: 'avp-sava', key: '1' },
      { sourceId: 'avpjm', key: '1' },
      { sourceId: 'avp-sava', key: 'Sava - Odžačka Posavina' },
    ]

    for (const { sourceId, key } of cases) {
      expect(parseRoute(routeToPath({ kind: 'reach', sourceId, key }))).toEqual({
        kind: 'reach',
        sourceId,
        key,
      })
    }
  })
})
