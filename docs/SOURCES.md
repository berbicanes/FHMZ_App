# SOURCES.md — specifikacije izvora

> ✅ **STATUS: VERIFIKOVANO 2026-08-04** (UTC) — `tools/Probe`, svi izvori uključujući RHMZ RS.
> Sve ispod je izvedeno iz stvarnih odgovora servera, snimljenih u `tests/fixtures/`.
> Izvještaj sonde: `tests/fixtures/_report/schema-2026-08-04.md`.

Ponovna verifikacija: `dotnet run --project tools/Probe`. Fixtures nose datum u imenu, pa
stari ostaju kao dokaz kako je shema izgledala ranije. Ako se stvarna shema razlikuje od
ovoga — stvarnost pobjeđuje, popravi ovaj fajl u istom commitu.

---

## Šta je verifikacija promijenila

| Pretpostavka prije Faze 0 | Stvarnost |
|---|---|
| AVPJM je server-rendered HTML za AngleSharp | **Laravel + Vue SPA.** Podaci su JSON u Vue propu, lista stanica je prazna ljuštura |
| AVP Sava `DATE_TIME` je nedvosmislen | Zona **riješena**: servis vraća pravi UTC (§1.6) |
| Registar stanica ima upotrebljiv `OBJECTID` | `objectIdField` je `null`; `outFields=*` ruši upit |
| `Crowdsource_Flood_public` je izvor | Servis postoji u katalogu ali **nije pokrenut** |
| RHMZ RS nema API | **Ima dva** — ali nijedan ne servira vodostaje; mapa koja bi ih dala je pokvarena |
| Vode Srpske objavljuju biltene sa vodostajima | 34 PDF-a, numerisana rednim brojem — časopis, ne operativni bilten |
| Polja i boje sloja realnog vremena | **Potvrđeno tačno**, 1:1 sa dokumentacijom |

---

## 1. AVP Sava — `avp-sava` — PRIORITET 1

**Baza:** `https://isvportal.voda.ba/server/rest/services`
ArcGIS Server **11.5** (potvrđeno), javno, bez autentikacije.

Katalog vraća **26 servisa**. Šest foldera traži token i nedostupno je:
`GP`, `Hosted`, `OGC_WEB_services`, `ProMigracija`, `SavskaKomisija`, `UNDP`.

> `SavskaKomisija` je zaključan folder na javnom portalu — vidi §5 i `LEGAL.md`.

### 1.1 Realno vrijeme — `Hidrolosko_stane_u_realnom_vremenu/FeatureServer/0`

Poligoni **dionica**, ne tačke stanica. **45 dionica ukupno**, jedan sloj.
`maxRecordCount` 2000 — paginacija u praksi nije potrebna, ali kod je svejedno mora podržati.

| Polje | Tip | Napomena |
|---|---|---|
| `OBJECTID` | OID | |
| `SEC_ID` | Double | id dionice |
| `description` | String | npr. `Bosna-Zenica`, `Fojnička rijeka` |
| `HYDRO_ID` | Double | veza na registar stanica (`HID_ID`) |
| `H_CM` | **Single** | vodostaj u cm; **može biti negativan** (viđeno `-28.2`) |
| `DATE_TIME` | Date | epoch ms, **pravi UTC** — vidi §1.6 |
| `STANDBY_STAT` | Integer | prag, cm |
| `REGULAR_DEF_ST` | Integer | prag, cm |
| `OUTSTANDING_ST` | Integer | prag, cm |
| `EMERGENCY_ST` | Integer | prag, cm |
| `Shape__Area`, `Shape__Length` | Double | |

`H_CM` je `esriFieldTypeSingle` — stiže sa artefaktima jednostruke preciznosti (`17.6000004`).
Parsiraj iz sirovog JSON teksta u `decimal`, nikad preko `double`.

**Zvanične boje iz renderera** (`uniqueValue` nad `CURRENT_STATUS`) — potvrđene, koristi ove:

| `CURRENT_STATUS` | Label | Hex | `AlertLevel` |
|---|---|---|---|
| Standby | Normalno | `#38A800` | Normal |
| Regular defence | Izljevanje iz korita | `#FFFF00` | Elevated |
| Outstanding defence | Poplave | `#FFAA00` | Flood |
| Emergency | Značajne poplave | `#E60000` | Emergency |
| No Data | Nema podataka | `#CCCCCC` | Unknown |

