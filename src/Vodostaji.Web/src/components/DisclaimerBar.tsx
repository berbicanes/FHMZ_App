import { useState } from 'react'

const STORAGE_KEY = 'vodostaji.disclaimer.seen'

/**
 * Traka pri prvoj posjeti (UI.md §4).
 *
 * Tekst je propisan i **ne mijenja se bez dogovora** (CLAUDE.md → Šta NE raditi). Prepisan je
 * doslovno; nijedna riječ nije preformulisana da „bolje zvuči".
 */
export const DISCLAIMER =
  'Ovo nije zvanični sistem upozorenja. Za odluke o evakuaciji pratite nadležnu civilnu zaštitu.'

export function DisclaimerBar() {
  const [dismissed, setDismissed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === 'true'
    } catch {
      // Blokiran localStorage znači da se traka prikaže svaki put. To je ispravna strana
      // greške — bolje je pokazati je viška nego je propustiti.
      return false
    }
  })

  if (dismissed) return null

  const dismiss = () => {
    try {
      localStorage.setItem(STORAGE_KEY, 'true')
    } catch {
      // Ako se ne može zapamtiti, traka se pojavi ponovo. Nije greška vrijedna poruke.
    }
    setDismissed(true)
  }

  return (
    <div
      role="region"
      aria-label="Važno upozorenje"
      className="relative z-30 flex items-start gap-3 border-b border-[#6b4c14] bg-[#241b06] px-4 py-2.5 text-sm"
    >
      <svg
        width="16"
        height="16"
        viewBox="0 0 16 16"
        aria-hidden="true"
        className="mt-0.5 shrink-0 text-[#ffd98a]"
      >
        <path
          d="M8 1.5l6.5 12h-13L8 1.5z"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.4"
          strokeLinejoin="round"
        />
        <path d="M8 6.2v3.4" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
        <circle cx="8" cy="11.6" r="0.8" fill="currentColor" />
      </svg>

      <p className="flex-1 leading-relaxed text-[#ffd98a]">{DISCLAIMER}</p>

      <button
        type="button"
        onClick={dismiss}
        className="shrink-0 rounded-[--radius-chip] border border-[#7a5a1a] px-3 py-1 text-xs text-[#ffd98a] hover:bg-[#33270a]"
      >
        Razumijem
      </button>
    </div>
  )
}

/**
 * Trajna napomena, uvijek vidljiva. Traka iznad se može zatvoriti, a LEGAL.md §2.2 traži da
 * disclaimer **postoji** — ne da se pojavi jednom.
 */
export function PersistentDisclaimer() {
  return (
    <p className="text-xs leading-relaxed text-[--color-text-muted]">
      Podaci nisu zvanični i ne služe za odbranu od poplava. Preuzeti su iz javnih izvora
      nadležnih agencija; izvor je naveden uz svaku dionicu.
    </p>
  )
}
