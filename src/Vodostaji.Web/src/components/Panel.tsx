import { useId, useState, type ReactNode } from 'react'

/**
 * Sklopiva sekcija u bočnoj ploči.
 *
 * Ploča nosi legende tri agencije, stanje izvora, pokrivenost, disclaimer i tabelu svih
 * dionica. Prije redizajna je sve stajalo otvoreno, istog vizuelnog težišta, i čitalo se
 * kao zid teksta — ništa nije vodilo oko. Sklapanje daje hijerarhiju bez skrivanja: sve je
 * i dalje u DOM-u i dostupno tastaturi i čitaču ekrana.
 */
export function Section({
  title,
  children,
  defaultOpen = true,
  badge,
}: {
  title: string
  children: ReactNode
  defaultOpen?: boolean
  badge?: ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)
  const id = useId()

  return (
    <section className="border-b border-[--color-line] last:border-b-0">
      <h2>
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          aria-expanded={open}
          aria-controls={id}
          className="flex w-full items-center gap-2 px-4 py-3 text-left hover:bg-[--color-ink-850]"
        >
          <span className="eyebrow flex-1">{title}</span>
          {badge}
          <svg
            width="12"
            height="12"
            viewBox="0 0 12 12"
            aria-hidden="true"
            className="shrink-0 text-[--color-text-muted] transition-transform duration-200"
            style={{ transform: open ? 'rotate(90deg)' : 'none' }}
          >
            <path
              d="M4 2l4 4-4 4"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.6"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      </h2>

      {/* `hidden` umjesto uklanjanja iz stabla: tabela je obavezna alternativa mapi
          (UI.md §5), pa mora ostati pretraživa i dostupna i kad je sekcija sklopljena. */}
      <div id={id} hidden={!open} className="px-4 pb-4">
        {children}
      </div>
    </section>
  )
}
