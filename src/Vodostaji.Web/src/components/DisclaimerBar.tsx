import { useState } from 'react'

const STORAGE_KEY = 'vodostaji.disclaimer.seen'

/**
 * Traka pri prvoj posjeti (UI.md §4).
 *
 * Tekst je propisan i **ne mijenja se bez dogovora** (CLAUDE.md → Šta NE raditi).
 * Prepisan je doslovno; nijedna riječ nije preformulisana da "bolje zvuči".
 */
export const DISCLAIMER =
  'Ovo nije zvanični sistem upozorenja. Za odluke o evakuaciji pratite nadležnu civilnu zaštitu.'

export function DisclaimerBar() {
  const [dismissed, setDismissed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === 'true'
    } catch {
      // Blokiran localStorage znači da se traka prikaže svaki put. To je ispravna
      // strana greške — bolje je pokazati je viška nego je propustiti.
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
      className="flex items-start gap-3 border-b border-[#5a3d00] bg-[#3a2800] px-4 py-3 text-sm"
    >
      <p className="flex-1 leading-relaxed text-[#ffd98a]">{DISCLAIMER}</p>
      <button
        type="button"
        onClick={dismiss}
        className="shrink-0 rounded border border-[#7a5a1a] px-3 py-1 text-[#ffd98a] hover:bg-[#4a3400]"
      >
        Razumijem
      </button>
    </div>
  )
}

/**
 * Trajna napomena, uvijek vidljiva. Traka iznad se može zatvoriti, a LEGAL.md §2.2 traži
 * da disclaimer postoji — ne da se pojavi jednom.
 */
export function PersistentDisclaimer() {
  return (
    <p className="text-xs leading-relaxed text-[--color-text-muted]">
      Podaci nisu zvanični i ne služe za odbranu od poplava. Preuzeti su iz javnih izvora
      nadležnih agencija; izvor je naveden uz svaku dionicu.
    </p>
  )
}
