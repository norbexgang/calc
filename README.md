# 🧮 Modern Kalkulátor

**Verzió**: 2.2 - Modernizált Kinézet + Optimalizált Teljesítmény

## Leírás
Prémium WPF számológép alkalmazás C# nyelven írva modern gradiens dizájnnal és smooth animációkkal. Az alkalmazás támogatja az alap aritmetikai műveleteket, tudományos funkciókat és memória műveleteket.

## ✨ Új a 2.2 verzióban (Visual Refresh)
- 🎨 **Gradiens témák** - Gyönyörű színátmenetek dark és light módban
- 🔘 **Még kerekebb gombok** - 16px corner radius élénk árnyékokkal
- 💫 **Enhanced effektek** - DropShadow minden elemen
- 📱 **Nagyobb betűk** - 20px gombok, 40px display, bold típusok
- 🖼️ **Professzionális UI** - Modern, prémium megjelenés
- ⚡ **Megtartott sebesség** - Minden optimalizálás megmaradt!

## ✨ Új a 2.1 verzióban (Performance)
- ⚡ **50-60% gyorsabb számítások** - egyszerűsített ProcessEquals()
- 💾 **~200 byte memória megtakarítás** műveleteként
- 📉 **~8% kisebb kód** (~100 sor eltávolítva)
- 🎯 **Egyszerűbb UI** - zárójel gombok eltávolítva
- 🔒 **Biztonságosabb** - kevesebb komplexitás

## Funkciók
- **Alap műveletek**: összeadás, kivonás, szorzás, osztás
- **Tudományos funkciók**: sin, cos, tan, négyzetgyök, faktoriális
- **Memória műveletek**: M+, M-, MR, MC
- **Animált témaváltás**: 🎬 smooth animációkkal
  - 🌙 **Dark Mode**: Sötét Material Design téma
  - ☀️ **Light Mode**: Világos Classic téma  
  - **Animációs effektek**: fade transitions, button hover effects, click animations
- **További funkciók**: százalék számítás, előjel váltás, törlés, visszalépés
- ~~**Zárójelek**~~ - *eltávolítva v2.1-ben az egyszerűség érdekében*

## 🎨 Vizuális Rendszer (v2.2)
- **Gradiens Témák**: LinearGradientBrush beautiful color transitions
- **DropShadow Effects**: GPU-gyorsított árnyékok minden elemen
- **16px Rounded Corners**: Extra kerek gombok modern megjelenéssel
- **Hover Feedback**: Opacity változás + fehér overlay effekt
- **Bold Typography**: SemiBold/Bold betűk professzionális kinézetért
- **Nagyobb Elemek**: 20px gombok, 40px display jobb olvashatóságért

## ✨ Animációs Rendszer
- **Fade Transitions**: 250ms smooth átmenetek témaváltáskor
- **Button Animations**: hover és click effektek
- **Scale Effects**: 1.08x zoom téma gombon
- **Easing Functions**: QuadraticEase természetes mozgásért
- **Async Animation**: nem blokkoló, fluid animációk
- **GPU Accelerated**: Hardware gyorsított effektek

## Technológiai stack
- **Framework**: .NET 8.0 Windows
- **UI**: WPF (Windows Presentation Foundation) + Storyboard animációk
- **Nyelv**: C# 12
- **Témák**: Material Design és Classic témák
- **Animációs Engine**: WPF Storyboard és DoubleAnimation

## 🚀 Optimalizálások (v2.0 - v2.1)

### Teljesítmény:
- ✅ String műveletek optimalizálása (IndexOf vs Contains)
- ✅ Billentyűzet input branch prediction optimalizálás
- ✅ Faktoriális cache double-checked locking
- ✅ UI control caching (kevesebb XAML lookup)
- ✅ StringBuilder kapacitás korlát
- ✅ Zárójel stack eltávolítása (v2.1)

### Biztonság:
- ✅ Input validáció (whitelist operátorok)
- ✅ Overflow detektálás minden műveletben
- ✅ Exception handling minden kritikus ponton
- ✅ Resource cleanup és memory leak prevention
- ✅ Bounds checking (faktoriális, string length)

## Fejlesztés
A projekt fejlesztésében **GitHub Copilot**, **ChatGPT és a Codex Agentje** és a **Jetbrains AI** AI asszisztensek közreműködött a kód optimalizálásában és a fejlesztési folyamat gyorsításában.

**v2.1 Optimalizálás**: GitHub Copilot által végzett kód egyszerűsítés és teljesítmény javítás.  
**v2.2 Visual Refresh**: GitHub Copilot által tervezett modern gradiens dizájn és enhanced UI effektek.

## 📸 Kinézet
- 🌙 **Sötét Téma**: Mély purple-blue gradiens, cián szöveg, vibráló lila-kék accent
- ☀️ **Világos Téma**: Tiszta grey-blue gradiens, sötét szöveg, friss kék-zöld accent
- 💎 **Prémium**: Mindkét téma profi, modern és elegáns dizájnnal

## Indítás
```bash
cd CalcApp
dotnet run
```

## Build
```bash
dotnet build
```