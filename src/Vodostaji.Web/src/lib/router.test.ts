import { describe, expect, it } from 'vitest'
import { parseRoute, routeToPath } from './router'

/**
 * Deep linkovi su po UI.md §4 glavni kanal distribucije — novinari i lokalne grupe dijele
 * link na konkretnu dionicu. Link koji se pokvari je izgubljen posjetilac koji je već bio
 * jedan klik daleko.
 */
describe('parseRoute', () => {
  it('čita rute dionice i stanice', () => {
    expect(parseRoute('/dionica/1')).toEqual({ kind: 'reach', key: '1' })
    expect(parseRoute('/stanica/71')).toEqual({ kind: 'station', key: '71' })
  })

  it('podnosi kosu crtu na kraju', () => {
    expect(parseRoute('/dionica/1/')).toEqual({ kind: 'reach', key: '1' })
  })

  it('dekodira ključ iz URL-a', () => {
    expect(parseRoute('/stanica/HS%20Gora%C5%BEde')).toEqual({
      kind: 'station',
      key: 'HS Goražde',
    })
  })

  it('nepoznata putanja vodi na mapu, ne u prazan ekran', () => {
    expect(parseRoute('/')).toEqual({ kind: 'map' })
    expect(parseRoute('/nesto/drugo')).toEqual({ kind: 'map' })
    expect(parseRoute('/dionica')).toEqual({ kind: 'map' })
    expect(parseRoute('/dionica/')).toEqual({ kind: 'map' })
  })
})

describe('routeToPath', () => {
  it('gradi putanju koja se može podijeliti', () => {
    expect(routeToPath({ kind: 'reach', key: '1' })).toBe('/dionica/1')
    expect(routeToPath({ kind: 'station', key: '71' })).toBe('/stanica/71')
    expect(routeToPath({ kind: 'map' })).toBe('/')
  })

  it('kodira ključ sa razmakom i dijakritikom', () => {
    expect(routeToPath({ kind: 'station', key: 'HS Goražde' })).toBe(
      '/stanica/HS%20Gora%C5%BEde',
    )
  })

  it('putanja preživi krug tamo i nazad', () => {
    for (const key of ['1', 'HS Goražde', 'Sava - Odžačka Posavina']) {
      expect(parseRoute(routeToPath({ kind: 'reach', key }))).toEqual({ kind: 'reach', key })
    }
  })
})
