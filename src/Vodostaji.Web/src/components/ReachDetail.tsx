import { lazy, Suspense, type ReactNode } from 'react'
import type { ReachProperties } from '../api/types'
import {
  cadenceLabel,
  cm,
  formatDuration,
  highestReached,
  nextThresholdAbove,
  ratePerHour,
  signedCm,
} from '../lib/derived'
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
import { StatusDot } from './StatusMark'
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
 * koliko je vode, koliko fali do praga, kad je mjereno, pa tek onda historija.
 *
 * Sve izvedene brojke ovdje (razlika do praga, cm/h) su **aritmetika nad vrijednostima koje
 * je agencija objavila** — vidi `lib/derived.ts`. Nijedna ne uvodi novu kategoriju ni ocjenu;
 * status i dalje dolazi isključivo od izvora (zlatno pravilo 3).
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
  const stale = freshnessOf(reach) === 'stale'

  const next = nextThresholdAbove(reach.valueCm, reach.thresholds)
  const reached = highestReached(reach.valueCm, reach.thresholds)
  const rate = ratePerHour(reach)
  const cadence = cadenceLabel(reach)

  return (
    <article aria-label={`Detalj: ${reach.name}`} className="pb-6">
      {/* Zaglavlje ostaje na vrhu pri skrolanju: u dugom detalju se inače izgubi koja se
          dionica gleda, a to je jedina informacija koja mora biti stalno prisutna. */}
      <header className="sticky top-0 z-10 border-b border-line bg-ink-850/95 px-4 py-3 backdrop-blur">
        <div className="flex items-start gap-3">
          <button
            type="button"
            onClick={onClose}
            aria-label="Nazad na pregled"
            className="mt-0.5 shrink-0 rounded-chip border border-line-strong p-1.5 text-fg-soft hover:bg-ink-800 hover:text-fg"
          >
            <svg width="14" height="14" viewBox="0 0 14 14" aria-hidden="true">
              <path
                d="M8.5 3L4.5 7l4 4"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.7"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>

          <div className="min-w-0 flex-1">
            <h2 className="truncate text-base leading-tight font-semibold">{reach.name}</h2>
            <p className="mt-0.5 truncate text-xs text-fg-muted">
              {reach.river ? `Rijeka ${reach.river} · ` : ''}
              {reach.agencyName}
            </p>
          </div>
        </div>
      </header>

      <div className="px-4 pt-4">
        {/* Vodostaj kao heroj, sa stanjem odmah uz njega. */}
        <div className="flex items-end justify-between gap-4">
          <div className="min-w-0">
            {hasValue ? (
              <p className="numeric flex items-baseline gap-2 leading-none">
                <span className="text-[3.25rem] font-bold xl:text-[3.75rem]">
                  {cm(reach.valueCm as number)}
                </span>
                <span className="text-lg font-medium text-fg-muted">cm</span>
              </p>
            ) : (
              <p className="numeric text-3xl leading-none font-bold text-fg-muted">
                Nema podatka
              </p>
            )}

            <div className="mt-2.5 flex items-center gap-2 text-sm">
              <StatusDot reach={reach} size={10} />
              <span>{reach.levelLabel}</span>
            </div>
          </div>

          {hasValue && trend !== 'unknown' && (
            <div className="shrink-0 text-right">
              <span
                aria-hidden="true"
                className="block text-2xl leading-none text-fg-soft"
              >
                {trendArrow(trend)}
              </span>
              <span className="mt-1 block text-xs text-fg-muted">
                {trendLabel(trend)}
              </span>
            </div>
          )}
        </div>

        {/*
         * Negativan vodostaj nije greška, i to mora pisati.
         *
         * Jala u Tuzli stoji na −23 cm. Broj se mjeri **od nule vodomjerne letve**, a ne od
         * dna korita; nula letve je proizvoljno odabrana visina (za Tuzlu 221.921 m n.v.),
         * pa voda ispod nje daje negativan broj. Bez ove rečenice minus izgleda kao kvar u
         * aplikaciji, a onda i sve ostalo na ekranu gubi kredibilitet.
         */}
        {hasValue && (reach.valueCm as number) < 0 && (
          <p className="mt-3 rounded-card border border-line bg-ink-900 px-3 py-2.5 text-xs leading-relaxed text-fg-soft">
            Minus ne znači grešku. Vodostaj se mjeri u odnosu na nulu vodomjerne letve, ne od
            dna korita, pa voda ispod te nule daje negativan broj.
            {reach.gaugeZeroMetres !== null && reach.gaugeZeroMetres !== undefined && (
              <>
                {' '}
                Nula letve na ovoj stanici je na{' '}
                <span className="tabular">{reach.gaugeZeroMetres} m</span> nadmorske visine.
              </>
            )}
          </p>
        )}

        {/* Podatak koji je zastario mora to reći iznad svega ostalog, a ne u fusnoti. */}
        {stale && (
          <p className="mt-3 rounded-card border border-[#7a5a1a] bg-[#241b06] px-3 py-2 text-xs leading-relaxed text-[#ffd98a]">
            Ovo očitanje je starije od tri očekivana ciklusa ove stanice. Prikazano je onako
            kako je objavljeno, ali vjerovatno ne opisuje trenutno stanje.
          </p>
        )}

        {/*
         * Razdaljina do sljedećeg praga.
         *
         * Ovo je najkorisniji jedan broj na ekranu i razlog zbog kojeg neko otvori detalj.
         * Oduzimanje dvije vrijednosti agencije — ne ocjena. Zato nigdje ne piše „opasno"
         * ni „približava se"; piše koliko centimetara, i ko je taj prag postavio.
         */}
        {next && (
          <div className="mt-4 rounded-card border border-line bg-ink-900 px-3.5 py-3">
            <p className="eyebrow">Do sljedećeg praga</p>
            <p className="numeric mt-1.5 text-2xl leading-none font-semibold">
              {cm(next.distanceCm)}
              <span className="font-sans text-sm font-normal text-fg-muted"> cm</span>
            </p>
            <p className="mt-1.5 text-xs leading-relaxed text-fg-soft">
              „{next.threshold.label}" je na{' '}
              <span className="tabular">{cm(next.threshold.valueCm)} cm</span>.
              {reach.thresholdsDefinedBy ? ` Prag definiše ${reach.thresholdsDefinedBy}.` : ''}
            </p>
          </div>
        )}

        {/* Kad je vrijednost iznad svih pragova, „još X cm" nema smisla — kaže se gdje jeste. */}
        {!next && reached && (
          <div className="mt-4 rounded-card border border-line bg-ink-900 px-3.5 py-3">
            <p className="eyebrow">Iznad svih pragova</p>
            <p className="mt-1.5 text-sm leading-relaxed text-fg-soft">
              Vrijednost je iznad najvišeg praga koji {reach.thresholdsDefinedBy ?? 'agencija'}{' '}
              objavljuje — „{reached.label}" na{' '}
              <span className="tabular">{cm(reached.valueCm)} cm</span>.
            </p>
          </div>
        )}

        {/* Bez ovoga se obojena dionica može pročitati kao poplavljeno područje. Stoji uz
            vodostaj, ne u dnu, jer se čita u istom pogledu. */}
        {reach.sourceId === 'avp-sava' && (
          <p className="mt-4 rounded-card border border-line bg-ink-900 px-3 py-2.5 text-xs leading-relaxed text-fg-soft">
            Ocjena se odnosi na dionicu rijeke, mjerenu na hidrološkim stanicama na njoj — nije
            prikaz poplavljenog područja.
          </p>
        )}

        {/* Činjenica, ne sud. Ne tvrdimo da je očitanje pogrešno — kažemo da je promjena veća
            od cijelog raspona pragova koje je odredila agencija, i upućujemo na nju. */}
        {unusual && (
          <p className="mt-4 rounded-card border border-[#7a5a1a] bg-[#241b06] px-3 py-2.5 text-xs leading-relaxed text-[#ffd98a]">
            Promjena od {signedCm(unusual.changeCm)} cm veća je od cijelog raspona pragova ove
            dionice ({unusual.lowestCm}–{unusual.highestCm} cm). Vrijednost je prikazana onako
            kako ju je objavio {reach.agencyName}; prije oslanjanja provjeri kod njih.
          </p>
        )}

        {/* Činjenice u kartice umjesto u redove sa tačkicama: u uskoj koloni se dva stupca
            kratkih parova čitaju brže od osam redova preko cijele širine. */}
        <dl className="mt-4 grid grid-cols-2 gap-1.5">
          <Fact label="Mjereno" wide={!measured}>
            {measured ? (
              <>
                {measured}
                <span className="mt-0.5 block text-xs text-fg-muted">
                  {freshnessLabel(reach)}
                </span>
              </>
            ) : (
              (reach.noDataReason ?? 'Izvor nije poslao vrijednost.')
            )}
          </Fact>

          {reach.changeCm !== null && reach.changeCm !== undefined && (
            <Fact label="Promjena">
              <span className="tabular">{signedCm(reach.changeCm)} cm</span>
              <span className="mt-0.5 block text-xs text-fg-muted">
                {changeWindow(reach.changeOverMinutes) ?? 'u odnosu na prethodno očitanje'}
              </span>
            </Fact>
          )}

          {rate !== null && (
            <Fact label="Brzina">
              <span className="tabular">{signedCm(rate)} cm/h</span>
              <span className="mt-0.5 block text-xs text-fg-muted">
                iz zadnja dva očitanja
              </span>
            </Fact>
          )}

          {reach.previousValueCm !== null && reach.previousValueCm !== undefined && (
            <Fact label="Prethodno">
              <span className="tabular">{cm(reach.previousValueCm)} cm</span>
              <span className="mt-0.5 block text-xs text-fg-muted">
                {formatMeasuredAt(reach.previousMeasuredAt) ?? 'vrijeme nije objavljeno'}
              </span>
            </Fact>
          )}

          {cadence && (
            <Fact label="Ritam mjerenja" wide>
              {cadence}
            </Fact>
          )}

          {reach.publicationLagMinutes > 0 && (
            <Fact label="Kašnjenje objave" wide>
              Agencija objavi mjerenje oko {formatDuration(reach.publicationLagMinutes)} nakon
              što je snimljeno.
            </Fact>
          )}

          {/* Doslovan tekst agencije. AVPJM i FHMZBIH ga ne šalju, pa prazan red ne stoji —
              prazno polje uz natpis izgleda kao da smo nešto izgubili, a nema šta pokazati. */}
          {reach.statusLabelOriginal && reach.statusLabelOriginal.trim().length > 0 && (
            <Fact label="Izvor kaže" wide>
              „{reach.statusLabelOriginal}"
            </Fact>
          )}
        </dl>

        <ThresholdScale reach={reach} />

        {reach.sourceId && reach.stationKey && (
          <Suspense
            fallback={
              <div className="mt-5 h-[320px] animate-pulse rounded-card bg-ink-800" />
            }
          >
            <HistoryChart sourceId={reach.sourceId} stationKey={reach.stationKey} />
          </Suspense>
        )}

        {/* Atribucija po dionici, ne u footeru (LEGAL.md §2.1). */}
        <footer className="mt-6 border-t border-line pt-3.5">
          <a
            href={reach.sourceUrl ?? reach.agencyUrl ?? '#'}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 rounded-chip border border-line-strong px-3 py-1.5 text-xs text-fg-soft hover:bg-ink-800 hover:text-fg"
          >
            Otvori kod izvora — {reach.agencyName}
            <svg width="11" height="11" viewBox="0 0 12 12" aria-hidden="true">
              <path
                d="M4 2h6v6M10 2L3 9"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </a>
        </footer>
      </div>
    </article>
  )
}

function Fact({
  label,
  children,
  wide = false,
}: {
  label: string
  children: ReactNode
  wide?: boolean
}) {
  return (
    <div
      className={`rounded-card border border-line bg-ink-900 px-3 py-2.5 ${
        wide ? 'col-span-2' : ''
      }`}
    >
      <dt className="eyebrow">{label}</dt>
      <dd className="mt-1 text-sm leading-snug">{children}</dd>
    </div>
  )
}
