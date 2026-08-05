import type { ReachProperties } from '../api/types'
import {
  changeWindow,
  formatMeasuredAt,
  freshnessLabel,
  freshnessOf,
  trendArrow,
  trendLabel,
  trendOf,
} from '../lib/freshness'
import { unusualChange } from '../lib/unusual'
import { lazy, Suspense } from 'react'

/**
 * Graf se učitava tek kad se otvori detalj. Recharts je oko 100 KB gzip, a mapa se otvara
 * mnogo češće nego pojedina dionica — plaćati ga na svakom učitavanju znači usporiti
 * aplikaciju za sve, zbog ekrana koji većina neće ni otvoriti (UI.md §4).
 */
const HistoryChart = lazy(() =>
  import('./HistoryChart').then((module) => ({ default: module.HistoryChart })),
)

/**
 * Detalj odabrane dionice.
 *
 * Vodostaj, vrijeme mjerenja, pragovi, graf 7/30 dana i atribucija.
 *
 * Strelice trenda još nema: izvodi se iz dva uzastopna očitanja, a historija se tek puni.
 * Strelica izvedena iz jednog očitanja bila bi izmišljen podatak.
 */
export function ReachDetail({
  reach,
  onClose,
}: {
  reach: ReachProperties
  onClose: () => void
}) {
  const freshness = freshnessOf(reach)
  const measured = formatMeasuredAt(reach.measuredAt)
  const unusual = unusualChange(reach)

  return (
    <aside
      aria-label={`Detalj dionice ${reach.name}`}
      className="border-t border-[--color-border] bg-[--color-surface-raised] p-4"
    >
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold">{reach.name}</h2>
          {reach.river && (
            <p className="text-xs text-[--color-text-muted]">Rijeka {reach.river}</p>
          )}
        </div>
        <button
          type="button"
          onClick={onClose}
          className="rounded border border-[--color-border] px-2 py-1 text-xs text-[--color-text-muted] hover:text-[--color-text]"
        >
          Zatvori
        </button>
      </div>

      {/* Vodostaj je heroj ekrana (UI.md §6). Brojevi tabularni. */}
      {reach.valueCm !== null && reach.valueCm !== undefined ? (
        <>
          <p className="tabular mb-1 text-4xl leading-none font-semibold">
            {reach.valueCm}
            <span className="ml-1.5 text-base font-normal text-[--color-text-muted]">cm</span>
          </p>

          {/* Strelica nikad ne ide sama. Trend je naš izvod iz dva očitanja, pa uz njega
              stoje tačna razlika i period — inače je tvrdnja koju korisnik ne može provjeriti. */}
          {reach.changeCm !== null && reach.changeCm !== undefined && (
            <p className="mb-2 flex flex-wrap items-baseline gap-x-1.5 text-sm">
              <span aria-hidden="true">{trendArrow(trendOf(reach))}</span>
              <span>{trendLabel(trendOf(reach))}</span>
              <span className="tabular text-[--color-text-muted]">
                {reach.changeCm > 0 ? '+' : ''}
                {reach.changeCm} cm
              </span>
              <span className="text-xs text-[--color-text-muted]">
                {changeWindow(reach.changeOverMinutes)}
              </span>
            </p>
          )}
        </>
      ) : (
        <p className="mb-1 text-2xl leading-none font-semibold text-[--color-text-muted]">
          Nema podatka
        </p>
      )}

      <p className="mb-3 flex items-center gap-2 text-sm">
        <span
          aria-hidden="true"
          className="status-swatch h-3 w-3 shrink-0 rounded-sm border border-black/40"
          style={{
            backgroundColor: reach.color ?? '#cccccc',
            backgroundImage:
              freshness === 'unknown'
                ? 'repeating-linear-gradient(45deg, #5c6470 0 2px, transparent 2px 5px)'
                : undefined,
          }}
        />
        <span>{reach.levelLabel}</span>
      </p>

      <dl className="space-y-1.5 text-sm">
        {measured ? (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Mjereno</dt>
            <dd className="text-right">
              {measured} <span className="text-[--color-text-muted]">({freshnessLabel(reach)})</span>
            </dd>
          </div>
        ) : (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Zašto nema podatka</dt>
            <dd className="text-right">{reach.noDataReason ?? 'Izvor nije poslao vrijednost.'}</dd>
          </div>
        )}

        {/* Doslovni tekst agencije. Termin koji korisnik vidi mora biti isti kroz cijeli
            flow (UI.md §7), pa se pokazuje i ono što je izvor stvarno rekao.

            AVPJM ne šalje nikakav tekst statusa, pa prazan red ne stoji — prazno polje uz
            natpis "Izvor kaže" izgleda kao da smo nešto izgubili, a nismo: nema šta da se
            pokaže. Razlog je objašnjen u legendi tog izvora. */}
        {reach.statusLabelOriginal && reach.statusLabelOriginal.trim().length > 0 && (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Izvor kaže</dt>
            <dd className="text-right">{reach.statusLabelOriginal}</dd>
          </div>
        )}

        {/* Bez ovoga podatak star dva sata izgleda kao zastoj, a nije — izvor jednostavno
            objavljuje sa kašnjenjem. Prešutjeti to znači pustiti korisnika da pogrešno
            procijeni koliko je informacija aktuelna. */}
        {reach.publicationLagMinutes > 0 && (
          <div className="flex justify-between gap-4">
            <dt className="text-[--color-text-muted]">Kašnjenje objave</dt>
            <dd className="text-right">
              oko {Math.round(reach.publicationLagMinutes / 60)} h
            </dd>
          </div>
        )}
      </dl>

      {reach.thresholds && reach.thresholds.length > 0 && (
        <section className="mt-4">
          <h3 className="mb-1.5 text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
            Pragovi
          </h3>
          <ul className="space-y-1 text-sm">
            {reach.thresholds.map((threshold) => (
              <li key={threshold.label} className="flex justify-between gap-4">
                <span className="text-[--color-text-muted]">{threshold.label}</span>
                <span className="tabular">{threshold.valueCm} cm</span>
              </li>
            ))}
          </ul>
          {/* Pragove definiše agencija, i njeno ime ide uz njih (UI.md §3). */}
          <p className="mt-1.5 text-xs text-[--color-text-muted]">
            Pragove definiše {reach.thresholdsDefinedBy}.
          </p>
        </section>
      )}

      {/* Činjenica, ne sud. Ne tvrdimo da je očitanje pogrešno — kažemo da je promjena veća
          od cijelog raspona pragova koje je odredila agencija, i upućujemo na nju. */}
      {unusual && (
        <p className="mt-3 rounded border border-[#7a5a1a] bg-[#2a2000] p-3 text-xs leading-relaxed text-[#ffd98a]">
          Promjena od {unusual.changeCm > 0 ? '+' : ''}
          {unusual.changeCm} cm veća je od cijelog raspona pragova ove dionice (
          {unusual.lowestCm}–{unusual.highestCm} cm). Vrijednost je prikazana onako kako ju je
          objavio {reach.agencyName}; prije oslanjanja provjeri kod njih.
        </p>
      )}

      {reach.sourceId && reach.stationKey && (
        <Suspense
          fallback={
            <p className="mt-4 text-sm text-[--color-text-muted]">Učitavanje historije…</p>
          }
        >
          <HistoryChart sourceId={reach.sourceId} stationKey={reach.stationKey} />
        </Suspense>
      )}

      {/* Atribucija po dionici, ne u footeru (LEGAL.md §2.1). */}
      <p className="mt-4 border-t border-[--color-border] pt-3 text-xs text-[--color-text-muted]">
        Izvor:{' '}
        <a
          href={reach.sourceUrl ?? reach.agencyUrl ?? '#'}
          target="_blank"
          rel="noreferrer"
          className="underline"
        >
          {reach.agencyName}
        </a>
      </p>
    </aside>
  )
}
