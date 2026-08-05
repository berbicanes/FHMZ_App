# ROADMAP.md

Faza nije gotova dok svi izlazni kriteriji nisu ispunjeni. Pri prelasku faze ažuriraj liniju "Trenutno stanje" u `CLAUDE.md`.

---

## Faza 0 — Probe ✅ **ZAVRŠENA 2026-08-04**

Verifikacija stvarnosti prije ijedne linije adaptera.

- [x] `tools/Probe` konzolna aplikacija
- [x] `?f=json` pozvan na svim ArcGIS servisima i slojevima iz `SOURCES.md` — 26 servisa,
      6 foldera vraća `Token Required`, `Crowdsource_Flood_public` nije pokrenut
- [x] Reprezentativna stranica snimljena za AVPJM, FHMZBIH, RHMZ RS i Vode Srpske
- [x] RHMZ RS sondiran — adrese nađene čitanjem njihovih stranica i skripti. Ima JSON API,
      ali nijedan endpoint ne servira vodostaje; mapa koja bi ih dala je pokvarena
- [x] Fixtures u `tests/fixtures/<source>/<layer>-YYYY-MM-DD.json`
- [x] `SOURCES.md` regenerisan sa stvarnim poljima, tipovima, domenima, renderer bojama
- [x] Upozorenje "NEVERIFIKOVANO" uklonjeno, datum verifikacije upisan

**Faza 0 je zatvorena. Ne piši adaptere prije nego se pređe u Fazu 1.**

**Nose se u Fazu 1:** zona `DATE_TIME`-a kod AVP Save nije riješena, a veza
`HYDRO_ID` ↔ `HID_ID` nije potvrđena. Oboje je opisano u `SOURCES.md` → Otvorena pitanja.

---

## Faza 1 — Vertikalni presjek ✅ **ZAVRŠENA 2026-08-05**

Samo AVP Sava. Cilj je funkcionalna mapa sjeverne BiH od kraja do kraja.

- [x] Solution, docker compose (Postgres + PostGIS + Redis), migracije
- [x] `IStationDataSource` + `AvpSavaArcGisSource`
- [x] Ingest job na 15 min, sa circuit breakerom i validacijom
- [x] `/api/v1/geojson/reaches` kao statički fajl koji job prepisuje
- [x] MapLibre mapa sa zvaničnim bojama iz renderera
- [x] Disclaimer traka i atribucija
- [x] Fixture testovi adaptera + test vremenskih zona sa DST prelazom
- [x] Test da `Unknown` ne može postati `Normal`

**Riješeno usput:** zona `DATE_TIME`-a kod AVP Save (SOURCES.md §1.6). Odgovor je bio u
metadata sloja i jednom upitu, ne u 24-satnom mjerenju — **prvo pitaj izvor šta tvrdi o sebi.**

---

## Faza 2 — Stanice i historija ✅ **ZAVRŠENA 2026-08-05**

- [x] Registar stanica iz `ISV_BIH_2009_javnakarta/MapServer/1`
- [x] Tačke stanica kao **zaseban sloj** — veze sa dionicama nema, vidi SOURCES.md §1.7
- [x] Detalj panel po specifikaciji iz `UI.md` — strelica trenda i napomena o neuobičajenoj
      promjeni urađene. Protoka nema jer ga izvor ne objavljuje.
- [x] Graf 7/30 dana sa pragovima i imenom agencije koja ih definiše
- [x] Deep linkovi — `/dionica/{SEC_ID}` i `/stanica/{HID_ID}`, bez rutera kao zavisnosti
- [x] Pretraga po rijeci i po mjestu, neosjetljiva na dijakritiku i na `dj`/`đ`
- [x] Prikaz starosti podatka (opacity, ivica, šrafura) — urađeno u Fazi 1

**Riješeno:** Api servira sagrađeni SPA i ima fallback na `index.html`, pa `/dionica/*` i
`/stanica/*` rade i bez Vitea. Fallback namjerno **ne** hvata `/api/*` — API poziv koji dobije
`index.html` izgledao bi kao uspjeh sa čudnim tijelom.