**Stanje 2026-08-04 22:25Z:** 33 dionice `Standby`, **12 `No Data`**, 11 sa `DATE_TIME = null`.
Četvrtina mreže bez podatka nije rub slučaj nego normalno stanje — UI mora izgledati dobro sa
12 sivih dionica, ne tretirati to kao grešku.

Pragovi su rastući i **dati po dionici** (npr. Bosna-Zenica `124/154/344/394`). Status dolazi
gotov u `CURRENT_STATUS`. Zlatno pravilo 3 stoji: ne izvodi status iz `H_CM` i pragova sam.

**Primjer upita:**

```
GET https://isvportal.voda.ba/server/rest/services/
    Hidrolosko_stane_u_realnom_vremenu/FeatureServer/0/query
    ?where=1%3D1&outFields=*&outSR=4326&f=geojson
```

### 1.2 Registar stanica — `ISV_BIH_2009_javnakarta/MapServer/1`

Naziv sloja: *Hidrološke stanice*. Tačke. **102 stanice.**
Servis ima ukupno 109 slojeva; nas zanima sloj 1 i tabele 50 i 98.

Polja: `HID_ID`, `shape`, `OBJECTID`, `NAZIV`, `LOKACIJA`, `x`, `y`,
`TIP_HIDROLOŠKE_STANICE`, `KOTA_0`, `BR_V_LETVI`.

**Tri zamke, sve potvrđene:**

1. **`outFields=*` ruši upit** (`ArcGIS 400: Failed to execute query`). Uzrok je `OBJECTID`:
   sloj ga prijavljuje u shemi, ali `objectIdField` je `null` i upit nad njim puca. Traži polja
   poimence, bez `OBJECTID`. `tools/Probe` to radi automatski kad `*` bude odbijen.
2. **`x` i `y` atributi nisu upotrebljivi.** To je Gauss-Krüger (MGI/Balkans), ali u
   **miješanim zonama** — 89 stanica u zoni 6, 8 u zoni 5 — a kod **3 stanice su ose zamijenjene**
   (`Krušnica ušće`, `HS Orašje`, `HS Kreševka - Kiseljak`). Koristi geometriju koju server vrati
   uz `outSR=4326`; ona je ispravna za svih 101 stanicu koja geometriju ima.
3. **Nepotpunost:** 1 stanica bez geometrije i bez `HID_ID`, **13 stanica bez `KOTA_0`**.

`HID_ID` je unikatan (102/102) i `NAZIV` je unikatan — `HID_ID` je ključ, `NAZIV` rezerva.
Tipovi: 99 `Automatska stanica`, 3 `Vodomjerna letva`.
Geografski opseg: lon 15.783–18.974, lat 43.582–45.180.

### 1.3 Mjerenja — `ISV_BIH_2009_javnakarta/MapServer` tabele 50 i 98

- **50 (`MON_HIDRO`)** — registarski zapis stanice: `HIDRO_ID`, `EU_CD` (npr. `BA5010`),
  `NAZIV` (`HS Goražde`), `LOKACIJA`, `X`, `Y`, `Z`, `POV_SLIVA`, `DAT_AZUR`, `NADLEZNOST`.
- **98 (`MON_HID`)** — `HID_ID`, `HIDRO_ID`, `ORG_OZN`, `ORIG_BR`, `TIP_HID_PO`,
  **`KOTA_0`** (kota nule letve, npr. `150.22`), `BR_V_LETVI`, `STAC_KM`.

`KOTA_0` je nula vodomjerne letve — bez nje se `H_CM` ne može prevesti u apsolutnu kotu.
Za Fazu 2 to znači: apsolutne kote nisu moguće za 13 stanica.

### 1.4 Ostali servisi u katalogu

| Servis | Tipovi | Stanje |
|---|---|---|
| `Prognoza_hidrološkog_stanja_javno` | Feature + Map | radi, po 2 sloja, `RBM_FRM.DBO.PrognozaPoplave`, poligoni — **van dometa** |
| `Upravljanje_rizicima_od_poplave___javno` | Feature + Map | radi, po 32 sloja (poplavne zone) |
| `Crowdsource_Flood_public` | Feature + Map | **`ArcGIS 500: Service not started`** — ne postoji kao izvor |
| `ServisneInformacije_javno`, `Vodna_knjiga_SA`, `RBM_Nitrati`, `VodniKatastri_Nitrati`, `RBM_EKO_Nitrati`, `MAPE_HAZARD_RISK/*`, `FGU_INSPIRE_Geoportal/*`, `PUBLIC/BIH_ISV_PubWebApp_VD_PP_PA` | | postoje, nisu relevantni |

