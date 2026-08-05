# TrayMenu

Утилита для Windows 11: иконка в системном трее, меню из ярлыков `.lnk` в выбранной папке.

## Возможности

- Меню в трее из ярлыков; подпапки — вложенные подменю
- Иконки пунктов берутся из ярлыков
- Настройки: путь к папке, автозапуск с Windows
- Автообновление меню при изменении папки
- Один экземпляр приложения

Конфиг: `%AppData%\TrayMenu\config.json`

## Требования

- [.NET SDK](https://dotnet.microsoft.com/download) 10+ для сборки
- Windows 10/11 (нужен .NET Desktop Runtime 10, либо self-contained publish)

## Сборка и запуск

```powershell
dotnet build
dotnet run --project TrayMenu
```

Exe после сборки: `TrayMenu\bin\Debug\net10.0-windows\TrayMenu.exe`

Публикация без зависимости от установленного runtime:

```powershell
dotnet publish TrayMenu -c Release -r win-x64 --self-contained true -o publish
```

## Использование

1. Запустите приложение — появится иконка в трее.
2. ПКМ / ЛКМ по иконке → «Настройки…» → укажите папку с `.lnk`.
3. При необходимости включите «Запускать с Windows».
