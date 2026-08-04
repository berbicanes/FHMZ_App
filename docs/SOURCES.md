# SOURCES.md — specifikacije izvora

> ⚠️ **STATUS: NEVERIFIKOVANO.** Sve ispod je prikupljeno ručno i može biti netačno ili zastarjelo.
> Faza 0 zamjenjuje ovaj fajl generisanim sadržajem iz stvarnih odgovora servera.
> Do tada: **ne piši adaptere protiv ove dokumentacije.**

## FAZA 0 — obavezno prije bilo kakvog koda

1. Napravi `tools/Probe` — konzolna aplikacija
2. Pozovi `?f=json` na svakom navedenom ArcGIS servisu i sloju
3. Za HTML izvore: snimi po jednu reprezentativnu stranicu
4. Snimi pune odgovore u `tests/fixtures/<source>/<layer>-YYYY-MM-DD.json`
5. Regeneriši ovaj fajl sa **stvarnim** poljima, tipovima, domenima i renderer bojama
6. Ukloni upozorenje na vrhu i upiši datum verifikacije

Ako se stvarna shema razlikuje od ovoga — stvarnost pobjeđuje.

---

## 1. AVP Sava — `avp-sava` — PRIORITET 1

**Baza:** `https://isvportal.voda.ba/server/rest/services`
ArcGIS Server 11.5, javno, bez autentikacije.

| Servis | Sadržaj |
|---|---|
| `Hidrolosko_stane_u_realnom_vremenu/FeatureServer/0` | realno vrijeme — **poligoni dionica** |
| `Prognoza_hidrološkog_stanja_javno/FeatureServer` | prognoza |
| `ISV_BIH_2009_javnakarta/MapServer/1` | registar hidroloških stanica (tačke) |
| `ISV_BIH_2009_javnakarta/MapServer` tabele 50, 98 | `MON_HIDRO`, `MON_HID` — mjerenja |
| `Upravljanje_rizicima_od_poplave___javno` | poplavne zone |
| `Crowdsource_Flood_public` | prijave građana |

**Primjer upita:**

```
GET https://isvportal.voda.ba/server/rest/services/
    Hidrolosko_stane_u_realnom_vremenu/FeatureServer/0/query
    ?where=1%3D1&outFields=*&outSR=4326&f=geojson
```

**Očekivana polja sloja 0 (neverifikovano):**
`HYDRO_ID`, `H_CM`, `DATE_TIME`, `STANDBY_STAT`, `REGULAR_DEF_ST`, `OUTSTANDING_ST`, `EMERGENCY_ST`, `CURRENT_STATUS`

**Zvanične boje iz renderera** — koristi ove, ne izmišljaj svoje:

| `CURRENT_STATUS` | Label | Hex | `AlertLevel` |
|---|---|---|---|
| Standby | Normalno | `#38A800` | Normal |
| Regular defence | Izljevanje iz korita | `#FFFF00` | Elevated |
| Outstanding defence | Poplave | `#FFAA00` | Flood |
| Emergency | Značajne poplave | `#E60000` | Emergency |
| No Data | Nema podataka | `#CCCCCC` | Unknown |

**Tehničko:**
- Nativni CRS je EPSG:3857 → **uvijek** `outSR=4326`
- `MaxRecordCount` 2000 → paginacija preko `resultOffset` / `resultRecordCount`
- Podržava `orderByFields` i statističke upite — koristi za historiju umjesto punog povlačenja
- `f=geojson` preferirano nad `f=json`

---

## 2. AVP Jadranskog mora — `avpjm` — PRIORITET 2

- Lista: `https://avpjm.jadran.ba/vodomjerne_stanice`
- Detalj: `https://avpjm.jadran.ba/vodomjerne_stanice/{id}` (`/1` = Hidrološka postaja Mostar)
- Pokriva: Neretva, Trebišnjica, Cetina, Krka
- Kontakt: `jsliv@jadran.ba`

Server-rendered HTML → **AngleSharp**, nikad regex.

Njihov ISV (`isvportal.jadran.ba`) je isti Esri stack kao Sarajevo, ali sa pristupom reguliranim korisničkim pravima. **Ako dobijemo pristup, ovaj adapter se prepisuje na ArcGIS i scraper se briše** — piši ga tako da to bude trivijalno: interfejs isti, implementacija zamjenjiva.

---

## 3. FHMZBIH — `fhmzbih` — PRIORITET 3

- `https://www.fhmzbih.gov.ba/latinica/HIDRO/`
- `https://fop.fhmzbih.gov.ba` — pragovi obavještavanja

Stanice: Bihać, Martin Brod, Sanski Most, Vrhpolje, Sarajevo, Reljevo, Zenica, Kiseljak, Han Bila, Tuzla, Kašići, Konjic.

Uloga: cross-check za sliv Save i pokrivač rupa. Dnevna frekvencija.

---

## 4. RHMZ RS + Vode Srpske — `rhmz-rs` — PRIORITET 4, NAJTEŽE

RHMZ RS osmatra vodostaje na hidrološkim stanicama u RS i objavljuje redovne i vanredne hidrološke biltene. **Nema API-ja.** Realno: HTML ili PDF parsiranje.

Očekuj najviše lomljenja. Piši defanzivno, sa velikim brojem fixture testova, i tretiraj neuspjeh parsiranja kao **normalno stanje** koje se logira — ne kao izuzetak koji ruši job.

Ovo je izvor gdje direktan kontakt sa institucijom donosi najviše. Vidi `LEGAL.md`.

---

## 5. Sava FFWS / Sava HIS — istražiti, ne graditi

Sava FFWS integriše data hub za osmotrene podatke (Sava HIS) preko šest zemalja. Za BiH su operativno uključeni FHMZBIH, RHMZ RS i JU "Vode Srpske" — **jedino mjesto gdje su oba entiteta već spojena.**

Pokriva samo sliv Save (nema Neretve). Pristup institucionalni, preko ISRBC.

Postoji presedan: regionalni servis `vodostaj.rs` navodi FFWS kao svoj izvor za BiH.

Ne gradi na ovome dok pristup nije potvrđen, ali drži interfejs otvorenim.

---

## Kontrolna lista za novi adapter

- [ ] Fixture snimljen u `tests/fixtures/<source>/` sa datumom
- [ ] Implementira `IStationDataSource`, ne zna za druge adaptere
- [ ] Dokumentovano kako interpretira vremensku zonu + test sa DST prelazom
- [ ] Mapiranje statusa u `AlertLevel` eksplicitno, sa `StatusLabelOriginal` sačuvanim
- [ ] `ExpectedInterval` postavljen realno (nosi prikaz starosti u UI-u)
- [ ] Atribucija: `AgencyName` + `AgencyUrl` popunjeni
- [ ] Neuspjeh parsiranja logira i preskače stanicu, ne ruši cijeli run
- [ ] Nepoznat ili neparsiran status → `AlertLevel.Unknown`, nikad `Normal`
- [ ] Rate limit ≥ 10 min, `User-Agent` sa kontaktom
- [ ] Upisan u `/api/v1/sources`
