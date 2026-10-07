#  Inteligentny System Zarządzania Akwarium

Kompleksowy system do **monitorowania, analizy i zarządzania akwarium**, łączący aplikację mobilną, aplikację webową, lokalne API, bazę danych SQL Server oraz mikrokontroler ESP32 z podłączonymi czujnikami i urządzeniami wykonawczymi.

Projekt został przygotowany jako **projekt inżynierski na kierunku Informatyka**.

---

##  O projekcie

Celem projektu było stworzenie systemu umożliwiającego bieżące monitorowanie parametrów akwarium oraz zdalne zarządzanie podłączonymi urządzeniami.

System zbiera dane z czujników podłączonych do ESP32, przekazuje je do warstwy API, a następnie zapisuje je w bazie danych. Użytkownik może przeglądać aktualne oraz historyczne pomiary, konfigurować progi alarmowe i sterować urządzeniami.

System składa się z kilku współpracujących ze sobą elementów:

*  aplikacji mobilnej .NET MAUI,
*  aplikacji webowej ASP.NET Core,
*  lokalnego REST API,
*  bazy danych Microsoft SQL Server,
*  mikrokontrolera ESP32,
*  czujników pomiarowych,
*  urządzeń wykonawczych sterowanych przez ESP32.

---

#  Architektura

```text
                    ┌─────────────────────┐
                    │    Aplikacja Web    │
                    │    ASP.NET Core     │
                    └──────────┬──────────┘
                               │
                               │ HTTP
                               ▼
┌─────────────────┐      ┌───────────────┐      ┌─────────────────┐
│ Aplikacja       │ HTTP │               │ SQL  │                 │
│ mobilna         ├─────►│  Akwarium API ├─────►│  SQL Server     │
│ .NET MAUI       │      │  Minimal API  │      │                 │
└─────────────────┘      └───────┬───────┘      └─────────────────┘
                                  │
                                  │ HTTP
                                  ▼
                         ┌─────────────────┐
                         │      ESP32      │
                         │                 │
                         │  Czujniki       │
                         │  Urządzenia     │
                         └─────────────────┘
```

API pełni rolę **warstwy serwerowej**, pośredniczącej pomiędzy aplikacjami, bazą danych oraz urządzeniem ESP32.

---

#  Główne funkcjonalności

##  Użytkownicy

* rejestracja użytkownika,
* logowanie przy użyciu nicku lub adresu e-mail,
* zmiana hasła,
* obsługa ról użytkowników,
* blokowanie użytkowników,
* generowanie linku umożliwiającego przejście z aplikacji mobilnej do aplikacji webowej.

##  Zarządzanie akwariami

* tworzenie akwarium,
* zmiana nazwy,
* usuwanie akwarium,
* wyświetlanie listy akwariów użytkownika,
* przypisywanie czujników do akwarium.

##  Czujniki

System umożliwia:

* dodawanie czujników,
* edycję nazwy i typu czujnika,
* konfigurację opisu,
* pobieranie aktualnych wartości,
* zapisywanie pomiarów,
* przeglądanie historii pomiarów,
* konfigurację minimalnych i maksymalnych wartości,
* generowanie alertów po przekroczeniu ustalonych progów.

##  Historia pomiarów

Dane pomiarowe są zapisywane wraz z czasem wykonania pomiaru.

API udostępnia możliwość pobierania historii dla wybranego czujnika z określonego przedziału czasu.

Aplikacja mobilna umożliwia wybór czujnika oraz zakresu dat i wyświetlenie jego historycznych pomiarów.

##  Alerty i powiadomienia

Użytkownik może określić:

* minimalną wartość dla czujnika,
* maksymalną wartość,
* aktywność alertu.

Aplikacja mobilna sprawdza aktualne wartości i może generować lokalne powiadomienia w przypadku przekroczenia ustalonych wartości.

Dodatkowo obsługiwane są cykliczne przypomnienia związane z obsługą akwarium.

##  Sterowanie urządzeniami

Aplikacja mobilna umożliwia sterowanie urządzeniami podłączonymi do ESP32, m.in.:

* oświetleniem,
* pompą,
* filtrem,
* grzałką,
* dodatkowymi urządzeniami użytkownika.

Sterowanie odbywa się poprzez API, które przekazuje odpowiednie polecenia do ESP32.

##  Harmonogramy

System umożliwia definiowanie harmonogramów pracy urządzeń.

Użytkownik może określić:

* urządzenie,
* dni tygodnia,
* godzinę rozpoczęcia,
* godzinę zakończenia.

Harmonogram może następnie zostać przekazany do ESP32.

##  Podgląd kamery

Aplikacja mobilna posiada również ekran przeznaczony do podglądu obrazu z kamery znajdującej się przy akwarium.

---

#  Aplikacja mobilna

Aplikacja została wykonana w technologii **.NET MAUI**.

Dostępne są m.in. ekrany:

* logowania,
* rejestracji,
* listy akwariów,
* panelu akwarium,
* aktualnych parametrów,
* historii pomiarów,
* sterowania urządzeniami,
* ustawień akwarium,
* konfiguracji alertów,
* harmonogramów,
* podglądu kamery.

Aplikacja komunikuje się z API za pomocą żądań HTTP i wykorzystuje format JSON do wymiany danych.

---

# Akwarium API

