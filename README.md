# 🔗 IKO Nexo Bridge

**Windows Service do integracji aplikacji IKO Mobile z InsERT nexo PRO**

---

## 📋 Spis treści

1. [Opis działania](#opis-działania)
2. [Wymagania systemowe](#wymagania-systemowe)
3. [Instalacja krok po kroku](#instalacja-krok-po-kroku)
4. [Konfiguracja połączenia z nexo PRO](#konfiguracja-połączenia-z-nexo-pro)
5. [Uruchomienie i testowanie](#uruchomienie-i-testowanie)
6. [Instalacja jako Windows Service](#instalacja-jako-windows-service)
7. [Rozwiązywanie problemów](#rozwiązywanie-problemów)

---

## 📊 Opis działania

```
┌─────────────────┐                    ┌─────────────────┐
│  📱 Tablet      │                    │  🔗 Nexo Bridge │
│  (aplikacja)    │                    │  (ten serwis)   │
│                 │     INTERNET       │                 │
│  Zamówienie ────┼───────────────────▶│  Pobiera        │
│  od handlowca   │                    │  zamówienia     │
└─────────────────┘                    │                 │
                                       │        ▼        │
┌─────────────────┐                    │  Tworzy dok.    │
│  ☁️ Cloud API   │◀───────────────────│  ZO w nexo      │
│  (serwer)       │                    │                 │
│                 │                    │        ▼        │
│  Produkty ──────┼───────────────────▶│  Aktualizuje    │
│  Klienci        │                    │  status         │
└─────────────────┘                    └────────┬────────┘
                                                │
                                                ▼
                                       ┌─────────────────┐
                                       │  📊 InsERT      │
                                       │  nexo PRO       │
                                       │  (SQL Server)   │
                                       │                 │
                                       │  • Produkty     │
                                       │  • Kontrahenci  │
                                       │  • Zamówienia   │
                                       └─────────────────┘
```

### Co robi Nexo Bridge:

1. **Pobiera zamówienia** z Cloud API (składane przez handlowców w aplikacji)
2. **Tworzy dokumenty ZO** (Zamówienie od Odbiorcy) w nexo PRO
3. **Synchronizuje produkty** z nexo PRO → Cloud API → aplikacja mobilna
4. **Synchronizuje kontrahentów** z nexo PRO → Cloud API → aplikacja mobilna
5. **Raportuje statusy** zamówień do Cloud API

---

## 💻 Wymagania systemowe

### Serwer/komputer z Nexo Bridge:
- **System**: Windows 10/11 lub Windows Server 2016+
- **Runtime**: .NET 8.0 Runtime
- **Sieć**: Dostęp do internetu (Cloud API) i sieci lokalnej (SQL Server nexo)
- **Pamięć**: Min. 512 MB RAM
- **Dysk**: Min. 100 MB wolnego miejsca

### InsERT nexo PRO:
- **Wersja**: nexo PRO 30+ (zalecana najnowsza)
- **Baza danych**: SQL Server (Express, Standard lub Enterprise)
- **Licencja Sfera**: Opcjonalnie - dla pełnej integracji przez SDK

### Cloud API:
- Działające API pod publicznym adresem URL
- Skonfigurowany klucz API dla Bridge

---

## 🚀 Instalacja krok po kroku

### Krok 1: Zainstaluj .NET 8.0 Runtime

Pobierz i zainstaluj z: https://dotnet.microsoft.com/download/dotnet/8.0

```powershell
# Sprawdź wersję po instalacji
dotnet --version
```

### Krok 2: Pobierz Nexo Bridge

```powershell
# Sklonuj repozytorium
git clone https://github.com/mastermi-ai/iko-nexo-bridge.git
cd iko-nexo-bridge

# Lub pobierz ZIP z GitHub i rozpakuj
```

### Krok 3: Zbuduj projekt

```powershell
# Przywróć zależności i zbuduj
dotnet restore
dotnet build --configuration Release

# Opcjonalnie: opublikuj jako pojedynczy plik
dotnet publish -c Release -o ./publish --self-contained false
```

---

## ⚙️ Konfiguracja połączenia z nexo PRO

### Edytuj plik `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },

  "CloudApi": {
    "BaseUrl": "https://TWOJ-SERWER.com",
    "ApiKey": "TWOJ-KLUCZ-API-DLA-BRIDGE",
    "ClientId": 1,
    "PollingIntervalSeconds": 30,
    "RequestTimeoutSeconds": 60
  },

  "NexoPro": {
    "ServerName": "NAZWA-SERWERA-SQL\\NEXO",
    "DatabaseName": "NexoPRO",
    "Username": "",
    "Password": "",
    "OperatorSymbol": "ADMIN",
    "OperatorPassword": "haslo_operatora"
  },

  "Sync": {
    "SyncOrdersEnabled": true,
    "SyncProductsEnabled": true,
    "SyncCustomersEnabled": true,
    "ProductsSyncIntervalMinutes": 60,
    "CustomersSyncIntervalMinutes": 60
  }
}
```

### Wyjaśnienie parametrów:

#### CloudApi:
| Parametr | Opis | Przykład |
|----------|------|----------|
| `BaseUrl` | Adres Cloud API | `https://api.iko-mobile.pl` |
| `ApiKey` | Klucz autoryzacji Bridge | `bridge-secret-key-12345` |
| `ClientId` | ID klienta w systemie | `1` |
| `PollingIntervalSeconds` | Co ile sekund sprawdzać zamówienia | `30` |

#### NexoPro:
| Parametr | Opis | Przykład |
|----------|------|----------|
| `ServerName` | Nazwa serwera SQL | `SERWER\NEXO` lub `localhost\SQLEXPRESS` |
| `DatabaseName` | Nazwa bazy nexo | `NexoPRO` lub `Nexo_FirmaXYZ` |
| `Username` | Login SQL (puste = Windows Auth) | `sa` lub `` |
| `Password` | Hasło SQL | `haslo123` |
| `OperatorSymbol` | Symbol operatora nexo | `ADMIN` |
| `OperatorPassword` | Hasło operatora nexo | `haslo_operatora` |

---

## 🔍 Jak znaleźć dane połączenia do nexo PRO

### 1. Nazwa serwera SQL:

Otwórz **SQL Server Management Studio** i skopiuj nazwę serwera z okna logowania.

Lub uruchom w CMD:
```cmd
sqlcmd -L
```

### 2. Nazwa bazy danych:

W nexo PRO: **Narzędzia** → **Opcje** → zakładka **Baza danych**

Lub w SQL Server Management Studio sprawdź listę baz - szukaj `Nexo...` lub `NexoPRO...`

### 3. Operator nexo:

W nexo PRO: **Administracja** → **Operatorzy** → wybierz operatora z uprawnieniami do tworzenia dokumentów

---

## ▶️ Uruchomienie i testowanie

### Uruchom w trybie konsoli (do testów):

```powershell
cd iko-nexo-bridge
dotnet run
```

### Co powinieneś zobaczyć:

```
info: IkoNexoBridge[0]
      IKO Nexo Bridge Worker starting...
info: IkoNexoBridge.Services.NexoSferaService[0]
      Connecting to nexo PRO: SERWER\NEXO/NexoPRO
info: IkoNexoBridge.Services.NexoSferaService[0]
      Successfully connected to nexo PRO database via SQL
info: IkoNexoBridge[0]
      Processing 0 pending orders
```

### Test połączenia z Cloud API:

```powershell
# Sprawdź czy API odpowiada
curl https://TWOJ-SERWER.com/bridge/health -H "X-Bridge-Api-Key: TWOJ-KLUCZ"
```

### Test połączenia z bazą nexo:

```powershell
# W SQL Server Management Studio wykonaj:
SELECT TOP 10 tw_Symbol, tw_Nazwa FROM tw__Towar
```

---

## 🖥️ Instalacja jako Windows Service

### Krok 1: Opublikuj aplikację

```powershell
dotnet publish -c Release -o C:\IkoNexoBridge --self-contained false
```

### Krok 2: Skopiuj appsettings.json

```powershell
copy appsettings.json C:\IkoNexoBridge\
```

### Krok 3: Zainstaluj jako serwis

**Jako Administrator** uruchom PowerShell:

```powershell
# Utwórz serwis Windows
sc.exe create "IkoNexoBridge" binPath="C:\IkoNexoBridge\IkoNexoBridge.exe" start=auto displayname="IKO Nexo Bridge"

# Ustaw opis
sc.exe description "IkoNexoBridge" "Serwis integracji IKO Mobile z InsERT nexo PRO"

# Uruchom serwis
sc.exe start IkoNexoBridge

# Sprawdź status
sc.exe query IkoNexoBridge
```

### Krok 4: Konfiguracja automatycznego restartu

```powershell
# Ustaw automatyczny restart przy awarii
sc.exe failure IkoNexoBridge reset=86400 actions=restart/60000/restart/60000/restart/60000
```

### Zarządzanie serwisem:

```powershell
# Zatrzymaj serwis
sc.exe stop IkoNexoBridge

# Uruchom serwis
sc.exe start IkoNexoBridge

# Usuń serwis (gdy chcesz odinstalować)
sc.exe delete IkoNexoBridge
```

---

## 📝 Logi i monitoring

### Lokalizacja logów:

```
C:\IkoNexoBridge\logs\
```

### Podgląd logów w czasie rzeczywistym:

```powershell
Get-Content C:\IkoNexoBridge\logs\nexo-bridge-*.log -Tail 50 -Wait
```

### Logi w Windows Event Viewer:

1. Otwórz **Event Viewer** (eventvwr.msc)
2. Przejdź do: **Applications and Services Logs** → **IkoNexoBridge**

---

## 🔧 Rozwiązywanie problemów

### Problem: Nie można połączyć się z SQL Server

**Sprawdź:**
1. Czy SQL Server jest uruchomiony
2. Czy nazwa serwera jest poprawna
3. Czy firewall nie blokuje portu 1433
4. Czy użytkownik ma uprawnienia do bazy

```powershell
# Test połączenia SQL
sqlcmd -S "SERWER\NEXO" -d "NexoPRO" -Q "SELECT 1"
```

### Problem: Błąd autoryzacji do Cloud API

**Sprawdź:**
1. Czy `ApiKey` jest poprawny
2. Czy `BaseUrl` jest dostępny z tego serwera
3. Czy firewall nie blokuje połączeń wychodzących HTTPS

```powershell
# Test połączenia z API
Invoke-WebRequest -Uri "https://TWOJ-SERWER.com/bridge/health" -Headers @{"X-Bridge-Api-Key"="TWOJ-KLUCZ"}
```

### Problem: Zamówienia nie są przetwarzane

**Sprawdź:**
1. Czy są zamówienia w statusie `pending` w Cloud API
2. Czy `SyncOrdersEnabled` = `true`
3. Logi w konsoli/pliku

### Problem: Produkty/klienci nie synchronizują się

**Sprawdź:**
1. Czy `SyncProductsEnabled` / `SyncCustomersEnabled` = `true`
2. Czy minął interwał synchronizacji
3. Czy są produkty/klienci w bazie nexo

---

## 📞 Kontakt i wsparcie

W razie problemów:
1. Sprawdź logi aplikacji
2. Sprawdź dokumentację InsERT Sfera: https://www.insert.com.pl/programy_insert/sfera_dla_programistow
3. Skontaktuj się z zespołem PRODAUT

---

## 📄 Licencja

© 2026 PRODAUT. Wszelkie prawa zastrzeżone.
