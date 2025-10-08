# Számológép (Calculator) 🧮

## Leírás
Modern WPF számológép alkalmazás C# nyelven írva. Az alkalmazás támogatja az alap aritmetikai műveleteket, tudományos funkciókat és memória műveleteket.

## Funkciók
- **Alap műveletek**: összeadás, kivonás, szorzás, osztás
- **Tudományos funkciók**: sin, cos, tan, négyzetgyök, faktoriális
- **Memória műveletek**: M+, M-, MR, MC
- **Animált témaváltás**: 🎬 smooth animációkkal
  - 🌙 **Dark Mode**: Sötét Material Design téma
  - ☀️ **Light Mode**: Világos Classic téma  
  - **Animációs effektek**: fade transitions, button hover effects, click animations
- **További funkciók**: százalék számítás, előjel váltás, törlés, visszalépés

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

## Fejlesztés
A projekt fejlesztésében **GitHub Copilot**, **ChatGPT és a Codex Agentje** és a **Jetbrains AI** AI asszisztensek közreműködött a kód optimalizálásában és a fejlesztési folyamat gyorsításában.

## Indítás
```bash
cd CalcApp
dotnet run
```

## Build
```bash
dotnet build
```