/**
 * Legenda jedne agencije. Boje i natpisi su iz njihovog renderera (SOURCES.md §1.1).
 *
 * Sljedeći izvor donosi **svoju** legendu — stapanje agencija u jedan sloj sa jednom
 * legendom je zabranjeno (UI.md §1).
 */
const ENTRIES = [
  { color: '#38a800', label: 'Normalno' },
  { color: '#ffff00', label: 'Izljevanje iz korita' },
  { color: '#ffaa00', label: 'Poplave' },
  { color: '#e60000', label: 'Značajne poplave' },
] as const

/**
 * Legenda Jadranskog sliva. **Zasebna**, jer je i sloj zaseban.
 *
 * AVPJM ne objavljuje stupanj opasnosti javnosti, pa ovdje nema skale od zelene do crvene —
 * ima samo razlika između "izmjereno" i "nema podatka". Plava je birana tako da se ne
 * pomiješa sa skalom AVP Save; ista nijansa bi tvrdila nešto što agencija nije rekla.
 */
export function PointSourceLegend({
  agencyName,
  color,
  ring,
  note,
}: {
  agencyName: string
  color: string
  /** Svijetli prsten — isti oblik kojim se ta agencija crta na mapi. */
  ring: boolean
  note: string
}) {
  return (
    <section aria-label={`Legenda — ${agencyName}`}>
      <h2 className="mb-2 text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
        Legenda — {agencyName}
      </h2>

      <ul className="space-y-1.5">
        <li className="flex items-center gap-2.5 text-sm">
          {/* Oblik, ne samo boja (UI.md §5) — legenda mora izgledati kao mapa. */}
          <span
            aria-hidden="true"
            className="h-3.5 w-3.5 shrink-0 rounded-full"
            style={{
              backgroundColor: color,
              border: ring ? '2px solid #e6edf3' : '1px solid rgb(0 0 0 / 0.4)',
            }}
          />
          <span>Izmjereno, bez ocjene opasnosti</span>
        </li>
        <li className="flex items-center gap-2.5 text-sm">
          <span
            aria-hidden="true"
            className="h-3.5 w-3.5 shrink-0 rounded-full border border-black/40"
            style={{
              backgroundColor: '#cccccc',
              backgroundImage:
                'repeating-linear-gradient(45deg, #5c6470 0 2px, transparent 2px 5px)',
            }}
          />
          <span>Nema podatka</span>
        </li>
      </ul>

      <p className="mt-3 text-xs leading-relaxed text-[--color-text-muted]">{note}</p>
    </section>
  )
}

export function Legend({ agencyName }: { agencyName: string }) {
  return (
    <section aria-label="Legenda">
      <h2 className="mb-2 text-xs font-semibold tracking-wide text-[--color-text-muted] uppercase">
        Legenda — {agencyName}
      </h2>

      <ul className="space-y-1.5">
        {ENTRIES.map((entry) => (
          <li key={entry.label} className="flex items-center gap-2.5 text-sm">
            <span
              aria-hidden="true"
              className="status-swatch h-3.5 w-3.5 shrink-0 rounded-sm border border-black/40"
              style={{ backgroundColor: entry.color }}
            />
            <span>{entry.label}</span>
          </li>
        ))}

        {/* Nema podatka nosi šrafuru i ovdje, ne samo na mapi. Ista stvar mora izgledati
            isto na oba mjesta, inače legenda ne objašnjava mapu. */}
        <li className="flex items-center gap-2.5 text-sm">
          <span
            aria-hidden="true"
            className="h-3.5 w-3.5 shrink-0 rounded-sm border border-black/40"
            style={{
              backgroundColor: '#cccccc',
              backgroundImage:
                'repeating-linear-gradient(45deg, #5c6470 0 2px, transparent 2px 5px)',
            }}
          />
          <span>Nema podatka</span>
        </li>
      </ul>

      <p className="mt-3 rounded border border-[--color-border] bg-[--color-surface] p-2.5 text-xs leading-relaxed">
        <strong className="font-semibold">Boja pokazuje stanje rijeke na toj dionici, ne
        poplavljeno područje.</strong>{' '}
        Obris omeđuje dionicu na koju se ocjena odnosi — u prosjeku oko 340 km². Ocjena dolazi
        sa mjerila na toj rijeci, a ne znači da je cijelo područje pod vodom.
      </p>

      <p className="mt-2 text-xs leading-relaxed text-[--color-text-muted]">
        Bljeđa boja znači da je jedno ili više mjerenja izostalo; isprekidana ivica da su
        izostala više od tri. Starost se računa od trenutka kad podatak realno može stići,
        jer izvor objavljuje sa zastojem — svjež podatak zato ne znači i podatak od maloprije.
      </p>
    </section>
  )
}