### 1.7 `HYDRO_ID` ne povezuje dionice sa stanicama — provjereno 2026-08-05

**Dionice i registar stanica se ne mogu spojiti javnim podacima.** Ne pokušavaj.

`HYDRO_ID` na sloju dionica ima 39 različitih vrijednosti u rasponu 1–39. Numerički se
poklapa savršeno:

| Ključ | Poklapanje |
|---|---|
| `HYDRO_ID` ∩ `sloj1.HID_ID` | 35/39 |
| `HYDRO_ID` ∩ `t98.HIDRO_ID` | **39/39** |
| `HYDRO_ID` ∩ `t50.HIDRO_ID` | **39/39** |

**I to je zamka.** Spoj uspijeva numerički a semantički je besmislen:

| Dionica | `HYDRO_ID` | Spojena stanica |
|---|---|---|
| Sana-Sanski Most | 1 | HS Goražde *(druga rijeka, drugi kraj zemlje)* |
| Una-Bihać | 5 | HS Rmanj Manastir |
| Bosna-Zenica | 22 | HS Raspotočje |

Provjereno i obrnuto: za 19 dionica kod kojih se stanica može nedvosmisleno naći **po imenu**,
`HYDRO_ID` se ne poklapa ni sa `HID_ID` ni sa `HIDRO_ID` te stanice — **nijednom od 19**.
Razlike nemaju obrazac, pa nije ni pomak ni preslikavanje.

Pretraženo je i svih 109 slojeva servisa: `SEC_ID` se ne pojavljuje nigdje, a `HID_ID` samo u
slojevima 1, 50 i 98. **`HYDRO_ID` dakle pokazuje na registar koji nije javno objavljen.**

**Šta to znači za Fazu 2:** ništa se ne blokira. Dionice nose vrijednost, vrijeme, pragove i
historiju — sve što detalj i graf trebaju. Registar stanica ide kao **zaseban sloj tačaka**,
što UI.md §1 ionako traži. Spajanje po imenu bi radilo za 19 od 45, a ostatak bi bio nagađanje;
izmišljena veza između dionice i stanice je izmišljen podatak.

Ako veza zatreba (npr. `KOTA_0` za apsolutnu kotu), jedini pošten put je **ručno provjerena
tabela u repozitoriju**, sa eksplicitno navedenim nespojenim slučajevima — nikad zaključivanje
u vrijeme izvršavanja. Ili pitanje za `info@voda.ba` u Fazi 6.

---

### 1.6 Vremenska zona — riješeno 2026-08-04

**`DATE_TIME` je pravi UTC epoch. Ne pomjera se ni za sekundu.**

Sloj to i deklariše u svojoj metadata:

```json
"dateFieldsTimeReference": {
  "timeZone": "Central European Standard Time",
  "timeZoneIANA": "Europe/Warsaw",
  "respectsDaylightSaving": true
},
"datesInUnknownTimezone": false
```

Ali deklaracija govori u čemu je vrijeme **pohranjeno**, ne šta servis **vraća**. Razlika je
bitna, jer mnogi ArcGIS servisi vraćaju lokalno zidno vrijeme zapakovano kao da je UTC — što
je tačno ono što AVPJM radi (§2). Provjereno upitom:

```
DATE_TIME = '2026-08-04 22:00:00'   →  count 0
DATE_TIME = '2026-08-05 00:00:00'   →  count 28
```

Epoch koji je servis vratio je `1785880800000`, što se naivno čita kao `2026-08-04 22:00Z`.
Baza ga matchuje na `2026-08-05 00:00`, dakle drži **lokalno zidno vrijeme**. A `00:00 CEST`
**jeste** `22:00Z`. Konverziju dakle radi njihov servis, ispravno, i epoch stiže već u UTC-u.

**Ispravka ranije pretpostavke.** Do ovog dokaza se čitalo pesimistično kao CEST, što je
podatke prikazivalo **dva sata starijim nego što jesu**. Pesimizam je bio ispravan izbor dok
se nije znalo, ali nije zamjena za provjeru.

