# Vodostaji BiH

Jedna mapa sa stanjem svih rijeka u BiH, u realnom vremenu, besplatno. Danas su podaci razbijeni na četiri agencije i tri sajta; korisnik ne treba znati ništa o slivovima ni entitetima.

**Trenutno stanje: `FAZA 3 — dva izvora u mapi; ostaje vizuelna provjera juga.`**
71 test u .NET-u, 38 u web-u. AVP Sava (45 dionica, sa ocjenom) i AVPJM (20 stanica, **bez
ocjene** — agencija je ne objavljuje javnosti) rade kao zasebni pipelinei sa zasebnim
legendama. Rute su `/dionica/{izvor}/{ključ}` jer ključ nije globalan.
Ažuriraj ovu liniju pri svakom prelasku faze. Ako opis ispod ne odgovara stvarnom kodu — stvarnost pobjeđuje, popravi fajl u istom commitu.

---

## Zlatna pravila

Ovih šest se ne pregovara. Rješenje koje krši bilo koje od njih je pogrešno, bez obzira koliko je elegantno.

1. **`Unknown` nikad ne postaje `Normal`.** Nemamo podatak i nema opasnosti su dvije različite stvari — u modelu, u boji, u tekstu, u svakoj grani koda.
2. **Nikad ne prikazuj stari podatak kao svjež.** Svaka vrijednost nosi vidljiv timestamp iz `MeasuredAt` (kad je izmjereno), ne `FetchedAt` (kad smo povukli).
3. **Nikad ne izmišljaj status.** Pragove definiše hidrolog. Ako izvor kaže "Normalno", pišeš "Normalno" — ne izvodiš vlastitu kategorizaciju iz sirovih centimetara.
4. **Atribucija po stanici**, ne globalno u footeru. Uvijek ime agencije i link.
5. **Pad jednog izvora ne ruši aplikaciju.** Adapteri su izolovani — jedan timeout je jedan sivi sloj, ne prazan ekran. Kad fetch ne uspije, stari podatak ostaje sa poštenim timestampom; nikad se ne briše.
6. **Browser nikad ne gađa izvorne servere.** Sve kroz naš backend i keš. Njihova infrastruktura je javna imovina.

Ovo je aplikacija u kojoj netačan podatak može navesti nekoga na pogrešnu odluku o vlastitoj sigurnosti. Kad biraš između "izgleda ljepše" i "pošteno prikazuje neizvjesnost" — biraj drugo, svaki put.

---

## Komande

```bash
docker compose up -d                  # Postgres + PostGIS + Redis — TRAŽE GA Data testovi
dotnet build
dotnet test                           # Data testovi padaju bez baze, i to namjerno
dotnet run --project src/Vodostaji.Api

# Migracije. Startup je zasad Data jer Api još ne postoji — kad dođe, prebaci na njega.
dotnet ef migrations add <Name> -p src/Vodostaji.Data -s src/Vodostaji.Data
dotnet ef database update -p src/Vodostaji.Data -s src/Vodostaji.Data

dotnet run --project tools/Probe                        # snimi fixtures sa izvora
dotnet run --project tools/Probe -- --watch 20 --cycles 72   # mjeri pomak vremenskih zona

cd src/Vodostaji.Web
npm run dev
npm run build
npm run typecheck
npm test                              # Vitest, čista logika bez DOM-a
npm run generate:api                  # TS tipovi iz OpenAPI sheme — API mora raditi
```

Ako komanda ne postoji jer faza još nije došla — dodaj je ovdje čim je napraviš.

---

## Stack

.NET 8 / ASP.NET Core Minimal API · PostgreSQL 16 + PostGIS · Redis · React 18 + TypeScript + Vite · **MapLibre GL JS** · Tailwind · Recharts · TanStack Query · AngleSharp (scraping) · Serilog · xUnit · Vitest · Azure App Service.

Ne uvodi nove biblioteke bez pitanja. Obrazloži zašto prije nego dodaš.

---

## Struktura

```
src/Vodostaji.Api      endpointi, DI
src/Vodostaji.Core     domenski modeli, IStationDataSource
src/Vodostaji.Ingest   adapteri po izvoru — srce projekta
src/Vodostaji.Data     EF Core, migracije
src/Vodostaji.Web      React
tools/Probe            verifikacija izvora
tests/fixtures         snimljeni odgovori izvora, sa datumom u imenu
docs/                  vidi ispod
```

---

## Detaljna dokumentacija

Pročitaj relevantan fajl **prije** rada na toj oblasti. Ne radi iz sjećanja na ovaj sažetak.

| Fajl | Kad ga čitaš |
|---|---|
| `docs/DOMAIN.md` | prije bilo kakvog rada s podacima — institucionalna fragmentacija, terminologija, model podataka |
| `docs/SOURCES.md` | prije pisanja ili izmjene adaptera — specifikacije svakog izvora |
| `docs/UI.md` | prije rada na frontendu ili mapi |
| `docs/LEGAL.md` | prije mijenjanja atribucije, disclaimera ili pozicioniranja |
| `docs/ROADMAP.md` | prije nego počneš novu fazu |

---

## Konvencije

**C#** — nullable enabled, warnings as errors · `record` za modele i DTO-e · `decimal` za mjerenja, nikad `float`/`double` · `DateTimeOffset`, nikad goli `DateTime` · sve u bazi UTC (`timestamptz`) · bez `async void` · `CancellationToken` kroz cijeli lanac · Serilog sa `SourceId` u svakom ingest logu.

**TypeScript** — `strict: true`, bez `any` · tipovi API odgovora generisani iz OpenAPI sheme, ne pisani ručno · TanStack Query za dohvat, nikad `useEffect`.

**Testovi** — svaki adapter ima fixture testove protiv snimljenih odgovora · testovi vremenskih zona obavezni, uključujući DST prelaz · test da `Unknown` ne može postati `Normal` ni u jednoj putanji.

**Commiti** — conventional commits, engleski.

---

## Šta NE raditi

- Ne dodavati ArcGIS JS API (pretežak, MapLibre je dovoljan)
- Ne izvoditi vlastite pragove iz sirovih cm vrijednosti
- Ne stapati slojeve različitih agencija u jedan sloj sa jednom legendom
- Ne kešovati agresivnije nego što izvor osvježava — to je lažna svježina
- Ne gađati izvorne servere češće od 10 minuta
- Ne pisati parsere regexom nad HTML-om (AngleSharp)
- Ne slati notifikacije prije Faze 5
- Ne dodavati reklame, profilirajuću analitiku, ni bilo šta što usporava učitavanje na slaboj vezi
- Ne mijenjati tekst disclaimera bez dogovora
- Ne pisati kod prije nego je Faza 0 završena

---

## Kad nisi siguran

Pitaj prije nego pretpostaviš — posebno oko semantike pragova, mapiranja statusa, pravnih pitanja, i bilo čega što utiče na to kako korisnik procjenjuje rizik od poplave.
