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
