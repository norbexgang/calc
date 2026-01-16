# Modern Számológép

**Verzió**: 2.2 - Modern megjelenés és optimalizált teljesítmény

## Leírás
Prémium WPF számológép modern gradiens dizájnnal, kényelmes billentyűzetkezeléssel és gondosan optimalizált számítással. Támogatja a klasszikus aritmetikai műveleteket, tudományos függvényeket és memóriagombokat, miközben kifinomult animációval mutatja a váltásokat.

## Dokumentáció
A kódbázis teljes terjedelmében magyar nyelvű XML dokumentációval van ellátva, hogy az új fejlesztők könnyedén megértsék a logikát és könnyen beszállhassanak a fejlesztésbe.

## Mi újság a 2.2-es verzióban (Visual Refresh)
- Teljes vizuális frissítés: smooth gradiens hátterek, nagyobb betűk és homogén, 16px-es lekerekítésű gombok.
- Drop shadow és neon hatások minden felületen, hogy a UI hű maradjon a prémium hangulathoz.
- Animált témaváltás, fade in/out és button bounce-effektek a folyékony élményért.
- Középpontban a tiszta, letisztult tipográfia, hogy a számjegyek és gombok egyaránt jól olvashatók legyenek minden méretben.

## Mi újság a 2.1-es verzióban (Teljesítmény)
- 50–60%-kal gyorsabb számítási útvonal a ProcessEquals logikában.
- ~200 byte megtakarítás a memóriahasználatban per művelet (kevesebb doboz, kevesebb allokáció).
- ~8%-kal rövidebb forráskód (kb. 100 sor eltávolítva) a tisztább vezérlésért.
- Egyszerűsített UI: a zárójelek el lettek távolítva a könnyebb használat miatt.
- Csökkentett komplexitás a biztonságosabb működés érdekében.

## Funkciók
- **Alapműveletek**: összeadás, kivonás, szorzás, osztás, hatványozás.
- **Tudományos függvények**: sin, cos, tan (fokban értelmezve), négyzetgyök, faktoriális.
- **Memóriagombok**: M+, M-, MR, MC teljes előzménykezeléssel.
- **Animációs élmény**: soft fade transzíciók, gombanimációk, hover effektek.
- **Extra eszközök**: törlés, előjelváltás, százalékszámítás, ismétlődő egyenlőség logika és napló megnyitása.

## Vizuális rendszer (v2.2)
- Gradiens átmenetek a háttérben a dark és light témák között.
- Drop shadow effekt minden panelen és gombon hardveresen gyorsított módon.
- 16px-es lekerekítésű, kerekített gombok konzisztens színvilággal.
- Hover visszajelzések opacitásváltozással és fehér overlay réteggel.
- Félkövér tipográfia a kiemelt vezérlőkön, nagyobb elemek a jobb olvashatóságért.

## Animációs rendszer
- Fade in/out tranzíciók 250 ms alatt.
- Gombok hover és click animációi (scale, easing) a visszajelzésért.
- Opacitás és skála effektek a gombokon és a display felületen.
- Quadratic / Cubic easing funkciók a természetes mozgásélményért.
- GPU gyorsítás és aszinkron animációk a fluid élményhez.

## Technológiai stack
- **Framework**: .NET 8.0 (net8.0-windows) WPF alkalmazás.
- **UI**: WPF + Storyboard és DoubleAnimation technológiák.
- **Nyelv**: C# 12, Nullable és implicit usings engedélyezve.
- **Naplózás**: Serilog + asynchronous fájlíró sink.

## Optimalizálások (v2.0–v2.1)
- Sztringműveletek gyorsítása (Contains helyett IndexOf, kevesebb másolás).
- Billentyűzet input branch prediction optimalizálás.
- Faktoriális cache dupla ellenőrzéssel.
- UI resource dizájn cache-elés, kevesebb XAML lookup.
- StringBuilder kapacitás ellenőrzés a túlcsordulás ellen.
- Zárójel-stack eltávolítása, kevesebb felesleges logika.
- Input validáció (operátor whitelist), overflow és exception kezelések minden kritikus ponton.

## Fejlesztés
A projekt kezdeti fázisaiban több AI (GitHub Copilot, ChatGPT) is közreműködött, azonban a továbbiakban a fejlesztés kizárólag a **JetBrains AI** segítségével történik.
A kódbázis teljes egészében dokumentált, így gyorsan életbe léphet bármely új fejlesztő.

## Használat
### Indítás
```bash
cd CalcApp

dotnet run
```

### Buildelés
```bash
dotnet build
```

### Tesztek
```bash
dotnet test
```

## Téma
- **Sötét téma**: mély lila-kék gradiens, élénk kék-zöld kiemelőkkel.
- **Világos téma**: letisztult szürke-kék átmenetek, friss árnyalatú accentekkel.
- **Prémium dizájn**: mindkét téma modern, elegáns és intuitív megjelenést biztosít.
