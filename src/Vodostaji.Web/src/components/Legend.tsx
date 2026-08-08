/**
 * Legende. **Jedna po agenciji**, nikad zajednička.
 *
 * AVP Sava ima skalu od pet stupnjeva sa zvaničnim bojama; AVPJM i FHMZBIH nemaju nijedan,
 * jer stupanj opasnosti ne objavljuju. Zajednička legenda bi morala izmisliti nešto za jednu
 * od njih (CLAUDE.md → Šta NE raditi).
 */
const SAVA_ENTRIES = [
  { color: '#38a800', label: 'Normalno' },
  { color: '#ffff00', label: 'Izljevanje iz korita' },
  { color: '#ffaa00', label: 'Poplave' },
  { color: '#e60000', label: 'Značajne poplave' },
] as const

const HATCH =
  'repeating-linear-gradient(45deg, #8a97a8 0 2px, transparent 2px 5px)'

function Swatch({
  color,
  hatched = false,
  ring = false,
  round = false,
}: {
  color: string
  hatched?: boolean
  ring?: boolean
  round?: boolean
}) {
  return (
    <span
      aria-hidden="true"
      className={`h-3.5 w-3.5 shrink-0 ${round ? 'rounded-full' : 'rounded-[3px]'}`}
      style={{
        backgroundColor: color,
        backgroundImage: hatched ? HATCH : undefined,
        border: ring ? '2px solid #0b1018' : '1px solid rgb(0 0 0 / 0.45)',
      }}
    />
  )
}

export function SavaLegend({ agencyName }: { agencyName: string }) {
  return (
    <div>
      <p className="mb-2 text-xs text-fg-muted">{agencyName}</p>

      <ul className="space-y-1.5">
        {SAVA_ENTRIES.map((entry) => (
          <li key={entry.label} className="flex items-center gap-2.5 text-sm">
            <Swatch color={entry.color} />
            <span>{entry.label}</span>
          </li>
        ))}
        <li className="flex items-center gap-2.5 text-sm">
          <Swatch color="#cccccc" hatched />
          <span>Nema podatka</span>
        </li>
      </ul>

      <p className="mt-3 rounded-card border border-line bg-ink-850 px-3 py-2.5 text-xs leading-relaxed">
        <strong className="font-semibold">
          Boja pokazuje stanje rijeke na toj dionici, ne poplavljeno područje.
        </strong>{' '}
        Obris omeđuje dionicu na koju se ocjena odnosi — u prosjeku oko 340 km². Ocjena dolazi
        sa mjerila na toj rijeci.
      </p>

      <p className="mt-2 text-xs leading-relaxed text-fg-muted">
        Bljeđa boja znači da je jedno ili više mjerenja izostalo; isprekidana ivica da su
        izostala više od tri. Starost se računa od trenutka kad podatak realno može stići, jer
        izvor objavljuje sa zastojem.
      </p>
    </div>
  )
}

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
    <div>
      <p className="mb-2 text-xs text-fg-muted">{agencyName}</p>

      <ul className="space-y-1.5">
        <li className="flex items-center gap-2.5 text-sm">
          {/* Oblik, ne samo boja (UI.md §5) — legenda mora izgledati kao mapa. */}
          <Swatch color={color} ring={ring} round />
          <span>Izmjereno, bez ocjene opasnosti</span>
        </li>
        <li className="flex items-center gap-2.5 text-sm">
          <Swatch color="#cccccc" hatched round />
          <span>Nema podatka</span>
        </li>
      </ul>

      <p className="mt-3 text-xs leading-relaxed text-fg-muted">{note}</p>
    </div>
  )
}