**Nalaz iz podataka, 2026-08-05:** dionica `Fojnička rijeka` je u tri uzastopna sata dala
120 → 160 → −28.3 cm. Izvor je tako objavio i mi to vjerno čuvamo, ali skok od 188 cm za sat
gotovo sigurno nije voda nego senzor. `UI.md` §3 traži `Suspect` oznaku baš za ovo, a ona
još ne postoji.

**Riješeno:** `Suspect` kao sud **nije uveden**. Umjesto njega stoji činjenična napomena, a
mjera nije naša nego njihova — promjena se poredi sa **rasponom pragova koje je agencija
odredila za tu dionicu**. Promjena veća od cijelog tog raspona dobija napomenu; sve unutar
njega ne dobija ništa.

Ključno svojstvo: stvarni poplavni talas unutar operativnog raspona agencije **ne** dobija
napomenu. Provjereno: Fojnička (Δ −188 cm, raspon 120) se označava, a rast od +200 cm na
Zenici (raspon 270) se ne označava. Dionica bez pragova ne dobija napomenu jer nemamo skalu
s kojom bismo poredili.

**Riješeno:** web ima Vitest, 36 testova nad čistom logikom (pretraga, starost, trend,
napomena o promjeni, rute). Poznato ograničenje zapisano u testu: poklapanje je po podnizu,
pa drugi padež ne nalazi — "pjesacki" ne nalazi "pješačkog". Stemmer za bosanski je zaseban
posao; zapisano da se zna da je izbor, ne previd.

---

## Faza 3 — Jadranski sliv ✅ **ZAVRŠENA 2026-08-05**

Prvi put dva izvora u istoj mapi. **Ovdje se testira sav rad oko nejednake gustine i frekvencije** — ako je nešto pogrešno u modelu, ovdje puca.

- [x] `AvpjmSource` (AngleSharp) — 20 stanica iz jednog zahtjeva
- [x] Zaseban layer sa zasebnom legendom
- [x] `/api/v1/sources` sa statusom po izvoru, vidljiv u UI-u
- [ ] **Vizuelna provjera: da li jug izgleda "prazan" ili "bez podatka".** Traži ljudske oči.

**Bug koji je držao mapu praznom (2026-08-05):** MapLibre parsira GeoJSON u Web Workeru, a
ime tog fajla sklapa u vrijeme izvršavanja, pa ga nijedan bundler ne otkriva statički. Worker
je vraćao 404 → vektorski slojevi prazni, raster podloga se uredno crtala. Mapa je izgledala
kao mapa **bez ijedne opasnosti**, uz jedan jedini 404 u konzoli.

Riješeno eksplicitnim `?worker&url` uvozom i `setWorkerUrl` (`src/lib/maplibre-worker.ts`).
Worker nije samostalan — uvozi `maplibre-gl-shared.mjs` — pa mu treba pakovanje sa
zavisnostima, ne kopiranje fajla.

**Izgubljena historija (2026-08-05):** integracijski testovi su gađali istu bazu koju koristi
aplikacija i brišu tabele između slučajeva. Jedan `dotnet test` je obrisao svu prikupljenu
historiju. **Nepovratno** — AVP Sava ne objavljuje arhivu, pa se ta mjerenja ne mogu ponovo
povući; skupljanje počinje ispočetka.

Popravljeno: testovi rade nad `vodostaji_test`, koju sami prave, a brisanje je zaključano
provjerom da ime baze završava na `_test`. Ista greška se više ne može ponoviti.

**Pouka:** greška u infrastrukturi prikaza je u ovoj aplikaciji greška o sigurnosti. Prazna
mapa i mapa bez opasnosti izgledaju identično. Zato je uz popravku dodano i da se svaka
greška mape ispiše **na ekranu**, ne u konzoli.

**Model je pukao na dva mjesta, tačno kako je ova faza i predviđala:**

