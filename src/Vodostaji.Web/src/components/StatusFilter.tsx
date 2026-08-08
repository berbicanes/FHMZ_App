import type { Bucket, BucketKey } from '../lib/levels'

/**
 * Sažetak stanja, i ujedno filter.
 *
 * Prvo je **pregled**: koliko ih je u kojem stanju, u jednom pogledu, prije ijednog klika.
 * To je odgovor na pitanje sa kojim se otvara aplikacija — „ima li igdje problema" — i do
 * sada se do njega dolazilo skrolanjem kroz 77 redova.
 *
 * Filter je druga funkcija istog elementa: gasi grupe **samo iz prikaza**. Tabela ispod i
 * podaci ostaju netaknuti, i traka uvijek pokazuje pun broj u svakoj grupi — i u onoj koja
 * je ugašena. Filter koji krije i vlastiti broj ostavlja korisnika u uvjerenju da nečega
 * nema, a to je greška koju ova aplikacija ne smije praviti.
 */
export function StatusFilter({
  buckets,
  active,
  onToggle,
  onReset,
}: {
  buckets: Bucket[]
  /** `null` znači „sve prikazano" — različito od praznog skupa, koji se ne dopušta. */
  active: BucketKey[] | null
  onToggle: (key: BucketKey) => void
  onReset: () => void
}) {
  if (buckets.length === 0) return null

  const filtering = active !== null

  return (
    <section aria-label="Stanje po grupama" className="px-4 pt-3.5 pb-4">
      <div className="mb-2.5 flex items-baseline justify-between gap-3">
        <h2 className="eyebrow">Stanje</h2>

        {filtering && (
          <button
            type="button"
            onClick={onReset}
            className="text-xs text-fg-muted underline underline-offset-2 hover:text-fg"
          >
            Prikaži sve
          </button>
        )}
      </div>

      <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3">
        {buckets.map((bucket) => {
          const on = active === null || active.includes(bucket.key)

          return (
            <button
              key={bucket.key}
              type="button"
              onClick={() => onToggle(bucket.key)}
              aria-pressed={on}
              title={`${bucket.label} — ${bucket.count}`}
              className={`relative overflow-hidden rounded-card border px-2.5 py-2 text-left transition-colors ${
                on
                  ? 'border-line-strong/70 bg-ink-800'
                  : 'border-line bg-transparent opacity-45 hover:opacity-70'
              }`}
            >
              {/* Boja stoji kao traka uz ivicu, ne kao ispuna dugmeta: ispuna u punoj boji
                  statusa bi u traci bila glasnija nego ista ta boja na mapi. */}
              <span
                aria-hidden="true"
                className="absolute inset-y-0 left-0 w-[3px]"
                style={{
                  backgroundColor: bucket.color,
                  // Grupa bez podatka nosi šrafuru i ovdje — isti obrazac kao na mapi, pa
                  // boja nigdje nije jedini nosilac (UI.md §5).
                  backgroundImage:
                    bucket.key === 'NoData'
                      ? 'repeating-linear-gradient(45deg, #5c6470 0 2px, transparent 2px 5px)'
                      : undefined,
                }}
              />

              <span className="numeric block pl-1.5 text-xl leading-none font-semibold">
                {bucket.count}
              </span>
              <span className="mt-1 block pl-1.5 text-[11px] leading-tight text-fg-muted">
                {bucket.label}
              </span>
            </button>
          )
        })}
      </div>
    </section>
  )
}