**Kašnjenje objave je 85–115 minuta**, mjereno kroz `--watch`: mjerenje sa oznakom punog sata
pojavi se oko sat i po kasnije. Kadenca je satna, pa `ExpectedInterval = 1h` stoji.

To dvoje se **ne smije spojiti u jedan broj.** Kadenca je koliko često izvor mjeri; kašnjenje
je koliko treba da mjerenje stigne do nas. Pošto je zdravo očitanje uvijek staro oko dva sata,
starost se mjeri **od trenutka kad je podatak realno mogao stići** (`Station.MissedCycles`).
Bez toga svaka dionica trajno stoji kao "kasni", korisnik se navikne da je signal uvijek
upaljen, i prestane ga gledati — što je gore nego da ga nema.

Izmjereno na svih 45 dionica nakon ispravke: 27 svježih, 3 sa propuštenim ciklusima,
3 zastarjele, 12 bez podatka. Prije ispravke je **sve** stajalo kao zastarjelo.

`respectsDaylightSaving: true` znači da baza zimi drži CET a ljeti CEST — ali pošto servis
konvertuje, adapter to ne mora znati. Zato je konvencija `Utc`, a ne `LocalWithDst`.

---

### 1.5 Tehničko

- Nativni CRS je EPSG:3857 → **uvijek** `outSR=4326`
- `f=geojson` radi na FeatureServer sloju; **MapServer slojevi prijavljuju `geoJSON` u
  `supportedQueryFormats` ali ga ne serviraju uvijek** — čitaj `supportedQueryFormats` i budi
  spreman pasti na `f=json`
- Podržava `orderByFields` i statističke upite — koristi za historiju umjesto punog povlačenja
- **ArcGIS greške stižu sa HTTP 200** i omotačem `{"error":{...}}`. Adapter koji gleda samo
  statusni kod vidi uspjeh gdje ga nema. Uvijek pregledaj tijelo.

---

## 2. AVP Jadranskog mora — `avpjm` — PRIORITET 2

- Lista: `https://avpjm.jadran.ba/vodomjerne_stanice`
- Detalj: `https://avpjm.jadran.ba/vodomjerne_stanice/{id}` (`/1` = Mostar, Neretva)
- Kontakt: `jsliv@jadran.ba`

**Ispravka ranije tvrdnje (2026-08-05).** U Fazi 0 je ovdje pisalo da stranica liste ne sadrži
podatke i da "scraper nad listom ne može raditi". **To je bilo pogrešno.** Tražio sam oblik koji
sam očekivao — tabele, linkove, riječ "stanic" — a podaci su HTML-escapovan JSON unutar Vue
atributa, gdje se nijedno od toga ne pojavljuje. Pouka: traži podatak, ne njegov očekivani oblik.

**Lista nosi cijeli registar u jednom zahtjevu.** Stack je Laravel + Vue (Vuetify), stranice su
server-rendered, a podaci putuju kroz Vue propove:

- `<stations-grid :items="…">` i `<stations-map :stations="…">` na listi — **20 stanica**
  sa trenutnim vrijednostima, pragovima, lokacijama i vodotocima
- Detalj nosi istu strukturu plus punu seriju

Jedan zahtjev na `/vodomjerne_stanice` dakle daje cijeli sliv. Detalj se poziva samo kad
treba historija.

Stranica detalja **nosi cijeli podatak u Vue propovima**:

- `<station-map :station="…">` i `<station-data-table :data="…">` — JSON objekat stanice
- `<station-chart :readings="…">` — puna serija kao `epoch<TAB>vrijednost` po liniji

Za `/1` (Mostar): **2976 očitanja, korak 15 minuta, 31 dan historije.** To je znatno bogatije
od AVP Save i dolazi u jednom zahtjevu.

**Polja objekta stanice** (potvrđeno na `/1`):

