import type { ReachProperties } from '../api/types'

/**
 * Napomena o neuobičajenoj promjeni — bez ocjene da li je očitanje tačno.
 *
 * `UI.md` §3 traži da se neuobičajeno očitanje označi. Namjerno **nije** uveden `Suspect`
 * kao sud: preosjetljiv prag bi stvarni poplavni talas označio kao sumnjiv i naveo nekoga
 * da ga zanemari, a to je jedini kvar gori od neoznačavanja.
 *
 * Mjera nije naša. Poredi se sa **rasponom pragova koje je odredila agencija** za tu
 * dionicu — od najnižeg do najvišeg. Promjena za sat veća od cijelog tog raspona je, po
 * njihovoj vlastitoj skali, izvan uobičajenog. Nijedan broj ovdje nije izmišljen.
 *
 * Dionica bez pragova ne dobija napomenu: nemamo skalu s kojom bismo poredili, a poređenje
 * s nekom drugom skalom bilo bi nagađanje.
 */
export interface UnusualChange {
  changeCm: number
  spanCm: number
  lowestCm: number
  highestCm: number
}

export function unusualChange(reach: ReachProperties): UnusualChange | null {
  const change = reach.changeCm
  if (change === null || change === undefined) return null

  const values = (reach.thresholds ?? [])
    .map((threshold) => threshold.valueCm)
    .filter((value): value is number => value !== null && value !== undefined)

  if (values.length < 2) return null

  const lowest = Math.min(...values)
  const highest = Math.max(...values)
  const span = highest - lowest
  if (span <= 0) return null

  return Math.abs(change) > span
    ? { changeCm: change, spanCm: span, lowestCm: lowest, highestCm: highest }
    : null
}
