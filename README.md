# Inteligentny System Zarządzania Akwarium

## Opis projektu

Projekt inżynierski przedstawiający inteligentny system zarządzania akwarium wykorzystujący aplikację mobilna napisaną w technologii .NET MAUI, aplikację webową w technologii ASP.NET Core, bazę danych SQL oraz mikrokontroler esp32. Wszystkie podzespoły komunikują się
z lokalnym API, która stanowi warstwę serwerową systemu

Celem projektu jest monitorowanie parametrów akwarium, analiza danych środowiskowych oraz automatyzacja wybranych procesów związanych z utrzymaniem odpowiednich warunków dla organizmów wodnych.

---

## Główne funkcjonalności

* monitorowanie parametrów akwarium
* komunikacja aplikacji z mikrokontrolerem Arduino
* zapis danych do bazy SQL
* analiza i prezentacja danych środowiskowych
* interfejs użytkownika w technologii ASP.NET Core oraz .NET MAUI
* możliwość dalszej rozbudowy o automatyczne sterowanie urządzeniami

---

## Technologie

### Backend / Aplikacja

* C#
* .NET MAUI, ASP.NET Core, Minimal Web API
* Entity Framework Core
* Razor Pages

### Baza danych

* SQL Server
* SQL

### Hardware

* esp32
* czujniki środowiskowe
* urządzenia sterujące

### Narzędzia

* Visual Studio
* Git
* SSMS
* Arduino

---

## Architektura projektu

System składa się z trzech głównych elementów:

1. Aplikacji webowej oraz mobilnej odpowiedzialych za komunikację z użytkownikiem
2. Mikrokontrolera esp32 zbierającego dane z czujników
3. Bazy danych przechowującej historię pomiarów oraz parametry systemu

---

## Przykładowe funkcje systemu

* odczyt temperatury wody
* zapis danych pomiarowych
* analiza parametrów środowiskowych
* prezentacja danych użytkownikowi
* podstawy automatyzacji zarządzania akwarium

---

## Cel projektu

Celem projektu było stworzenie systemu wspierającego zarządzanie akwarium poprzez wykorzystanie technologii informatycznych, baz danych oraz mikrokontrolerów.

Projekt pozwolił na rozwój umiejętności związanych z:

* programowaniem w języku C#
* projektowaniem baz danych
* komunikacją z urządzeniami zewnętrznymi
* analizą danych
* tworzeniem aplikacji desktopowych

---

## Autor

Jakub Kacprzycki

Projekt inżynierski — Informatyka
Akademia Mazowiecka w Płocku
