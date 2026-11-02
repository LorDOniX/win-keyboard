# Česká klávesnice

Kompaktní plovoucí klávesnice pro Windows 10/11, české rozložení QWERTZ.

## Spuštění

Spusťte `dist\CeskaKlavesnice.exe`. Instalace není nutná. Aplikace používá .NET Framework 4.x, který je součástí Windows 10/11.

Klikněte do textového pole v cílové aplikaci a potom na klávesy. Okno zůstává vždy navrchu a kliknutí na klávesy nepřebírá aktivní textové pole. Přesun okna funguje za standardní záhlaví, velikost lze měnit tažením za okraj. Poloha a velikost se při zavření ukládají do `%LOCALAPPDATA%\CeskaKlavesnice\window.txt`.

Spodní řádek začíná informací **CZ / QWERTZ**. Vpravo je přepínač **Tmavý / Světlý** (popisek ukazuje aktuální režim). Výchozí je tmavý vzhled. Volba se ukládá ihned do `%LOCALAPPDATA%\CeskaKlavesnice\theme.txt` a při příštím spuštění se automaticky obnoví.

Vedle přepínače tématu je přepínač **Tučné**, který ztuční písmo kláves i spodního řádku. Výchozí stav je vypnutý. Volba se ihned ukládá do `%LOCALAPPDATA%\CeskaKlavesnice\typography.txt` a obnovuje při dalším spuštění.

## Předvyplněné texty

Tlačítko **+** vedle přepínače **Tučné** otevře popup s texty ze souboru `predvolene-texty.txt` v kořenovém adresáři zdrojů. Soubor upravujte v UTF-8. Každý neprázdný řádek vytvoří jedno tlačítko; prázdné řádky a řádky obsahující jen bílé znaky se vynechají. Ostatní obsah včetně počátečních a koncových mezer se zachová.

Po úpravě souboru spusťte znovu `build.ps1`. Texty jsou součástí sestaveného EXE, takže soubor není potřeba přenášet k aplikaci. Kliknutí vloží celý vybraný řádek do aktivního pole na pozici kurzoru, bez přidání Enteru, a popup zavře. Nabídka nepřebírá fokus, respektuje téma i tučné písmo, dlouhé texty zalamuje a delší seznam umožňuje posouvat. Zavřete ji také tlačítkem ×, opětovným kliknutím na + nebo kliknutím mimo nabídku.

Sestavení lze spustit i dvojklikem na `build.cmd` v kořeni projektu. Okno zůstane po dokončení otevřené s výsledkem. Pro spuštění bez čekání použijte `build.cmd --no-pause`.

## Rozložení a ovládání

- Horní řádek: Esc, F1–F12, Delete.
- Samostatný řádek **1 2 3 4 5 6 7 8 9 0**, Home, End, Page Up, Page Down.
- Pod ním standardní český řádek **+ ě š č ř ž ý á í é**, interpunkce a Backspace.
- Písmena v rozložení **QWERTZ**, české ú/ů, Enter, Tab, mezerník a šipky.
- **Shift, Ctrl, Alt, Win a AltGr** se kliknutím zapnou pro následující klávesu. Opětovné kliknutí je vypne. Lze složit např. Ctrl + Shift + šipka nebo Ctrl + C. Po odeslání se přepínače vypnou.
- **Caps** zůstává zapnutý do dalšího kliknutí a ovlivňuje písmena této klávesnice.
- **AltGr** zobrazí doplňkové znaky, např. @, €, {, }, [, ], \\ a |.
- **´ / ˇ** složí znaménko s následujícím písmenem (např. ó, ď, ť, ň). Pro háček zapněte Shift a klikněte na ´. Mezerník po znaménku vloží samotné znaménko. Backspace nebo Esc rozpracované znaménko zruší.
- Přidržení běžné klávesy, Backspace, Delete nebo navigační klávesy opakuje vstup.

České znaky i samostatné číslice se posílají jako Unicode pomocí Windows SendInput, nezávisle na zvoleném jazyku fyzické klávesnice. Navigace, funkční klávesy a zkratky se posílají jako systémové klávesy.

Windows dovoluje posílat vstup jen aplikacím se stejnými nebo nižšími oprávněními. Pro psaní do aplikace spuštěné jako správce spusťte také tuto klávesnici jako správce. Přihlašovací a zabezpečené obrazovky Windows nejsou podporované. Aplikace, které odmítají syntetický/Unicode vstup (například některé hry), nemusí reagovat na všechny klávesy.

## Sestavení ze zdrojů

V PowerShellu v adresáři projektu:

```powershell
.\build.ps1
```

Používá vestavěný kompilátor .NET Framework; nestahuje balíčky a nepotřebuje .NET SDK. Výstup je jediný soubor `dist\CeskaKlavesnice.exe`.

Implementace Windows API vychází z dokumentace Microsoftu: [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput), [KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput), [WM_MOUSEACTIVATE](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mouseactivate).
