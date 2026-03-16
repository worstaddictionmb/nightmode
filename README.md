# 🌙 Night Mode for Windows — Gelişmiş Sürüm

Mevcut Night Mode uygulamasının kararlı, yeniden yazılmış hali.

## Fark Ne?

| Özellik | Eski Uygulama | Bu Uygulama |
|---|---|---|
| Yöntem | Sadece Overlay penceresi | Gamma Ramp (önce), Overlay (yedek) |
| Video izlerken bozulma | ✗ Olur | ✓ Gamma'da olmaz |
| Sekme kapatırken sıfırlanma | ✗ Olur | ✓ WinEvent hook ile önlenir |
| Çoklu monitör | ✓ | ✓ |
| Başlangıçta aç | — | ✓ Registry ile |
| Klavye kısayolları | ✓ | ✓ (aynı) |

## Klavye Kısayolları

| Kısayol | İşlev |
|---|---|
| Ctrl + Win + F11 | Parlaklığı azalt (−5%) |
| Ctrl + Win + F12 | Parlaklığı artır (+5%) |
| Ctrl + Win + F9  | Boss key — maksimum karanlık |
| Ctrl + Win + F10 | Maksimum parlaklık (boss öncesine döner) |

## Derleme (Build)

### Gereksinimler
- .NET 6 SDK veya üstü: https://dotnet.microsoft.com/download

### Adımlar

```bash
cd NightMode
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Çıktı: `bin\Release\net6.0-windows\win-x64\publish\nightmode.exe`

### Alternatif: Sadece çalıştır (SDK varsa)
```bash
dotnet run
```

## Kullanım

```
nightmode.exe          → varsayılan %70 parlaklıkla başlar
nightmode.exe 50       → %50 parlaklıkla başlar
nightmode.exe 30       → %30 parlaklıkla başlar
```

## Gamma Ramp vs Overlay

Uygulama başlarken önce **Gamma Ramp** yöntemini dener.
- GPU sürücünüz destekliyorsa (Intel/AMD/NVIDIA — çoğu modern GPU) Gamma kullanır
- Gamma ile tüm pencereler, videolar, oyunlar dahil ekranın kendisi karartılır
- Gamma çalışmazsa otomatik olarak **Overlay** moduna geçer
- Hangi yöntemin aktif olduğu tray menüsünde gösterilir

## Notlar

- Sadece ana monitörü etkiler (Gamma Ramp Windows'ta tek monitörle çalışır)
- Birden fazla monitörde Overlay modu devreye girer, her monitöre ayrı overlay uygulanır
- Uygulama kapanırken gamma otomatik olarak eski haline döner
