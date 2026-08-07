import type { ReachProperties, ReachThreshold } from '../api/types'

/**
 * Vodostaj u odnosu na pragove agencije, kao skala.
 *
 * Spisak brojeva („17.7 cm, pragovi 124/154/344/394") traži od korisnika da u glavi uporedi
 * pet brojeva. Skala to uradi za njega, a da **ništa ne zaključuje**: crta gdje je voda i
 * gdje su pragovi, imenovane onako kako ih agencija imenuje. Status i dalje dolazi od izvora.
 *
 * Ovo je prikaz podatka, ne dekor — zato smije biti istaknut.
 */
export function ThresholdScale({ reach }: { reach: ReachProperties }) {
  const value = reach.valueCm
  const thresholds = (reach.thresholds ?? [])
    .filter((t): t is ReachThreshold & { valueCm: number } => typeof t.valueCm === 'number')
    .sort((a, b) => a.valueCm - b.valueCm)

  if (thresholds.length === 0 || value === null || value === undefined) return null

  const lowest = thresholds[0].valueCm
  const highest = thresholds[thresholds.length - 1].valueCm

  // Domen mora obuhvatiti i vrijednost koja je izvan raspona pragova — a to je čest slučaj:
  // ljeti su rijeke duboko ispod najnižeg praga. Skala koja odsiječe vrijednost lagala bi
  // o tome gdje je voda.
  const padding = Math.max((highest - lowest) * 0.15, 10)
  const min = Math.min(value, lowest) - padding
  const max = Math.max(value, highest) + padding
  const span = max - min || 1

  const position = (v: number) => ((v - min) / span) * 100

  return (
    <section className="mt-5" aria-label="Vodostaj u odnosu na pragove">
      <h3 className="eyebrow mb-3">Pragovi</h3>

      <div className="relative h-2 rounded-full bg-[--color-ink-700]">
        {/* Ispunjeni dio do trenutne vrijednosti. Neutralne boje — ovo nije ocjena
            opasnosti, nego položaj. */}
        <div
          className="absolute inset-y-0 left-0 rounded-full bg-[--color-ink-500]"
          style={{ width: `${Math.max(position(value), 0)}%` }}
        />

        {thresholds.map((threshold) => (
          <span
            key={threshold.label}
            className="absolute top-1/2 h-4 w-0.5 -translate-x-1/2 -translate-y-1/2 rounded bg-[--color-text-muted]"
            style={{ left: `${position(threshold.valueCm)}%` }}
            aria-hidden="true"
          />
        ))}

        {/* Kazaljka trenutne vrijednosti. Nosi boju statusa jer je to jedina stvar na
            skali koja smije biti najsvjetlija. */}
        <span
          className="absolute top-1/2 h-5 w-1 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 ring-[--color-ink-900]"
          style={{
            left: `${position(value)}%`,
            backgroundColor: reach.color ?? '#cccccc',
          }}
          aria-hidden="true"
        />
      </div>

      <ul className="mt-3 space-y-1.5 text-sm">
        {thresholds.map((threshold) => {
          const reached = value >= threshold.valueCm

          return (
            <li key={threshold.label} className="flex items-baseline justify-between gap-4">
              <span
                className={reached ? 'text-[--color-text]' : 'text-[--color-text-muted]'}
              >
                {/* Doslovan naziv praga iz izvora — ne prevodimo ga i ne skraćujemo. */}
                {threshold.label}
              </span>
              <span className="flex items-baseline gap-2 whitespace-nowrap">
                {/* Riječima, ne samo bojom (UI.md §5). */}
                {reached && (
                  <span className="text-xs text-[--color-text-soft]">dosegnut</span>
                )}
                <span className="tabular text-[--color-text-soft]">
                  {threshold.valueCm} cm
                </span>
              </span>
            </li>
          )
        })}
      </ul>

      {reach.thresholdsDefinedBy && (
        // Prag bez imena onoga ko ga je postavio čita se kao naš (UI.md §3).
        <p className="mt-2.5 text-xs leading-relaxed text-[--color-text-muted]">
          Pragove definiše {reach.thresholdsDefinedBy}.
        </p>
      )}
    </section>
  )
}
