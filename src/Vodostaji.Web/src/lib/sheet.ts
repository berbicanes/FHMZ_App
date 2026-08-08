import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Donja ploča na telefonu.
 *
 * Na uskom ekranu ne postoji način da mapa i lista od 77 redova istovremeno budu čitljive.
 * Dijeljenje ekrana napola daje dvije neupotrebljive polovine: mapu presitnu da se na njoj
 * išta pogodi prstom, i listu u kojoj stanu tri reda.
 *
 * Zato mapa ide preko cijelog ekrana, a podaci u ploču koja se izvlači — obrazac koji ljudi
 * već znaju iz aplikacija za mape, pa se ne mora učiti. Tri položaja:
 *
 * - `peek` — samo hvatište i sažetak stanja; mapa je cijela vidljiva
 * - `half` — lista, uz mapu koja se i dalje vidi i može se pomjerati
 * - `full` — detalj preko skoro cijelog ekrana
 */
export type Snap = 'peek' | 'half' | 'full'

/** Iznad ove širine nema ploče nego bočna kolona. Ista granica kao Tailwind `lg`. */
const DESKTOP = '(min-width: 1024px)'

export function useIsCompact(): boolean {
  const [compact, setCompact] = useState(
    () => typeof window !== 'undefined' && !window.matchMedia(DESKTOP).matches,
  )

  useEffect(() => {
    const query = window.matchMedia(DESKTOP)
    const update = () => setCompact(!query.matches)
    update()
    query.addEventListener('change', update)
    return () => query.removeEventListener('change', update)
  }, [])

  return compact
}

/** Visina hvatišta koja mora ostati vidljiva u `peek` položaju. */
const PEEK = 132

/** Koliko ekrana ostaje slobodno iznad pune ploče, da se mapa nikad ne izgubi sasvim. */
const TOP_GAP = 72

export interface Sheet {
  snap: Snap
  /** Visina u pikselima, ili `null` kad ploče nema (široki ekran). */
  height: number | null
  dragging: boolean
  setSnap: (snap: Snap) => void
  /** Kači se na hvatište. `touch-action: none` je obavezan, inače stranica krade potez. */
  gripProps: {
    onPointerDown: (event: React.PointerEvent) => void
    style: { touchAction: 'none' }
  }
}

export function useSheet(enabled: boolean, snap: Snap, setSnap: (snap: Snap) => void): Sheet {
  const [viewport, setViewport] = useState(() =>
    typeof window === 'undefined' ? 800 : window.innerHeight,
  )
  const [dragHeight, setDragHeight] = useState<number | null>(null)
  const drag = useRef<{ startY: number; startHeight: number } | null>(null)

  useEffect(() => {
    const onResize = () => setViewport(window.innerHeight)
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  const heightOf = useCallback(
    (value: Snap) => {
      if (value === 'peek') return PEEK
      if (value === 'half') return Math.round(viewport * 0.5)
      return Math.max(viewport - TOP_GAP, PEEK)
    },
    [viewport],
  )

  const onPointerDown = useCallback(
    (event: React.PointerEvent) => {
      if (!enabled) return

      const target = event.currentTarget as HTMLElement
      target.setPointerCapture(event.pointerId)

      const startHeight = heightOf(snap)
      drag.current = { startY: event.clientY, startHeight }
      setDragHeight(startHeight)

      const move = (moveEvent: PointerEvent) => {
        if (!drag.current) return
        // Ploča raste kad prst ide **gore**, pa je razlika obrnuta.
        const next = drag.current.startHeight + (drag.current.startY - moveEvent.clientY)
        setDragHeight(Math.min(Math.max(next, 56), viewport - TOP_GAP))
      }

      const end = (endEvent: PointerEvent) => {
        target.releasePointerCapture?.(endEvent.pointerId)
        target.removeEventListener('pointermove', move)
        target.removeEventListener('pointerup', end)
        target.removeEventListener('pointercancel', end)

        const released = drag.current
        drag.current = null

        if (!released) return

        const travelled = released.startY - endEvent.clientY
        const finalHeight = released.startHeight + travelled

        // Kratak potez je namjera da se pređe **jedan** položaj, bez obzira gdje je prst
        // stao. Bez toga brz mali potez ne bi pomjerio ništa, jer je najbliži položaj i
        // dalje onaj sa kojeg je krenuo.
        const order: Snap[] = ['peek', 'half', 'full']
        const current = order.indexOf(snap)

        if (Math.abs(travelled) > 60 && Math.abs(travelled) < 140) {
          const step = travelled > 0 ? 1 : -1
          setSnap(order[Math.min(Math.max(current + step, 0), order.length - 1)])
        } else {
          const nearest = order.reduce((best, candidate) =>
            Math.abs(heightOf(candidate) - finalHeight) < Math.abs(heightOf(best) - finalHeight)
              ? candidate
              : best,
          )
          setSnap(nearest)
        }

        setDragHeight(null)
      }

      target.addEventListener('pointermove', move)
      target.addEventListener('pointerup', end)
      target.addEventListener('pointercancel', end)
    },
    [enabled, heightOf, snap, setSnap, viewport],
  )

  return {
    snap,
    height: enabled ? (dragHeight ?? heightOf(snap)) : null,
    dragging: dragHeight !== null,
    setSnap,
    gripProps: { onPointerDown, style: { touchAction: 'none' } },
  }
}
