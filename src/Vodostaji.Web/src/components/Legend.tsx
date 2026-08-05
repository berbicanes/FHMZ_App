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

      <p className="mt-3 text-xs leading-relaxed text-[--color-text-muted]">
        Bljeđa ispuna znači da je jedno ili više mjerenja izostalo; isprekidana ivica da su
        izostala više od tri. Starost se računa od trenutka kad podatak realno može stići,
        jer izvor objavljuje sa zastojem — svjež podatak zato ne znači i podatak od maloprije.
      </p>
    </section>
  )
}