Warstwa serwerowa została wykonana jako **ASP.NET Core Minimal Web API**.

API odpowiada m.in. za:

* autoryzację użytkowników,
* zarządzanie akwariami,
* zarządzanie czujnikami,
* zapis i odczyt pomiarów,
* obsługę progów alarmowych,
* sterowanie urządzeniami,
* harmonogramy,
* powiadomienia,
* funkcje administracyjne.

### Przykładowe endpointy

```text
POST   /api/auth/login
POST   /api/auth/register

GET    /api/aquariums/{userId}
POST   /api/users/{userId}/aquariums
PUT    /api/aquariums/{aquariumId}/name
DELETE /api/users/{userId}/aquariums/{aquariumId}

GET    /api/aquariums/{aquariumId}/sensors
POST   /api/aquariums/{aquariumId}/sensors
PUT    /api/sensors/{sensorId}
DELETE /api/aquariums/{aquariumId}/sensors/{sensorId}

POST   /api/sensordata
GET    /api/aquariums/{aquariumId}/sensors/latest
GET    /api/sensors/{sensorId}/history

PUT    /api/sensors/{sensorId}/thresholds

POST   /api/devices/control
POST   /api/devices/schedule
POST   /api/devices/schedule/disable

GET    /api/admin/users
GET    /api/admin/aquariums
GET    /api/admin/sensors
GET    /api/admin/logs
GET    /api/admin/notifications
```

---

#  Baza danych

System wykorzystuje **Microsoft SQL Server**.

Baza przechowuje m.in.:

* użytkowników,
* akwaria,
* czujniki,
* dane pomiarowe,
* konfigurację progów,
* informacje potrzebne do obsługi urządzeń i harmonogramów.

Dane pomiarowe są zapisywane wraz z czasem ich otrzymania, dzięki czemu możliwe jest późniejsze analizowanie zmian parametrów akwarium.

---

# ESP32

ESP32 pełni funkcję warstwy sprzętowej systemu.

Mikrokontroler odpowiada za komunikację z czujnikami oraz urządzeniami wykonawczymi.

Przykładowy przepływ danych:

```text
Czujnik
   │
   ▼
 ESP32
   │
   │ HTTP
   ▼
Akwarium API
   │
   ▼
SQL Server
   │
   ▼
Aplikacja mobilna / webowa
```

W drugą stronę możliwe jest przesyłanie poleceń sterujących:

```text
Aplikacja
    │
    ▼
   API
    │
    ▼
  ESP32
    │
    ▼
Urządzenie
```

---

#  AkwariumShared

Projekt zawiera również bibliotekę `AkwariumShared`, która przechowuje współdzielone modele i DTO wykorzystywane przez poszczególne komponenty systemu.

Pozwala to ograniczyć duplikowanie definicji modeli pomiędzy aplikacją a API.

---

#  Technologie

### Backend

* C#
* .NET 8
* ASP.NET Core
* Minimal Web API
* HTTP / REST
* JSON

### Aplikacja mobilna

* .NET MAUI
* XAML
* C#

### Baza danych

* Microsoft SQL Server
* SQL
* Microsoft.Data.SqlClient

### Hardware

* ESP32
* czujniki środowiskowe
* urządzenia wykonawcze
* komunikacja HTTP

### Narzędzia

* Visual Studio
* SQL Server Management Studio
* Git
* GitHub

---

#  Struktura repozytorium

```text
Akwarium
│
├── Mobilna
│   │
│   ├── Mobilna
│   │   ├── Pages
│   │   ├── Models
│   │   ├── Resources
│   │   ├── Platforms
│   │   └── Services
│   │
│   ├── AkwariumApi
│   │   ├── Program.cs
│   │   ├── PasswordHasher.cs
│   │   └── Properties
│   │
│   ├── AkwariumShared
│   │   ├── Auth.cs
│   │   └── Dashboard
│   │
│   └── Mobilna.sln
│
└── README.md
```

---

#  Uruchomienie

Do uruchomienia pełnego systemu wymagane są:

* Visual Studio z obsługą .NET MAUI,
* .NET 8 SDK,
* Microsoft SQL Server,
* skonfigurowana baza danych,
* działający ESP32 w tej samej sieci,
* odpowiednia konfiguracja adresu API i urządzenia ESP32.

Przed uruchomieniem należy skonfigurować połączenie z bazą danych oraz adresy sieciowe API i ESP32 w plikach konfiguracyjnych projektu.

> **Uwaga:** adresy IP oraz dane dostępowe nie powinny być umieszczane w publicznym repozytorium. W środowisku produkcyjnym należy przechowywać je w zmiennych środowiskowych, Secret Managerze lub innym bezpiecznym mechanizmie konfiguracji.

---

#  Cel projektu

Projekt został wykonany w ramach pracy inżynierskiej na kierunku **Informatyka**.

Jego celem było połączenie kilku obszarów informatyki w jeden działający system:

* tworzenie aplikacji mobilnych,
* tworzenie aplikacji webowych,
* projektowanie i obsługa API,
* praca z relacyjną bazą danych,
* komunikacja z urządzeniami IoT,
* przetwarzanie i prezentacja danych,
* automatyzacja procesów.

---

#  Autor

**Jakub Kacprzycki**

Projekt inżynierski — Informatyka
Akademia Mazowiecka w Płocku