| Polje | Primjer | Značenje |
|---|---|---|
| `id`, `title`, `filename` | `1`, `Mostar`, `mostar` | ključ i naziv |
| `vodotok`, `dionica`, `poplavno_podrucje` | `Neretva`, `Od HE Mostar do željezničkog mosta u Čapljini` | |
| `location` | `43.34835,17.8105` | **`lat,lon` WGS84 kao string** |
| `unit`, `kota` | `cm`, `40.29` | jedinica i kota nule |
| `val`, `valtime` | `244`, `1785885300` | zadnje očitanje (epoch **sekunde**) |
| `prevval`, `prevvaltime` | `251`, `1785884400` | prethodno |
| `redovna_obrana`, `vanredna_obrana`, `kontinuirana_obrana` | `null`, `null`, `850` | **pragovi agencije** |
| `max1`…`max4`, `max4date` | `850`, `1525`, `1999-12-16` | historijski maksimumi |
| `status`, `fop`, `color`, `pos` | `1`, `0`, `#93aae0`, `40` | |
| `owner` | `AVP Jadransko more Mostar (zimsko računanje vremena)` | **vidi ispod** |
| `start_date` | `1923` | početak osmatranja |

**Vremenska zona — riješeno i dokazano.** Timestampovi su epoch sekunde, ali predstavljaju
**lokalno zimsko vrijeme (CET, UTC+1), zapisano kao da je UTC**. Dokaz iz snimka:

- zadnje očitanje nosi `1785885300` = `2026-08-04 23:15Z` ako se čita kao UTC
- snimak je uzet u `22:33Z` — čitano kao UTC, podatak je **42 minute u budućnosti**
- kao CET ispada `22:15Z`, tj. 18 minuta prije dohvata, uz korak od 15 minuta ✓
- polje `owner` to i piše: *"zimsko računanje vremena"*

Dakle: **oduzmi tačno 1 sat, cijele godine, bez obzira na ljetno vrijeme.** Naivno čitanje kao
UTC ljeti prikazuje podatak sat vremena svježijim nego što jeste i povremeno u budućnosti — što
direktno krši zlatno pravilo 2. Test DST prelaza za ovaj adapter mora dokazati da offset
**ostaje** +1 i u martu i u oktobru.

Pragovi mogu biti `null` (za Mostar su `redovna_obrana` i `vanredna_obrana` prazni). Prazan prag
je `Unknown`, ne `Normal`.

**Implementacija:** izvlačenje JSON-a iz Vue propa, uz `AngleSharp` za dohvat atributa i
`System.Text.Json` za parsiranje. Vrijednost propa je HTML-escapovana i sama je JSON string —
mora se dvostruko odmotati.

Njihov ISV (`isvportal.jadran.ba`) je isti Esri stack kao Sarajevo, sa pristupom reguliranim
korisničkim pravima. **Ako dobijemo pristup, ovaj adapter se prepisuje na ArcGIS i scraper se
briše** — piši ga tako da to bude trivijalno: interfejs isti, implementacija zamjenjiva.

---

## 3. FHMZBIH — `fhmzbih` — PRIORITET 3

- `https://www.fhmzbih.gov.ba/latinica/HIDRO/` — UTF-8, klasičan server-rendered HTML
- `https://fop.fhmzbih.gov.ba` — pragovi obavještavanja

Stranica nosi **jednu tabelu sa 12 stanica** i legendu sa tri kategorije. Kolone:

`Vodotok · Vodomjerna stanica · Datum · Vrijeme · Aktuelni vodostaj (cm) · Trend · Kontinuirano obavještavanje stanovništva i CZ`

Primjer reda: `Una · Bihać · 4.8.2026 · 08:00 · 22 · · 100`

**Statusni rječnik agencije** (iz legende — sačuvaj doslovno u `StatusLabelOriginal`):

| Tekst | `AlertLevel` |
|---|---|
| Nema potrebe za obavještavanjem. | Normal |
| Vodostaj dosegao nivo kontinuiranog obavještavanja stanovništva i CZ | Elevated |
| Nema podataka o vodostaju | **Unknown** |

Stanice, sa vlastitim podstranicama (`hvsBihac.php` … 12 komada): Bihać, Martin Brod,
Sanski Most (`hvsSMost`), Vrhpolje, Sarajevo (`hvsCumurija`), Reljevo, Zenica, Kiseljak,
Han Bila (`hvsHBila`), Tuzla, Kašići, Konjic.

**Vrijeme je eksplicitno lokalno** (`4.8.2026 08:00`), u formatu `d.M.yyyy` + `HH:mm` — nema
epocha, nema zone. Parsiraj kao `Europe/Sarajevo` i pretvori u UTC.

