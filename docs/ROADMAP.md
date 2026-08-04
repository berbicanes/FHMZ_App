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

## Faza 1 — Vertikalni presjek ⬅️ **TRENUTNA**

Samo AVP Sava. Cilj je funkcionalna mapa sjeverne BiH od kraja do kraja.

- [ ] Solution, docker compose (Postgres + PostGIS + Redis), migracije
- [ ] `IStationDataSource` + `AvpSavaArcGisSource`
- [ ] Ingest job na 15 min, sa circuit breakerom i validacijom
- [ ] `/api/v1/geojson/reaches` kao statički fajl koji job prepisuje
- [ ] MapLibre mapa sa zvaničnim bojama iz renderera
- [ ] Disclaimer traka i atribucija
- [ ] Fixture testovi adaptera + test vremenskih zona sa DST prelazom
- [ ] Test da `Unknown` ne može postati `Normal`

---

## Faza 2 — Stanice i historija

- [ ] Registar stanica iz `ISV_BIH_2009_javnakarta/MapServer/1`
- [ ] Tačke stanica kao zaseban sloj
- [ ] Detalj panel po specifikaciji iz `UI.md`
- [ ] Graf 7/30 dana sa pragovima i imenom agencije koja ih definiše
- [ ] Deep linkovi `/stanica/{key}`
- [ ] Pretraga po rijeci i po mjestu
- [ ] Prikaz starosti podatka (opacity, ivica, šrafura)

---

## Faza 3 — Jadranski sliv

Prvi put dva izvora u istoj mapi. **Ovdje se testira sav rad oko nejednake gustine i frekvencije** — ako je nešto pogrešno u modelu, ovdje puca.

- [ ] `AvpjmScrapeSource` (AngleSharp)
- [ ] Zaseban layer sa zasebnom legendom
- [ ] `/api/v1/sources` sa statusom po izvoru, vidljiv u UI-u
- [ ] Vizuelna provjera: da li jug izgleda "prazan" ili "bez podatka"

---

## Faza 4 — FHMZBIH i RS

Puna pokrivenost. Očekuj da će RS adapter biti najkrhkiji dio sistema.

- [ ] `FhmzbihScrapeSource`
- [ ] `RhmzRsSource` (HTML + PDF bilteni)
- [ ] Brčko — istražiti šta uopšte postoji
- [ ] Neuspjeh parsiranja tretiran kao normalno stanje, ne kao pad joba

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
