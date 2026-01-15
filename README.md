# IKO Nexo Bridge

Windows Service łączący IKO Cloud API z systemem InsERT nexo PRO.

## 🎯 Funkcje

- **Przetwarzanie zamówień**: Automatyczne pobieranie zamówień z Cloud API i tworzenie dokumentów ZK (Zamówienie od Klienta) w nexo PRO
- **Synchronizacja produktów**: Okresowa synchronizacja towarów z nexo do Cloud API
- **Synchronizacja klientów**: Okresowa synchronizacja kontrahentów z nexo do Cloud API
- **Retry Policy**: Automatyczne ponawianie nieudanych operacji
- **Logowanie**: Szczegółowe logi operacji (Serilog)

## 📋 Wymagania

### System operacyjny
- Windows 10/11 lub Windows Server 2016+
- .NET 8.0 Runtime

### InsERT nexo PRO
- Zainstalowany InsERT nexo PRO z aktywną licencją
- Licencja Sfera (SDK dla programistów)
- Dostęp do bazy danych nexo (SQL Server)

### IKO Cloud API
- Działająca instancja IKO Cloud API
- Skonfigurowany klucz API dla Bridge

## 🚀 Instalacja

### 1. Przygotowanie środowiska

```powershell
# Zainstaluj .NET 8.0 Runtime jeśli nie jest zainstalowany
winget install Microsoft.DotNet.Runtime.8

# Sklonuj repozytorium
git clone https://github.com/mastermi-ai/iko-nexo-bridge.git
cd iko-nexo-bridge
```

### 2. Konfiguracja Sfera SDK

Skopiuj pliki biblioteki Sfera z instalacji nexo PRO:

```powershell
# Utwórz folder lib
mkdir lib

# Skopiuj biblioteki Sfera (przykładowa ścieżka)
copy "C:\Program Files\InsERT\nexo PRO\Sfera.dll" .\lib\
copy "C:\Program Files\InsERT\nexo PRO\Sfera.Model.dll" .\lib\
```

Odkomentuj sekcję referencji w `IkoNexoBridge.csproj`:

```xml
<ItemGroup>
  <Reference Include="Sfera">
    <HintPath>lib\Sfera.dll</HintPath>
  </Reference>
  <Reference Include="Sfera.Model">
    <HintPath>lib\Sfera.Model.dll</HintPath>
  </Reference>
</ItemGroup>
```

### 3. Konfiguracja aplikacji

Edytuj `appsettings.json`:

```json
{
  "CloudApi": {
    "BaseUrl": "https://your-cloud-api.com",
    "ApiKey": "YOUR_BRIDGE_API_KEY",
    "PollingIntervalSeconds": 30
  },
  "NexoPro": {
    "ServerName": "localhost\\NEXOPRO",
    "DatabaseName": "nexo_firma",
    "Username": "",
    "Password": "",
    "OperatorSymbol": "ADMIN",
    "OperatorPassword": "haslo123",
    "DefaultWarehouse": "MAG",
    "DefaultDocumentType": "ZK"
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

### 4. Build projektu

```powershell
dotnet build -c Release
```

### 5. Instalacja jako Windows Service

```powershell
# Jako Administrator
sc create "IKO Nexo Bridge" binPath="C:\path\to\IkoNexoBridge.exe" start=auto
sc description "IKO Nexo Bridge" "Synchronizacja IKO Cloud API z InsERT nexo PRO"

# Uruchom usługę
sc start "IKO Nexo Bridge"
```

Alternatywnie, możesz użyć .NET narzędzia:

```powershell
dotnet publish -c Release -o C:\Services\IkoNexoBridge

# Jako Administrator
New-Service -Name "IkoNexoBridge" -BinaryPathName "C:\Services\IkoNexoBridge\IkoNexoBridge.exe" -DisplayName "IKO Nexo Bridge" -StartupType Automatic -Description "Synchronizacja IKO Cloud API z InsERT nexo PRO"

Start-Service -Name "IkoNexoBridge"
```

## 🔧 Konfiguracja w Cloud API

Upewnij się, że w Cloud API jest skonfigurowany klucz API dla Bridge:

```env
# .env w iko-cloud-api
BRIDGE_API_KEY=your-secure-api-key-here
```

## 📊 Monitorowanie

### Logi
Logi są zapisywane w folderze `logs/`:
- `iko-nexo-bridge-YYYYMMDD.log` - dzienny plik logów

### Status usługi
```powershell
# Sprawdź status
sc query "IKO Nexo Bridge"

# Podgląd logów w czasie rzeczywistym
Get-Content -Path "logs\iko-nexo-bridge-*.log" -Wait
```

## 🔄 Przepływ danych

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Flutter App   │────▶│   Cloud API     │────▶│   Nexo Bridge   │
│   (Mobile)      │     │   (NestJS)      │     │   (Windows)     │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                                                         ▼
                                               ┌─────────────────┐
                                               │  nexo PRO       │
                                               │  (SQL Server)   │
                                               └─────────────────┘
```

### Zamówienia (Cloud → Nexo)
1. Użytkownik tworzy zamówienie w aplikacji Flutter
2. Zamówienie trafia do Cloud API (status: `pending`)
3. Bridge pobiera zamówienia `pending` co 30s
4. Bridge tworzy dokument ZK w nexo PRO
5. Bridge aktualizuje status na `completed` lub `failed`

### Produkty/Klienci (Nexo → Cloud)
1. Bridge co godzinę odczytuje dane z nexo
2. Dane są wysyłane do Cloud API
3. Cloud API zapisuje/aktualizuje rekordy
4. Aplikacje mobilne synchronizują dane z Cloud API

## 🛠️ Troubleshooting

### Błąd połączenia z nexo PRO
- Sprawdź czy SQL Server jest uruchomiony
- Sprawdź credentials w `appsettings.json`
- Upewnij się że operator ma odpowiednie uprawnienia

### Błąd połączenia z Cloud API
- Sprawdź czy `BaseUrl` jest poprawny
- Sprawdź czy `ApiKey` jest zgodny z konfiguracją Cloud API
- Sprawdź firewall/dostęp sieciowy

### Brak biblioteki Sfera
- Upewnij się że masz licencję Sfera
- Skopiuj biblioteki z instalacji nexo PRO
- Odkomentuj referencje w `.csproj`

## 📝 Status zamówień

| Status | Opis |
|--------|------|
| `pending` | Oczekuje na przetworzenie |
| `processing` | W trakcie przetwarzania |
| `completed` | Pomyślnie utworzono w nexo |
| `failed` | Błąd podczas przetwarzania |
| `created_in_nexo` | Dokument utworzony w nexo |

## 📄 Licencja

Proprietary - PRODAUT © 2026