**FHMZBIH poštuje ljetno vrijeme, za razliku od AVPJM-a.** Dokaz iz watch ciklusa
`2026-08-04 22:43Z`: Martin Brod nosi `5.8.2026 00:00`. Kao CEST (UTC+2) to je `22:00Z`, tj.
43 minute prije očitavanja ✓. Kao CET (UTC+1) bilo bi `23:00Z` — 17 minuta u budućnosti, što je
nemoguće. Dakle `Europe/Sarajevo` sa punim DST pravilima.

**Dvije agencije, dvije konvencije.** AVPJM je fiksno CET cijele godine, FHMZBIH je lokalno sa
DST-om. Test DST prelaza mora pokriti oba slučaja odvojeno — ista funkcija za oba izvora je greška.

Pojedinačne stranice stanica (`hvsZenica.php` i sl.) nose i koordinate stanice
(`44.20795 17.90702`), vlasnika, rijeku, sliv, trend i vrijeme posljednjeg mjerenja.
Zenica se ažurira satno, Bihać znatno rjeđe — potvrda da `ExpectedInterval` ide po stanici.

**Frekvencija nije dnevna kako se pretpostavljalo.** U snimku Bihać nosi `4.8. 08:00`, a
Martin Brod `5.8. 00:00` — razlika od 16 sati unutar iste tabele. `ExpectedInterval` mora biti
po stanici, ne po izvoru.

Uloga: cross-check za sliv Save i pokrivač rupa. Zato što objavljuje **lokalno vrijeme
eksplicitno**, ovo je i alat za rješavanje otvorenog pitanja oko `DATE_TIME` u §1.

---

## 4. RHMZ RS + Vode Srpske — `rhmz-rs`, `vode-srpske` — PRIORITET 4, NAJTEŽE

**Verifikovano 2026-08-04.** Adrese nisu pogođene naslijepo — svaka je pročitana sa njihove
stranice ili iz skripte koju ta stranica učitava.

- `https://rhmzrs.com` — RHMZ RS, živ
- `https://novi.rhmzrs.com` — isti sadržaj i isti set skripti; ogledalo, ne zaseban izvor
- `http://www.voders.org` — JU "Vode Srpske", **samo HTTP, bez TLS-a**

### 4.1 Pretpostavka "nema API-ja" je bila pola tačna

RHMZ RS **ima JSON API.** Dva endpointa rade:

| Endpoint | Sadržaj |
|---|---|
| `/api/flood-defense-points` | **11 tačaka odbrane od poplava, sa pragovima** |
| `/api/meteo-stations` | 76 meteoroloških stanica — **nijedna hidrološka** |

**Ali nijedan ne servira vodostaje.** Ono što bi ih serviralo je pokvareno, vidi §4.2.

**`/api/flood-defense-points`** je najvrjedniji nalaz kod ovog izvora:

| Polje | Primjer | Značenje |
|---|---|---|
| `place` | `Делибашино Село` | mjesto |
| `ordinary_value` | `300` | **redovna odbrana, cm** |
| `extraordinary_value` | `370` | **vanredna odbrana, cm** |
| `kote0` | `141.38` | kota nule letve |
| `nnv` | `<b>22</b><br><span class="date">09.06.1977.</span>` | najniži zabilježeni |
| `vvv` | `<b>816</b><br><span class="date">16.05.2014.</span>` | najviši zabilježeni |
| `river_basin` | `{'id': 3, 'title': 'Врбас', …}` | ugniježđen objekat sliva |

Pragove definiše hidrolog i ovdje dolaze gotovi — zlatno pravilo 3 je zadovoljeno.
`nnv` i `vvv` nose **HTML unutar JSON polja**; vrijednost i datum se moraju izvući, ne prikazati
sirovo. Ćirilica je svuda, `place` se ne poklapa nužno sa nazivom stanice kod drugih izvora.

### 4.2 Mapa automatskih hidroloških stanica je pokvarena

`https://rhmzrs.com/page/hidrologija-mapa-stanica` obećava tačno ono što nam treba — tabela ima
zaglavlje **Станица · Вријеме · Водостај · Температура воде** i Leaflet mapu.

Tabela je prazna i ostaje prazna. Skripta `js/hydro-stations-leaflet.js` zove:

```js
axios.get(`${config.API.meteoStations}`).then(rs => { data = rs.data …
```

