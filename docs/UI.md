# UI.md — mapa, prikaz i dizajn

## 1. Slojevi mape

1. **Dionice** — poligoni (AVP Sava), obojeni po statusu, WebGL fill
2. **Stanice** — tačke (svi izvori), klikabilne
3. **Poplavne zone** — opciono, toggle, default off

Slojevi različitih agencija su **zasebni layeri sa zasebnom legendom**, nikad stopljeni u jedan.

MapLibre GL JS. Ne ArcGIS JS API.

## 1a. Dionica nije poplavljeno područje

Poligoni AVP Save su **dionice**, u prosjeku **339 km²**, najveća 1041 km², ukupno trećina
teritorije BiH. Jedno očitanje na jednoj vodomjernoj letvi opisuje cijelu tu površinu.

Puna ispuna u crvenom preko 769 km² čita se kao *"sve ovo je pod vodom"*, a znači *"rijeka je
na jednom mjerilu prešla prag"*. Ta razlika je nečija odluka o evakuaciji.

**Zato:** ispuna je slaba (0.18 i niže), a **granica nosi boju punom jačinom**. Ispuna kaže
"ovdje je voda"; obris kaže "na ovo područje se ocjena odnosi". Boja i natpis agencije se ne
mijenjaju — mijenja se samo koliko glasno ispuna govori.

Uz to, i legenda i detalj **izričito pišu** da boja pokazuje stanje rijeke, ne poplavljeno
područje.

**Tačka na mjestu mjerila nije opcija.** `HYDRO_ID` ne povezuje dionice sa registrom stanica
(SOURCES.md §1.7), pa koordinatu mjerila za dionicu nemamo. Tačka u centru poligona bila bi
izmišljena lokacija mjerenja — gore od poligona.

---

## 2. Prikaz starosti podatka — obavezno

Starost se računa iz `MeasuredAt`, relativno prema `Station.ExpectedInterval`.

| Starost | Prikaz |
|---|---|
| < 1× interval | puna boja, puna opacity |
| 1–3× interval | opacity 0.6, tekst "prije Xh" |
| > 3× interval | isprekidana ivica, siva ispuna, "podatak zastario" |
| nema podatka | `#CCCCCC` + **dijagonalna šrafura**, "nema podatka" |

**Šrafura nije dekoracija.** Siva ispuna sama može izgledati kao "mirno". Šrafura ne može. Ovo je vizuelna implementacija zlatnog pravila br. 1.

## 3. Detalj stanice

- Ime rijeke i mjesta
- Trenutni vodostaj u cm, sa strelicom trenda
- Protok (m³/s) ako izvor daje
- Graf 7 / 30 dana
- Pragovi kao horizontalne linije na grafu, **sa imenom agencije koja ih je definisala**
- Timestamp mjerenja, čitljiv ("danas u 14:15", ne ISO string)
- Atribucija: ime agencije + link na izvornu stranicu
- Ako je `Suspect = true`: vidljiva napomena da je očitanje neuobičajeno

## 4. Obavezni elementi

- **Traka pri prvoj posjeti:** *"Ovo nije zvanični sistem upozorenja. Za odluke o evakuaciji pratite nadležnu civilnu zaštitu."*
- **Pretraga po imenu rijeke I imenu mjesta.** Korisnik iz Maglaja kuca "Maglaj", ne "Bosna".
- **Deep link po stanici** (`/stanica/{key}`) — novinari i lokalne grupe će ovo dijeliti; to je glavni kanal distribucije
- **Radi na telefonu iz 2019. na 3G vezi.** Nije opciono — ljudi u poplavi nemaju najbolju vezu.

## 5. Pristupačnost

- **Boja nikad nije jedini nosilac informacije.** Skala od zelene do crvene + daltonizam = ozbiljan problem. Uz boju uvijek ide tekstualni label i/ili obrazac (šrafura, ivica).
- Kontrast minimum WCAG AA
- Vidljivo focus stanje na svemu što se može fokusirati
- `prefers-reduced-motion` poštovan
- Screen reader labeli na markerima mape; mapa ima tabelarnu alternativu

## 6. Dizajn

Ovo je alat koji neko otvara u 3 ujutro kad mu je voda blizu kuće. Ozbiljan i čitljiv prije
svega ostalog.

**Izmjena 2026-08-07, odluka vlasnika projekta.** Raniji tekst je glasio: *„Bez gradijenata,
glassmorphisma, i bilo kakvog dekora koji nije podatak."* Ta zabrana je **ukinuta** — moderan
vizuelni jezik je dozvoljen.

Ukinuta je zabrana, **ne razlog zbog kojeg je postojala.** Zato ostaje kao tvrdo pravilo:

> **Ništa se ne smije takmičiti sa bojama statusa.** One su najsvjetlija i najzasićenija stvar
> na ekranu. Svaki gradijent, sjena, staklo ili akcenat mora biti tiši od njih — ako se pri
> pogledu na ekran oko prvo zaustavi na dekoru, dekor je pogrešan bez obzira koliko je lijep.

