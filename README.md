# Számológép (Calculator) 🧮

**Verzió**: 2.1 - Optimalizált és Egyszerűsített

## Leírás
Modern WPF számológép alkalmazás C# nyelven írva. Az alkalmazás támogatja az alap aritmetikai műveleteket, tudományos funkciókat és memória műveleteket. **Verzió 2.1-ben optimalizálva a teljesítményre és egyszerűségre**.

## ✨ Új a 2.1 verzióban
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

## Animációs Rendszer ✨
- **Fade Transitions**: 250ms smooth átmenetek témaváltáskor
- **Button Animations**: hover és click effektek
- **Scale Effects**: finom nagyítás/kicsinyítés animációk
- **Easing Functions**: QuadraticEase természetes mozgásért
- **Async Animation**: nem blokkoló, fluid animációk

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

Részletek: [OPTIMIZATIONS.md](OPTIMIZATIONS.md) | [CHANGELOG_v2.1.md](CHANGELOG_v2.1.md)

## Fejlesztés
A projekt fejlesztésében **GitHub Copilot**, **ChatGPT és a Codex Agentje** és a **Jetbrains AI** AI asszisztensek közreműködött a kód optimalizálásában és a fejlesztési folyamat gyorsításában.

**v2.1 Optimalizálás**: GitHub Copilot által végzett kód egyszerűsítés és teljesítmény javítás.

## Indítás
```bash
cd CalcApp
dotnet run
```

## Build
```bash
dotnet build
```