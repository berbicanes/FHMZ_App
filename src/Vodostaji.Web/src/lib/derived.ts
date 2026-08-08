import type { ReachProperties, ReachThreshold } from '../api/types'

/**
 * Izvedene činjenice o jednom očitanju.
 *
 * Sve ovdje je **aritmetika nad brojevima koje je agencija već objavila** — razlika dvije
 * njihove vrijednosti, ili podjela njihove promjene njihovim vremenom. Nijedna funkcija ne
 * uvodi novu kategoriju, prag ni ocjenu; to je zabranjeno (zlatno pravilo 3, i CLAUDE.md →
 * „Ne izvoditi vlastite pragove iz sirovih cm vrijednosti").
 *
 * Granica je jednostavna: smijemo reći „87 cm ispod praga koji zove *Redovna odbrana*", jer
 * su i 87 i taj prag njihovi. Ne smijemo reći „približava se opasnosti", jer je to ocjena.
 */

/** Prag sa vrijednošću — nakon filtriranja onih koje izvor nije popunio. */
export type NumericThreshold = ReachThreshold & { valueCm: number }

export function numericThresholds(
  thresholds: ReachThreshold[] | null | undefined,
): NumericThreshold[] {
  return (thresholds ?? [])
    .filter((t): t is NumericThreshold => typeof t.valueCm === 'number')
    .sort((a, b) => a.valueCm - b.valueCm)
}

/**
 * Najbliži prag **iznad** trenutne vrijednosti, i koliko centimetara fali do njega.
 *
 * Ovo je najkorisniji jedan broj na ekranu: „još 87 cm" odgovara na pitanje zbog kojeg je
 * neko otvorio aplikaciju. Ostaje čista oduzimanja — ime praga se prepisuje doslovno.
 */
export function nextThresholdAbove(
  valueCm: number | null | undefined,
  thresholds: ReachThreshold[] | null | undefined,
): { threshold: NumericThreshold; distanceCm: number } | null {
  if (valueCm === null || valueCm === undefined) return null

  const above = numericThresholds(thresholds).find((t) => t.valueCm > valueCm)
  if (!above) return null

  return { threshold: above, distanceCm: above.valueCm - valueCm }
}

/**
 * Najviši prag koji je vrijednost **dostigla ili prešla**.
 *
 * Ne pretvara se u status. Agencija svoj status objavljuje zasebno i on uvijek pobjeđuje;
 * ovo samo imenuje gdje se broj nalazi na njihovoj vlastitoj skali.
 */
export function highestReached(
  valueCm: number | null | undefined,
  thresholds: ReachThreshold[] | null | undefined,
): NumericThreshold | null {
  if (valueCm === null || valueCm === undefined) return null

  const reached = numericThresholds(thresholds).filter((t) => valueCm >= t.valueCm)
  return reached.at(-1) ?? null
}

/**
 * Brzina promjene u cm/h.
 *
 * `changeCm` je razlika dva očitanja, `changeOverMinutes` stvarni razmak među njima — a taj
 * razmak nije uvijek sat. Ako je izostalo nekoliko ciklusa, dijeljenje sa fiksnim satom bi
 * proglasilo mirnu rijeku brzom. Zato se dijeli stvarnim vremenom ili se ne računa.
 *
 * Vraća `null` i kad je razmak prekratak: 4 cm izmjerena u razmaku od dvije minute daju
 * 120 cm/h, što je artefakt zaokruživanja vremena, a ne mjerenje brzine.
 */
export function ratePerHour(reach: ReachProperties): number | null {
  const change = reach.changeCm
  const minutes = reach.changeOverMinutes

  if (change === null || change === undefined) return null
  if (minutes === null || minutes === undefined) return null
  if (minutes < 10) return null

  return (change / minutes) * 60
}

/**
 * Kako opisati ritam stanice.
 *
 * Razlika između izmjerenog i pretpostavljenog ritma nije sitnica: na njoj visi cijela
 * ocjena svježine (`freshness.ts`), pa korisnik mora vidjeti koja je od dvije u igri.
 */
export function cadenceLabel(reach: ReachProperties): string | null {
  const minutes = reach.expectedIntervalMinutes
  if (!minutes || minutes <= 0) return null

  const every = formatDuration(minutes)

  return reach.intervalIsMeasured
    ? `otprilike svakih ${every} — izmjereno iz naših zapisa`
    : `očekivano svakih ${every} — pretpostavka, ritam još nije izmjeren`
}

export function formatDuration(minutes: number): string {
  if (minutes < 60) return `${Math.round(minutes)} min`

  const hours = minutes / 60
  if (hours < 24) {
    const rounded = Math.round(hours * 10) / 10
    return `${rounded % 1 === 0 ? rounded.toFixed(0) : rounded.toFixed(1)} h`
  }

  const days = Math.round((hours / 24) * 10) / 10
  return `${days % 1 === 0 ? days.toFixed(0) : days.toFixed(1)} dana`
}

/**
 * Sažetak niza očitanja: najniže, najviše, raspon i koliko ih je.
 *
 * Opis skupa brojeva, ne ocjena o njima. „Najviše u 7 dana: 214 cm" je činjenica o našim
 * zapisima; koliko je to ozbiljno i dalje govori samo agencija.
 */
export function seriesSummary(
  values: number[],
): { min: number; max: number; span: number; count: number } | null {
  if (values.length === 0) return null

  const min = Math.min(...values)
  const max = Math.max(...values)

  return { min, max, span: max - min, count: values.length }
}

/** Broj u tekst bez lažne preciznosti — izvori šalju i `17.7000008`. */
export function cm(value: number): string {
  const rounded = Math.round(value * 10) / 10
  return rounded % 1 === 0 ? rounded.toFixed(0) : rounded.toFixed(1)
}

/** Predznak se ispisuje uvijek, jer je `+3` i `-3` cijela razlika u značenju. */
export function signedCm(value: number): string {
  return `${value > 0 ? '+' : value < 0 ? '−' : ''}${cm(Math.abs(value))}`
}