- **Podloga:** tamna mapa sa prigušenim terenom.
- **Tipografija:** jedan karakterističan display face za brojeve — vodostaj je heroj ekrana —
  i neutralan, vrlo čitljiv body face. **Brojevi tabularni**, da ne poskakuju pri osvježavanju.
- **Motion:** promjena statusa je jedina promjena koja zaslužuje punu pažnju. Ostale animacije
  su dozvoljene, ali kratke i tihe, i sve poštuju `prefers-reduced-motion`.
- **Dekor ne smije nositi značenje.** Boja, oblik i tekst nose podatak; sve ostalo je površina.

## 7. Copy

- Aktivni glagoli, rečenično pisanje, bez filera
- Prazno stanje je poziv na akciju, ne raspoloženje: *"Nema podataka za ovu stanicu od 07:00. Izvor: RHMZ RS."* — ne *"Ups, nešto je pošlo po zlu."*
- Greške se ne izvinjavaju i nikad nisu nejasne o tome šta se desilo
- Termin koji korisnik vidi je isti kroz cijeli flow: ako lista kaže "Izljevanje iz korita", detalj ne smije reći "Povišen nivo"

---

## 8. Tailwind v4 — `bg-[--token]` je tiha greška

**Nikad ne pisati `bg-[--color-x]`, `text-[--color-x]`, `rounded-[--radius-x]`.**

U Tailwindu v4 uglaste zagrade znače „uzmi ovu vrijednost doslovno", pa se ime varijable
ispiše bez `var()`:

```css
.bg-\[--color-ink-850\] { background-color: --color-ink-850 }   /* nije validan CSS */
```

Pravilo prođe build bez ijednog upozorenja, prođe typecheck, prođe testove — i **ne radi
ništa**. Cijela paleta je tako stajala neprimijenjena kroz 176 klasa u 13 fajlova, a
otkriveno je tek kad je detalj postao plutajuća ploča: bio je proziran, pa se lista ispod
vidjela kroz njega. Do tada je nedostatak pozadine bio nevidljiv jer iza nje ništa nije
stajalo, a tamna `body` pozadina je popunjavala rupu.

Ispravno je koristiti **utilitije koje `@theme` sam generiše**: token `--color-ink-850`
daje `bg-ink-850`, `--radius-card` daje `rounded-card`. Zato su tokeni za tekst nazvani
`--color-fg*`, a ne `--color-text*` — inače bi klasa bila `text-text-muted`.

Provjera koja hvata povratak greške:

```bash
grep -rn "\[--color-\|\[--radius-" src --include="*.tsx"   # mora biti prazno
grep -o "background-color:--" dist/assets/*.css            # mora biti prazno
```

---

## 9. Svijetla tema, samo BiH, i telefon

**Tema je svijetla.** Tokeni su ostali pod imenima `ink-*` iako više ne opisuju tamnu
skalu — opisuju dubinu, a dubina i dalje raste od podloge stranice prema pločama.
Preimenovanje bi bilo stotinu izmjena bez ijedne nove informacije.

**Obrisi su crni** (`--color-line-strong: #0b1018`). Zbog toga se boja statusa preselila iz
ivice u ispunu dionice, a ispuna je podignuta sa 0.16 na 0.32 — ista providnost na bijelom
izgleda upola slabija nego na crnom. Ispuna i dalje **nije puna**: poligon prosječne
površine 339 km² obojen do kraja čita se kao „sve ovo je pod vodom", a znači „rijeka je na
jednom mjerilu prešla prag".

**Upozorenje i greška su tokeni** (`--color-warn-*`, `--color-danger-*`), ne heksovi po
komponentama. Pri prelasku teme je svaki upisani heks morao biti nađen ručno, a propušteni
ostane tamna mrlja usred bijele ploče — i to baš na poruci upozorenja.

**Mapa pokriva samo BiH.** Poligon preko cijelog svijeta sa granicom države kao rupom
(`/geo/bih.json`, 13 kB, uz aplikaciju a ne sa tuđeg servera). Sloj stoji iznad imena sa
podloge, pa gasi i teren i natpise izvan granice odjednom. Bez maske su susjedne države
jednako naglašene kao BiH, pa se ne vidi dokle pokrivenost uopšte seže.

**Na telefonu je donja ploča, ne podijeljen ekran.** Ekran napola daje dvije neupotrebljive
polovine: mapu presitnu da se prstom išta pogodi, i listu u koju stanu tri reda. Ploča ima
tri položaja — `peek` (sažetak), `half` (lista), `full` (detalj) — i otvara se do kraja kad
se otvori detalj. Prekidači mape se sa mape sele u ploču, jer bi ih inače prekrila.

**Odskakanje preko kraja je ugašeno.** `overscroll-behavior: none` na dokumentu i `contain`
na kontejnerima koji skroluju. Bez toga skrol preko dna liste povuče cijeli dokument i
ispod se ukaže gola pozadina.
