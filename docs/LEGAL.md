# LEGAL.md — pravni okvir i status dozvola

> Nije pravni savjet. Radna procjena i dnevnik prepiske.

## Trenutni status: **javni podaci, bez pisane saglasnosti, bez naplate**

## 1. Procjena

**Sirove činjenice nisu autorsko djelo.** Izmjereni vodostaj — "Sana kod Sanskog Mosta, 142 cm, 14:00" — nije zaštićen autorskim pravom.

**Ali baza jeste.** Zakon o autorskom i srodnim pravima BiH uređuje sui generis pravo proizvođača baze podataka i usklađen je sa EU Direktivom 96/9/EC. To pravo štiti **znatan dio sadržaja** baze od izvlačenja i ponovnog korištenja, čak i kad su pojedinačni podaci obične činjenice. Sistematsko povlačenje svih stanica i građenje vlastite historijske baze je tačno taj scenarij.

**Otvoren endpoint nije licenca** — samo odsustvo zaključavanja. AVP Sava je ostavio ArcGIS otvoren, ali nije rekao "izvolite". Za razliku od, recimo, CBBH-a koji uz kursnu listu nudi eksplicitne JSON/XML/CSV export dugmad — to je implicitna dozvola za mašinsko korištenje.

**Zakon o slobodi pristupa informacijama.** Agencije su javna tijela. Formalni zahtjev za podacima je jači osnov od "skidao sam sa sajta" i obavezuje ih na odgovor u roku.

## 2. Obavezno u aplikaciji

1. Atribucija **po stanici**, sa imenom agencije i linkom na izvor
2. Disclaimer da podaci nisu zvanični i ne služe za odbranu od poplava
3. Nikad ne prezentirati podatak kao naše mjerenje
4. **Bez reklama i bez naplate** dok ne postoji pisana saglasnost
5. Rate limiti i `robots.txt` poštovani; ako izvor uvede `X-RateLimit`, adapter ga poštuje
6. `User-Agent` koji identifikuje aplikaciju i sadrži kontakt

Ovih šest čine poziciju u kojoj, praktično, nema ko da se buni.

## 3. Strategija kontakta

**Redoslijed je bitan i suprotan od intuitivnog.**

Mail koji kaže *"planiram napraviti aplikaciju, možete li mi dati pristup"* ostane bez odgovora tri mjeseca — neko ga proslijedi nekome i tu umre.

Mail koji kaže *"napravio sam ovo, evo linka, radi, koristi vaše javne podatke, htio bih to formalizovati i staviti vaše ime kao izvor"* dobija odgovor. Donio si gotovu stvar koja njima koristi, a ne zahtjev.

**Zato: kontakt ide u Fazi 6, sa aplikacijom kao argumentom.**

Šalji sva tri maila istovremeno. Ako dva odgovore pozitivno, treći se lakše ubjeđuje argumentom da su ostali već pristali.

| Institucija | Kontakt | Šta tražimo |
|---|---|---|
| AVP Sava | `info@voda.ba` | potvrda korištenja + stabilan endpoint + najava promjena |
| AVP Jadranskog mora | `jsliv@jadran.ba` | pristup `isvportal.jadran.ba` (zamjena scrapera) |
| JU "Vode Srpske" / RHMZ RS | — | bilo kakav strukturirani feed umjesto PDF biltena |
| ISRBC | — | pristup Sava HIS |

## 4. Dnevnik

Svaki kontakt se upisuje ovdje, sa datumom. Pisana saglasnost mijenja šta aplikacija smije tvrditi o sebi — i **samo** upis u ovu tabelu opravdava izmjenu pozicioniranja ili disclaimera.

| Datum | Institucija | Kanal | Ishod |
|---|---|---|---|
| — | — | — | još ništa |
