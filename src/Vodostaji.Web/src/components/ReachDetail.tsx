import { lazy, Suspense, type ReactNode } from 'react'
import type { ReachProperties } from '../api/types'
import {
  changeWindow,
  formatMeasuredAt,
  freshnessLabel,
  trendArrow,
  trendLabel,
  trendOf,
} from '../lib/freshness'
import { unusualChange } from '../lib/unusual'
import { StatusPill } from './StatusMark'
import { ThresholdScale } from './ThresholdScale'

/**
 * Graf se učitava tek kad se otvori detalj. Recharts je oko 100 kB gzip, a mapa se otvara
 * mnogo češće nego pojedina dionica — plaćati ga na svakom učitavanju znači usporiti
 * aplikaciju za sve, zbog ekrana koji većina neće ni otvoriti (UI.md §4).
 */
const HistoryChart = lazy(() =>
  import('./HistoryChart').then((module) => ({ default: module.HistoryChart })),
)

/**
 * Detalj odabrane dionice ili stanice.
 *
 * Vodostaj je heroj ekrana (UI.md §6): najveći element, u display fontu, sa tabularnim
 * ciframa. Ostalo je poredano po tome šta prvo treba nekome ko je ovo otvorio u tri ujutro —
 * koliko je vode, kad je mjereno, gdje je to u odnosu na pragove, pa tek onda historija.
 */
export function ReachDetail({
  reach,
  onClose,
}: {
  reach: ReachProperties
  onClose: () => void
}) {
  const measured = formatMeasuredAt(reach.measuredAt)
  const unusual = unusualChange(reach)
  const trend = trendOf(reach)
  const hasValue = reach.valueCm !== null && reach.valueCm !== undefined

  return (
    <article aria-label={`Detalj: ${reach.name}`} className="p-4">
      <header className="mb-4 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="truncate text-lg leading-tight font-semibold">{reach.name}</h2>
          {reach.river && (
            <p className="mt-0.5 text-sm text-[--color-text-muted]">Rijeka {reach.river}</p>
          )}
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Zatvori detalj"
          className="shrink-0 rounded-[--radius-chip] border border-[--color-line-strong] px-3 py-1 text-xs text-[--color-text-soft] hover:bg-[--color-ink-800] hover:text-[--color-text]"
        >
          Zatvori
        </button>
      </header>

      <div className="mb-4">
        {hasValue ? (
          <p className="numeric flex items-baseline gap-2 leading-none">
            <span className="text-[3.25rem] font-bold xl:text-[4rem]">{reach.valueCm}</span>
            <span className="text-lg font-medium text-[--color-text-muted]">cm</span>
          </p>
        ) : (
          <p className="numeric text-3xl leading-none font-bold text-[--color-text-muted]">
            Nema podatka
          </p>
        )}

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <StatusPill reach={reach} />

          {/* Trend koji je izvor objavio ima prednost nad našim izvodom iz dva očitanja —
              tvrdnja agencije je jača od našeg računa (zlatno pravilo 3). */}
          {hasValue && reach.publishedTrend && (
            <Chip>
              <span aria-hidden="true">{trendArrow(trend)}</span>
              <span>{trendLabel(trend)}</span>
              <span className="text-[--color-text-muted]">objavio {reach.agencyName}</span>
            </Chip>
          )}

          {hasValue &&
            !reach.publishedTrend &&
            reach.changeCm !== null &&
            reach.changeCm !== undefined && (
              <Chip>
                <span aria-hidden="true">{trendArrow(trend)}</span>
                <span>{trendLabel(trend)}</span>
                <span className="tabular text-[--color-text-muted]">
                  {reach.changeCm > 0 ? '+' : ''}
                  {reach.changeCm} cm {changeWindow(reach.changeOverMinutes)}
                </span>
              </Chip>
            )}
        </div>
      </div>

      {/* Bez ovoga se obojena dionica može pročitati kao poplavljeno područje. Stoji uz
          vodostaj, ne u dnu, jer se čita u istom pogledu. */}
      {reach.sourceId === 'avp-sava' && (
        <p className="mb-4 rounded-[--radius-card] border border-[--color-line] bg-[--color-ink-850] px-3 py-2.5 text-xs leading-relaxed text-[--color-text-soft]">
          Ocjena se odnosi na dionicu rijeke, mjerenu na hidrološkim stanicama na njoj — nije
          prikaz poplavljenog područja.
        </p>
      )}

      <dl className="space-y-2 text-sm">
        {measured ? (
          <Row label="Mjereno">
            {measured}{' '}
            <span className="text-[--color-text-muted]">({freshnessLabel(reach)})</span>
          </Row>
        ) : (
          <Row label="Zašto nema podatka">
            {reach.noDataReason ?? 'Izvor nije poslao vrijednost.'}
          </Row>
        )}

        {/* Doslovan tekst agencije. AVPJM i FHMZBIH ga ne šalju, pa prazan red ne stoji —
            prazno polje uz natpis izgleda kao da smo nešto izgubili, a nema šta pokazati. */}
        {reach.statusLabelOriginal && reach.statusLabelOriginal.trim().length > 0 && (
          <Row label="Izvor kaže">{reach.statusLabelOriginal}</Row>
        )}

        {reach.publicationLagMinutes > 0 && (
          <Row label="Kašnjenje objave">
            oko {Math.round(reach.publicationLagMinutes / 60)} h
          </Row>
        )}
      </dl>

      {/* Činjenica, ne sud. Ne tvrdimo da je očitanje pogrešno — kažemo da je promjena veća
          od cijelog raspona pragova koje je odredila agencija, i upućujemo na nju. */}
      {unusual && (
        <p className="mt-4 rounded-[--radius-card] border border-[#7a5a1a] bg-[#241b06] px-3 py-2.5 text-xs leading-relaxed text-[#ffd98a]">
          Promjena od {unusual.changeCm > 0 ? '+' : ''}
          {unusual.changeCm} cm veća je od cijelog raspona pragova ove dionice (
          {unusual.lowestCm}–{unusual.highestCm} cm). Vrijednost je prikazana onako kako ju je
          objavio {reach.agencyName}; prije oslanjanja provjeri kod njih.
        </p>
      )}

      <ThresholdScale reach={reach} />

      {reach.sourceId && reach.stationKey && (
        <Suspense
          fallback={
            <p className="mt-5 text-sm text-[--color-text-muted]">Učitavanje historije…</p>
          }
        >
          <HistoryChart sourceId={reach.sourceId} stationKey={reach.stationKey} />
        </Suspense>
      )}

      {/* Atribucija po dionici, ne u footeru (LEGAL.md §2.1). */}
      <footer className="mt-5 border-t border-[--color-line] pt-3 text-xs text-[--color-text-muted]">
        Izvor:{' '}
        <a
          href={reach.sourceUrl ?? reach.agencyUrl ?? '#'}
          target="_blank"
          rel="noreferrer"
          className="text-[--color-text-soft] underline underline-offset-2 hover:text-[--color-text]"
        >
          {reach.agencyName}
        </a>
      </footer>
    </article>
  )
}

function Chip({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-[--radius-chip] border border-[--color-line] bg-[--color-ink-800] px-2.5 py-1 text-xs">
      {children}
    </span>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="shrink-0 text-[--color-text-muted]">{label}</dt>
      <dd className="text-right">{children}</dd>
    </div>
  )
}