1. **`KnownCount` je značio dvije stvari.** Kod AVP Save "ima ocjenu" i "ima mjerenje" se
   poklapaju, pa se razlika nije vidjela. AVPJM daje 20 mjerenja i **nula ocjena**, i ista
   riječ je počela značiti suprotno u dva odgovora. Razdvojeno u `MeasuredCount` i `KnownCount`.
2. **Ruta je tretirala ključ kao globalan.** AVP Sava ima dionicu `1`, AVPJM ima stanicu `1`;
   `/dionica/1` je otvarao Sanu, a Mostar je bio nedostupan deep linkom. Domenski model je
   cijelo vrijeme govorio da ključ vrijedi samo unutar izvora — ruta ga nije slušala.
   Sada je `/dionica/{izvor}/{ključ}`.

**Ispravka iz Faze 0:** tvrdnja da lista AVPJM-a nema podatke bila je pogrešna, vidi
SOURCES.md §2.

---

## Faza 4 — FHMZBIH i RS ⬅️ **TRENUTNA**

Puna pokrivenost. Očekuj da će RS adapter biti najkrhkiji dio sistema.

- [x] `FhmzbihSource` — 12 stanica, AngleSharp, koordinate i kota nule sa podstranica
- [x] `RhmzRsSource` — **istraženo do kraja, izvora nema.** Vidi SOURCES.md §4.2–4.4:
      mapa automatskih stanica im je pokvarena, stranica biltena prazna na oba mirrora,
      a bilteni Voda Srpske su tromjesečni časopis (provjereno čitanjem broja 26).
      Jedini API im je `/api/flood-defense-points`, bez koordinata.
- [x] Brčko — istraženo, nijedan izvor nije nađen.

**Umjesto adaptera: objašnjena praznina.** Sjeveroistok zemlje nije miran nego neprikazan, i
UI to sada piše doslovno („Šta nije pokriveno"). Praznina bez objašnjenja čita se kao
„nema šta prijaviti" — zlatno pravilo 1 na nivou cijele karte.

RS se vraća u Fazu 6, gdje kontakt donosi najviše: njihova mapa stanica **postoji ali je
pokvarena**, pa se ne traži da naprave nešto novo nego da poprave nešto svoje.
- [x] Neuspjeh parsiranja tretiran kao normalno stanje, ne kao pad joba

**Treći izvor, treća konvencija.** AVP Sava objavljuje stupanj opasnosti i mjeri na sat;
AVPJM ne objavljuje stupanj i drži zimsko vrijeme cijele godine; FHMZBIH ne objavljuje
stupanj, **poštuje ljetno vrijeme**, i jedini **objavljuje trend** (`R`, `O`, `S`).

Zato je trend ušao u model kao `PublishedTrend`: kad ga izvor daje, ima prednost nad našim
izvodom iz dva očitanja. Naš račun je zamjena za tvrdnju agencije, ne obrnuto.

Oznaka `S2` se pojavljuje u njihovim podacima a značenje joj nije dokumentovano — ostaje
`Unknown` uz sačuvanu oznaku. Pogrešno pogođen smjer trenda je pogrešan podatak o rijeci.

---

## Faza 5 — Upozorenja

**Tek nakon što je pouzdanost podataka dokazana kroz stvarni sezonski ciklus.**

Lažno upozorenje je gore od nikakvog — ubija kredibilitet kod korisnika i kod agencija kojima ćeš pisati u Fazi 6.

- [ ] Push / email na korisnički prag po stanici
- [ ] Upozorenje se **ne šalje** ako je podatak stariji od 1× `ExpectedInterval`
- [ ] Rate limit po korisniku i po stanici (bez lavine kod oscilacija oko praga)

---

## Faza 6 — Kontakt sa agencijama

Sa gotovom aplikacijom kao argumentom. Vidi `LEGAL.md` za strategiju i redoslijed.

---

## Van dometa zasad

Prognoza (`Prognoza_hidrološkog_stanja_javno`), Sava HIS, crowdsource prijave, mobilne native aplikacije, bilo kakva monetizacija.
