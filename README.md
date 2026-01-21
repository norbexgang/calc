# Modern Számológép

**Verzió**: 2.2 - Modern megjelenés és optimalizált teljesítmény

## Leírás
WPF számológép modern gradiens dizájnnal és gyors, pontos számításokkal. Támogatja az alapműveleteket, tudományos függvényeket és memóriafunkciókat, valamint finom animációkat a jobb használhatóságért.


## Funkciók
- **Alapműveletek**: összeadás, kivonás, szorzás, osztás, hatványozás.
- **Tudományos függvények**: sin, cos, tan (fokban értelmezve), négyzetgyök, faktoriális.
- **Memóriagombok**: M+, M-, MR, MC teljes előzménykezeléssel.
- **Animációs élmény**: soft fade transzíciók, gombanimációk, hover effektek.
- **Extra eszközök**: törlés, előjelváltás, százalékszámítás, ismétlődő egyenlőség logika és napló megnyitása.

## Technológiai stack
- **Framework**: .NET 8.0 (net8.0-windows) WPF alkalmazás.
- **UI**: WPF + Storyboard és DoubleAnimation technológiák.
- **Nyelv**: C# 12, Nullable és implicit usings engedélyezve.
- **Naplózás**: Serilog + asynchronous fájlíró sink.

## Fejlesztés
A projekt kezdeti fázisaiban több AI (GitHub Copilot, ChatGPT) is közreműködött; jelenleg az AI feladatokat kizárólag a **GitHub Copilot** látja el és segíti a fejlesztést.
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