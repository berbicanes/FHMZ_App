import { describe, expect, it } from 'vitest'
import {
  cadenceLabel,
  cm,
  formatDuration,
  highestReached,
  nextThresholdAbove,
  numericThresholds,
  ratePerHour,
  seriesSummary,
  signedCm,
} from './derived'
import type { ReachProperties, ReachThreshold } from '../api/types'

const thresholds = (...values: number[]): ReachThreshold[] =>
  values.map((valueCm) => ({ label: `prag ${valueCm}`, valueCm, level: null }))

const reach = (properties: Partial<ReachProperties>): ReachProperties =>
  properties as unknown as ReachProperties

describe('numericThresholds', () => {
  it('odbacuje pragove bez vrijednosti i sortira ostatak', () => {
    // Izvori šalju i pragove sa imenom ali bez broja. Takav prag se ne da nacrtati.
    const mixed = [
      { label: 'bez broja', valueCm: undefined, level: null },
      ...thresholds(300, 124),
    ] as ReachThreshold[]

    expect(numericThresholds(mixed).map((t) => t.valueCm)).toEqual([124, 300])
  })

  it('prazan ulaz daje prazan izlaz, ne grešku', () => {
    expect(numericThresholds(null)).toEqual([])
    expect(numericThresholds(undefined)).toEqual([])
  })
})

describe('nextThresholdAbove', () => {
  it('nalazi najbliži prag iznad vrijednosti i tačnu razliku', () => {
    // Zenica, stvarni pragovi.
    const result = nextThresholdAbove(37, thresholds(124, 154, 344, 394))

    expect(result?.threshold.valueCm).toBe(124)
    expect(result?.distanceCm).toBe(87)
  })

  it('preskače pragove koje je vrijednost već prešla', () => {
    const result = nextThresholdAbove(200, thresholds(124, 154, 344, 394))

    expect(result?.threshold.valueCm).toBe(344)
    expect(result?.distanceCm).toBe(144)
  })

  it('nema šta vratiti kad je vrijednost iznad svih pragova', () => {
    // Ovo je poplava preko najvišeg praga. Napomena „još X cm" bi ovdje bila apsurdna.
    expect(nextThresholdAbove(500, thresholds(124, 394))).toBeNull()
  })

  it('prag tačno na vrijednosti se broji kao dostignut, ne kao sljedeći', () => {
    expect(nextThresholdAbove(124, thresholds(124, 154))?.threshold.valueCm).toBe(154)
  })

  it('bez vrijednosti nema računa', () => {
    expect(nextThresholdAbove(null, thresholds(124))).toBeNull()
    expect(nextThresholdAbove(undefined, thresholds(124))).toBeNull()
  })

  it('bez pragova nema računa — ne izmišljamo skalu', () => {
    // AVPJM i FHMZBIH ne objavljuju stupanj opasnosti javno (SOURCES.md §2.1).
    expect(nextThresholdAbove(37, [])).toBeNull()
    expect(nextThresholdAbove(37, null)).toBeNull()
  })
})

describe('highestReached', () => {
  it('imenuje najviši prag koji je vrijednost dostigla', () => {
    expect(highestReached(200, thresholds(124, 154, 344))?.valueCm).toBe(154)
  })

  it('vrijednost tačno na pragu ga je dostigla', () => {
    expect(highestReached(154, thresholds(124, 154, 344))?.valueCm).toBe(154)
  })

  it('vrijednost ispod svih pragova nije dostigla nijedan', () => {
    // Ljeti su rijeke duboko ispod najnižeg praga; to je normalno stanje, ne greška.
    expect(highestReached(37, thresholds(124, 154))).toBeNull()
  })

  it('bez vrijednosti ne tvrdi ništa', () => {
    expect(highestReached(null, thresholds(124))).toBeNull()
  })
})

describe('ratePerHour', () => {
  it('dijeli promjenu stvarnim razmakom, ne pretpostavljenim satom', () => {
    // 12 cm za 3 sata je 4 cm/h. Dijeljenje fiksnim satom bi dalo 12 — trostruko brže.
    expect(ratePerHour(reach({ changeCm: 12, changeOverMinutes: 180 }))).toBe(4)
  })

  it('pad daje negativnu brzinu', () => {
    expect(ratePerHour(reach({ changeCm: -30, changeOverMinutes: 60 }))).toBe(-30)
  })

  it('odbija prekratak razmak, jer je rezultat artefakt zaokruživanja', () => {
    // 4 cm u 2 minute bi dalo 120 cm/h — to nije brzina rijeke nego greška vremena.
    expect(ratePerHour(reach({ changeCm: 4, changeOverMinutes: 2 }))).toBeNull()
  })

  it('bez promjene ili bez razmaka ne računa ništa', () => {
    expect(ratePerHour(reach({ changeCm: null, changeOverMinutes: 60 }))).toBeNull()
    expect(ratePerHour(reach({ changeCm: 5, changeOverMinutes: null }))).toBeNull()
    expect(ratePerHour(reach({}))).toBeNull()
  })
})

describe('cadenceLabel', () => {
  it('razlikuje izmjeren ritam od pretpostavljenog', () => {
    // Ta razlika nosi cijelu ocjenu svježine — korisnik mora vidjeti koja je u igri.
    expect(cadenceLabel(reach({ expectedIntervalMinutes: 60, intervalIsMeasured: true }))).toContain(
      'izmjereno',
    )
    expect(
      cadenceLabel(reach({ expectedIntervalMinutes: 60, intervalIsMeasured: false })),
    ).toContain('pretpostavka')
  })

  it('bez poznatog intervala ne kaže ništa', () => {
    expect(cadenceLabel(reach({ expectedIntervalMinutes: 0, intervalIsMeasured: false }))).toBeNull()
  })
})

describe('formatDuration', () => {
  it('bira jedinicu po veličini', () => {
    expect(formatDuration(45)).toBe('45 min')
    expect(formatDuration(60)).toBe('1 h')
    expect(formatDuration(90)).toBe('1.5 h')
    expect(formatDuration(1440)).toBe('1 dana')
  })
})

describe('seriesSummary', () => {
  it('opisuje niz bez ocjenjivanja', () => {
    expect(seriesSummary([10, 44, 7, 21])).toEqual({ min: 7, max: 44, span: 37, count: 4 })
  })

  it('prazan niz nema sažetak', () => {
    expect(seriesSummary([])).toBeNull()
  })
})

describe('cm i signedCm', () => {
  it('skida lažnu preciznost izvora', () => {
    // ArcGIS vraća 17.7000008; prikazati sve cifre znači tvrditi tačnost koje nema.
    expect(cm(17.7000008)).toBe('17.7')
    expect(cm(124)).toBe('124')
  })

  it('predznak se ispisuje uvijek jer nosi značenje', () => {
    expect(signedCm(3)).toBe('+3')
    expect(signedCm(-3)).toBe('−3')
    expect(signedCm(0)).toBe('0')
  })
})