a `config` **nije definisan ni u jednoj od sedam skripti koje ta stranica učitava**
(provjereno u `plugins.js`, `theme.js`, `latinization.js`, `framer.js`, datatables, leaflet,
jQuery, i u samom HTML-u). Poziv baca `ReferenceError` prije nego išta krene.

Fixture `hydro-stations-leaflet-js` je snimljen kao dokaz.

**Praktično:** ako se to ikad popravi, iza njega vjerovatno stoji endpoint sa vodostajima i
temperaturom vode, po stanici, sa vremenom — što bi RS pretvorilo iz najtežeg u lakši izvor.
Vrijedi periodično provjeriti. **Ne graditi na tome dok ne proradi.**

Stranica biltena (`/page/bilten-izvjestaj-o-vodostanju`) je takođe prazna u HTML-u i nema
otkriven izvor podataka.

### 4.3 Vode Srpske

`voders.org` nosi **34 biltena u PDF-u**, imenovanih `ЈУ-Воде-Српске-БИЛТЕН-бројN.pdf`.
Numerisani su rednim brojem, ne datumom — to je institucionalni časopis, **ne operativni
dnevni bilten sa vodostajima**. Vijesti o vodostajima postoje kao tekst
(`/opadanje-vodostaja-u-republici-srpskoj/`), ali kao proza, ne kao podatak.

### 4.4 Zaključak za Fazu 4

Za RS **ne postoji javno dostupan feed vodostaja u realnom vremenu.** Postoje pragovi
(`/api/flood-defense-points`) i postoji mapa koja bi to riješila da radi.

Piši defanzivno, sa velikim brojem fixture testova, i tretiraj neuspjeh parsiranja kao
**normalno stanje** koje se logira — ne kao izuzetak koji ruši job. Do tada je RS realno
`Unknown` na mapi, i to je pošten prikaz, ne rupa.

**Kontakt** (iz podnožja `rhmzrs.com`): Републички хидрометеоролошки завод, Пут бањалучког
одреда бб, 78000 Бања Лука. Централа `+387 51 433-522`, **хидрологија `051 315-538`**.
Sajt ima i stavku **„Захтјев за подацима"** — formalni put koji se veže na argument o Zakonu o
slobodi pristupa informacijama iz `LEGAL.md` §1. Ovo je izvor gdje direktan kontakt donosi
najviše.

---

## 5. Sava FFWS / Sava HIS — istražiti, ne graditi

Sava FFWS integriše data hub za osmotrene podatke (Sava HIS) preko šest zemalja. Za BiH su
operativno uključeni FHMZBIH, RHMZ RS i JU "Vode Srpske" — **jedino mjesto gdje su oba entiteta
već spojena.** Pokriva samo sliv Save (nema Neretve). Pristup institucionalni, preko ISRBC.

**Novo iz Faze 0:** na `isvportal.voda.ba` postoji folder **`SavskaKomisija`** koji vraća
`ArcGIS 499: Token Required`. Servis dakle postoji na infrastrukturi AVP Save i zaključan je
pravima, a ne odsustvom. To je konkretan argument za Fazu 6 — ne tražiš da nešto naprave, nego
pristup nečemu što već stoji.

Ne gradi na ovome dok pristup nije potvrđen, ali drži interfejs otvorenim.

---

## Otvorena pitanja

1. ~~**`DATE_TIME` u AVP Sava — koja zona?**~~ **Riješeno 2026-08-04, vidi §1.6.**
   Ni sonda ni cross-check nisu bili potrebni — odgovor je bio u metadata sloja i u jednom
   upitu sa SQL literalom. Vrijedi zapamtiti redoslijed: **prvo pitaj izvor šta tvrdi o sebi,
   pa tek onda mjeri.** Sonda je trošila 24 sata na pitanje koje je servis odgovarao odmah.

2. ~~**Veza `HYDRO_ID` ↔ `HID_ID`**~~ **Provjereno 2026-08-05: veze nema.** Vidi §1.7.

3. **Brčko** — nije istraženo, nema poznat izvor.

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
- [ ] **Tijelo odgovora pregledano na grešku, ne samo HTTP status**
- [ ] Rate limit ≥ 10 min, `User-Agent` sa kontaktom (`tools/Probe/Contact.cs`)
- [ ] Upisan u `/api/v1/sources`
