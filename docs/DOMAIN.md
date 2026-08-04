# DOMAIN.md — domenski kontekst

Pročitaj prije bilo kakvog rada s podacima.

## 1. Zašto projekat postoji

BiH nema jedno tijelo nadležno za vode. Ima četiri, sa različitim tehnologijama, kvalitetom podataka i stepenom otvorenosti. Građanin koji hoće znati da li mu Sana raste mora znati da Sana pripada slivu Save. Za Neretvu — drugi sajt. Za Doboj — treći.

**To je administrativna podjela nametnuta korisniku, i ona je jedini razlog postojanja ovog projekta.**

| Područje | Institucija | Tehnologija | Otvorenost |
|---|---|---|---|
| FBiH, sliv Save | AVP Sava, Sarajevo | ArcGIS Server 11.5 REST | otvoreno, bez auth |
| FBiH, jadranski sliv | AVP Jadranskog mora, Mostar | server-rendered HTML | scraping; ISV zaključan |
| Republika Srpska | JU "Vode Srpske" + RHMZ RS | HTML + PDF bilteni | bez API-ja |
| Brčko distrikt | vlastita nadležnost | — | najslabije pokriveno |
| Presjek FBiH | FHMZBIH | HTML tabele | scraping |
| Regionalno (sliv Save) | ISRBC / Sava FFWS, Sava HIS | institucionalni pristup | uz dogovor |

## 2. Tri nejednakosti koje moraš nositi kroz cijeli sistem

**Gustina.** Sliv Save ima telemetriju sa poligonima dionica i pragovima po dionici. Hercegovina i RS imaju rjeđe tačke. Na mapi to izgleda kao da je sjever ugrožen a jug prazan — **a to nije istina, samo je izvor slabiji.** Ovo je najveća zamka projekta.

**Frekvencija.** AVP Sava — minute. FHMZBIH i RHMZ RS — dnevni bilteni. Ne smiju se bojiti istim intenzitetom.

**Pragovi.** AVP Sava ima četiri numerička praga po dionici. Ostali imaju vlastite kategorije koje se ne poklapaju jedan-na-jedan. Mapiranje mora biti eksplicitno u kodu i vidljivo korisniku — čiji je prag u pitanju.

## 3. Terminologija

Kod: engleski identifikatori. UI: bosanski/hrvatski/srpski.

| BHS | EN | Jedinica |
|---|---|---|
| vodostaj | water level | cm |
| protok / proticaj | flow, discharge | m³/s |
| hidrološka stanica / vodomjerna postaja | station | — |
| sliv / vodno područje | river basin | — |
| dionica | river reach | poligon |
| prag | threshold | cm |

## 4. Kanonski model

Sve što uđe u sistem svodi se na ovo. Adapteri su **jedino** mjesto gdje postoji specifičnost izvora.

```csharp
public enum AlertLevel
{
    Unknown   = 0,  // nemamo podatak — NIJE isto što i Normal
    Normal    = 1,  // redovno stanje
    Elevated  = 2,  // izljevanje iz korita / pripravnost
    Flood     = 3,  // poplave / redovna odbrana
    Emergency = 4   // značajne poplave / vanredna odbrana
}

public record Station(
    string Key,                  // "avps:1042", "avpjm:1", "rhmzrs:banjaluka-vrbas"
    string Name,                 // "Sana — Sanski Most"
    string RiverName,
    string SourceId,             // "avp-sava"
    string AgencyName,
    string AgencyUrl,
    double Latitude,             // UVIJEK EPSG:4326
    double Longitude,
    Thresholds? Thresholds,
    TimeSpan ExpectedInterval);  // koliko često izvor osvježava — nosi prikaz starosti

public record Thresholds(
    decimal? ElevatedCm,
    decimal? FloodCm,
    decimal? EmergencyCm,
    string DefinedBy);           // koja agencija — prikazuje se korisniku

public record Measurement(
    string StationKey,
    decimal? LevelCm,
    decimal? FlowM3s,
    decimal? WaterTempC,
    AlertLevel Status,
    string StatusLabelOriginal,  // doslovan tekst izvora: "Izljevanje iz korita"
    DateTimeOffset MeasuredAt,
    DateTimeOffset FetchedAt,
    bool Suspect = false);
```

### MeasuredAt vs FetchedAt

Dvije različite stvari, **nikad ih ne miješaj**. Ako u 14:00 povučemo bilten izdat u 07:00, podatak je star 7 sati iako je fetch bio prije minut. UI računa starost iz `MeasuredAt`.

### Vremenske zone

Izvori vraćaju lokalno vrijeme (Europe/Sarajevo), ponekad bez oznake zone, ponekad epoch millis u UTC. Sve u bazi kao UTC `timestamptz`. Svaki adapter eksplicitno dokumentuje kako interpretira vrijeme, i to je pokriveno testom — uključujući DST prelaz.

## 5. Ingest pipeline

```
Scheduler → Source Adapter → Normalizer → Validator → Repository → Cache invalidation
```

- Svaki adapter implementira `IStationDataSource` i **ne zna ništa o drugim adapterima**
- `try/catch` po izvoru, nikad jedan oko svih
- Interval po izvoru: AVP Sava 15 min, ostali 30–60 min. **Nikad ispod 10 minuta.**
- `User-Agent`: `VodostajiBiH/1.0 (+https://<domena>; kontakt@<domena>)`
- Retry sa eksponencijalnim backoffom, max 3 pokušaja, pa čekaj sljedeći ciklus
- Circuit breaker: 5 padova zaredom → pauza 1h + log
- Neuspio fetch **nikad ne briše** postojeći podatak

### Validacija — odbaci i logiraj, ne upisuj

- vodostaj izvan `[-500, 3000]` cm
- `MeasuredAt` u budućnosti (tolerancija 15 min)
- `MeasuredAt` stariji od 30 dana kod izvora koji tvrdi da je real-time
- skok > 200 cm između dva uzastopna očitanja iste stanice → **upiši, ali `Suspect = true`**

## 6. API

Javni, read-only, bez autentikacije, verzioniran.

```
GET /api/v1/stations                        registar
GET /api/v1/stations/{key}                  detalj
GET /api/v1/stations/{key}/history?days=7   vremenska serija
GET /api/v1/current                         zadnje mjerenje za sve
GET /api/v1/geojson/reaches                 poligoni dionica sa statusom
GET /api/v1/geojson/stations                tačke stanica sa statusom
GET /api/v1/sources                         status svakog izvora + zadnji uspješan fetch
```

`/api/v1/sources` **nije debug endpoint — dio je proizvoda.** Korisnik ima pravo znati koji izvor je pao.

Svaki odgovor nosi `attribution` po stanici. Cache headeri odražavaju stvarnu frekvenciju izvora, ne fiksnu vrijednost.

### Spike arhitektura — bitno

Upotreba je ekstremno šiljasta: 350 dana tišine, pa 10 dana kad svi gledaju. Spike dolazi tačno kad je najveća šansa da nešto pukne.

Zato `/api/v1/geojson/*` **mora biti statički fajl na CDN-u** koji ingest job prepisuje svakih 15 min — ne live upit na Postgres. Pri 50.000 istovremenih korisnika backend tada ne biva ni dodirnut.
