import { useCallback, useEffect, useState } from 'react'

/**
 * Najmanji mogući ruter, nad History API-jem.
 *
 * Namjerno bez biblioteke. `CLAUDE.md` traži da se nove zavisnosti ne uvode bez pitanja, a
 * ovdje su potrebne tačno dvije rute bez ugnježđivanja — react-router bi za to donio više
 * koda nego što ga rješava.
 *
 * Deep linkovi su, po UI.md §4, glavni kanal distribucije: novinari i lokalne grupe dijele
 * link na konkretnu dionicu. Zato je URL izvor istine o tome šta je otvoreno, a ne stanje
 * komponente — link koji neko pošalje mora otvoriti isto što je pošiljalac gledao.
 */
/**
 * Ruta nosi **i izvor i ključ**.
 *
 * Ključ stanice je jedinstven samo unutar svog izvora — to je zapisano u domenskom modelu i
 * potvrdilo se čim je došao drugi izvor: AVP Sava ima dionicu `1` (Sana-Sanski Most) i AVPJM
 * ima stanicu `1` (Mostar). Ruta bez izvora bi jedno od to dvoje učinila nedostupnim, a
 * podijeljen link bi otvarao pogrešnu rijeku.
 */
export type Route =
  | { kind: 'map' }
  | { kind: 'reach'; sourceId: string; key: string }
  | { kind: 'station'; sourceId: string; key: string }

export function parseRoute(pathname: string): Route {
  const segments = pathname.split('/').filter(Boolean)

  if (segments.length === 3) {
    const sourceId = decodeURIComponent(segments[1])
    const key = decodeURIComponent(segments[2])

    if (sourceId && key) {
      if (segments[0] === 'dionica') return { kind: 'reach', sourceId, key }
      if (segments[0] === 'stanica') return { kind: 'station', sourceId, key }
    }
  }

  return { kind: 'map' }
}

export function routeToPath(route: Route): string {
  switch (route.kind) {
    case 'reach':
      return `/dionica/${encodeURIComponent(route.sourceId)}/${encodeURIComponent(route.key)}`
    case 'station':
      return `/stanica/${encodeURIComponent(route.sourceId)}/${encodeURIComponent(route.key)}`
    case 'map':
      return '/'
  }
}

export function useRoute(): [Route, (route: Route) => void] {
  const [route, setRoute] = useState<Route>(() => parseRoute(window.location.pathname))

  useEffect(() => {
    // Dugme "nazad" mora vraćati na prethodni izbor, ne zatvarati aplikaciju.
    const onPopState = () => setRoute(parseRoute(window.location.pathname))
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const navigate = useCallback((next: Route) => {
    const path = routeToPath(next)
    if (path !== window.location.pathname) {
      window.history.pushState(null, '', path)
    }
    setRoute(next)
  }, [])

  return [route, navigate]
}
