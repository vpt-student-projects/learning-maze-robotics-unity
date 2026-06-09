<p align="center">
  <img src="docs/hero/banner.svg" width="100%" alt="Learning Maze Robotics Banner" />
</p>

<div align="center">

# 🤖 Learning Maze Robotics

### Unity-симулятор для обучения робототехнике, визуальному программированию и прохождению лабиринта

<p>
  <img alt="Unity" src="https://img.shields.io/badge/Unity-2022%2B-000000?style=for-the-badge&logo=unity&logoColor=white">
  <img alt="C#" src="https://img.shields.io/badge/C%23-Game%20Logic-68217A?style=for-the-badge&logo=csharp&logoColor=white">
  <img alt="ASP.NET Core" src="https://img.shields.io/badge/ASP.NET%20Core-REST%20API-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL-Database-336791?style=for-the-badge&logo=postgresql&logoColor=white">
  <img alt="Tests" src="https://img.shields.io/badge/Unity%20Test%20Runner-6%20Passed-brightgreen?style=for-the-badge">
</p>

<p>
  <a href="#-quick-start">
    <img src="https://img.shields.io/badge/▶%20RUN%20LOCALLY-Unity%20Project-bb2d2d?style=for-the-badge">
  </a>
  <a href="#-screenshots">
    <img src="https://img.shields.io/badge/🖼%20VIEW%20SCREENSHOTS-Project%20Gallery-8b0000?style=for-the-badge">
  </a>
  <a href="#-testing">
    <img src="https://img.shields.io/badge/✅%20TESTS-Unity%20Test%20Runner-success?style=for-the-badge">
  </a>
</p>

<p>
  <a href="#-about-the-project">About</a> •
  <a href="#-screenshots">Screenshots</a> •
  <a href="#-features">Features</a> •
  <a href="#-architecture">Architecture</a> •
  <a href="#-rest-api">API</a> •
  <a href="#-database">Database</a> •
  <a href="#-testing">Testing</a> •
  <a href="#-quick-start">Quick Start</a>
</p>

</div>

---

## 🧠 About the Project

**Learning Maze Robotics** — учебно-практическое приложение на **Unity**, предназначенное для демонстрации принципов алгоритмизации, робототехники и визуального программирования.

Пользователь проходит авторизацию, настраивает параметры генерации лабиринта, создаёт игровую среду и управляет виртуальной машинкой с помощью командных блоков. Данные о попытках прохождения и действиях машинки передаются через REST API и сохраняются в PostgreSQL.

Проект объединяет несколько частей в единую систему:

- интерактивный Unity-клиент;
- окно регистрации и входа;
- генерацию лабиринта по параметрам пользователя;
- визуальное программирование действий машинки;
- серверную часть на ASP.NET Core;
- базу данных PostgreSQL;
- автоматизированные тесты через Unity Test Runner.

---

## 🖼 Screenshots

| Окно входа | Генерация лабиринта |
|---|---|
| <img src="docs/screenshots/login-window.png" width="420" alt="Login Window"> | <img src="docs/screenshots/maze-generation.png" width="420" alt="Maze Generation"> |

| Игровая сцена | Визуальное программирование |
|---|---|
| <img src="docs/screenshots/main-scene.png" width="420" alt="Main Scene"> | <img src="docs/screenshots/visual-blocks.png" width="420" alt="Visual Blocks"> |

| Результаты тестирования |
|---|
| <img src="docs/screenshots/test-runner.png" width="860" alt="Unity Test Runner"> |

> Для корректного отображения скриншотов положи изображения в папку `docs/screenshots/` с именами:  
> `login-window.png`, `maze-generation.png`, `main-scene.png`, `visual-blocks.png`, `test-runner.png`.

---

## ✨ Features

| Модуль | Возможность |
|---|---|
| 🔐 Authorization | Регистрация и вход пользователя |
| 🧱 Maze Generation | Создание лабиринта по seed, размеру чанка, ширине, высоте и режиму финиша |
| 🎮 Unity Scene | Интерактивная игровая сцена с машинкой и лабиринтом |
| 🧩 Visual Blocks | Управление машинкой через визуальные командные блоки |
| 🤖 Robot Logic | Выполнение команд движения внутри лабиринта |
| 🌐 REST API | Создание попыток прохождения и отправка действий |
| 🗄 PostgreSQL | Хранение попыток, параметров лабиринта и действий машинки |
| ✅ Tests | Unit-тесты, API-тесты и интеграционный тест базы данных |

---

## 🏗 Architecture

```mermaid
flowchart LR
    User[👤 User] --> Auth[🔐 Login / Registration]
    Auth --> Unity[🎮 Unity Client]

    Unity --> MazeGen[🧱 Maze Generation]
    Unity --> Blocks[🧩 Visual Command Blocks]
    Blocks --> Controller[🤖 Car Controller]
    Controller --> Maze[🧭 Maze Scene]

    Unity -->|HTTP requests| API[🌐 ASP.NET Core REST API]
    API -->|SQL queries| DB[(🗄 PostgreSQL)]
    DB -->|Stored data| API
    API -->|JSON responses| Unity

    Tests[✅ Unity Test Runner] --> Unity
    Tests --> API
    Tests --> DB
```

### System Layers

| Layer | Technology | Responsibility |
|---|---|---|
| Client | Unity, C# | UI, лабиринт, визуальные блоки, движение машинки |
| Server | ASP.NET Core Web API | Обработка запросов и связь с базой данных |
| Database | PostgreSQL | Хранение попыток прохождения и действий |
| Testing | Unity Test Framework, NUnit | Проверка логики, API и сохранения данных |

---

## 🔄 Runtime Flow

```mermaid
sequenceDiagram
    participant U as User
    participant C as Unity Client
    participant A as REST API
    participant D as PostgreSQL

    U->>C: Register / Login
    U->>C: Configure maze parameters
    C->>C: Generate maze
    U->>C: Build command sequence
    C->>C: Run car movement
    C->>A: POST /attempts
    A->>D: Insert attempt
    D-->>A: attempt_id
    A-->>C: JSON response
    C->>A: POST /attempts/{id}/actions
    A->>D: Insert car actions
    D-->>A: Saved records
    A-->>C: Success response
```

---

## 🌐 REST API

REST API связывает Unity-клиент с базой данных PostgreSQL.

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | Проверка доступности API |
| `POST` | `/attempts` | Создание новой попытки прохождения лабиринта |
| `POST` | `/attempts/{id}/actions` | Сохранение действий машинки |
| `GET` | `/attempts/{id}/actions` | Получение сохранённых действий |

### Create Attempt Request

```json
{
  "maze_seed": 123,
  "maze_width": 3,
  "maze_height": 3,
  "create_finish_area": true,
  "create_finish_area_in_corner": false
}
```

### Create Attempt Response

```json
{
  "attempt_id": 1
}
```

### Save Actions Request

```json
{
  "records": [
    {
      "time_sec": 1.5,
      "action": "MOVE",
      "pos_x": 2,
      "pos_y": 3
    }
  ]
}
```

---

## 🗄 Database

База данных используется для хранения попыток прохождения лабиринта и действий машинки.

```mermaid
erDiagram
    ATTEMPTS ||--o{ ACTIONS : contains

    ATTEMPTS {
        int attempt_id PK
        int maze_seed
        int maze_width
        int maze_height
        boolean create_finish_area
        boolean create_finish_area_in_corner
        datetime created_at
    }

    ACTIONS {
        int action_id PK
        int attempt_id FK
        float time_sec
        string action
        int pos_x
        int pos_y
    }
```

| Table | Purpose |
|---|---|
| `attempts` | Параметры созданного лабиринта и попытки прохождения |
| `actions` | Последовательность действий машинки в рамках попытки |

---

## ✅ Testing

В проекте реализованы обычные unit-тесты и интеграционные проверки API/БД.

| Test | Type | What is checked |
|---|---|---|
| `Vector2Int_Creation_StoresCorrectCoordinates` | Unit | Корректное создание координаты клетки |
| `MazeCellCoordinates_AddDirection_ReturnsExpectedPosition` | Unit | Смещение координаты при движении вправо |
| `MazeSize_Calculation_ReturnsTotalCellCount` | Unit | Расчёт общего размера лабиринта |
| `Api_Health_ReturnsSuccessStatus` | API | Доступность серверной части |
| `CarApi_CreateAttempt_ReturnsAttemptId` | API | Создание попытки прохождения через API |
| `Database_SaveAndLoadCarActions_ReturnsSavedAction` | Integration | Сохранение и получение действий машинки через API и PostgreSQL |

<p align="center">
  <img src="docs/screenshots/test-runner.png" width="850" alt="Unity Test Runner Results">
</p>

---

## 🚀 Quick Start

### 1. Clone repository

```bash
git clone https://github.com/vpt-student-projects/learning-maze-robotics-unity.git
cd learning-maze-robotics-unity
```

### 2. Open Unity project

1. Open **Unity Hub**.
2. Click **Add project**.
3. Select the project folder.
4. Open the project with the required Unity version.

### 3. Start PostgreSQL

Make sure PostgreSQL is installed and the database exists.

Recommended database name:

```text
maze_db
```

### 4. Start API server

```bash
cd MazeAttemptsApi
dotnet run
```

Default API URL:

```text
http://localhost:5081
```

### 5. Run Unity scene

Open the main scene and press **Play**.

### 6. Run tests

```text
Window → General → Test Runner → EditMode → Run All
```

---

## 📁 Project Structure

<details>
<summary>Click to expand</summary>

```text
LearningMazeRobotics/
├── Assets/
│   ├── CoreScripts/
│   ├── Scenes/
│   ├── Tests/
│   └── UI/
├── MazeAttemptsApi/
│   ├── Controllers/
│   ├── Models/
│   └── Program.cs
├── docs/
│   ├── hero/
│   │   └── banner.svg
│   └── screenshots/
│       ├── login-window.png
│       ├── maze-generation.png
│       ├── main-scene.png
│       ├── visual-blocks.png
│       └── test-runner.png
├── README.md
└── .gitignore
```

</details>

---

## 🎯 Educational Value

Проект демонстрирует, как игровые технологии могут применяться для обучения робототехнике и алгоритмизации. Пользователь не просто запускает готовую сцену, а взаимодействует с системой: задаёт параметры лабиринта, формирует последовательность команд, запускает машинку и получает результат выполнения.

Такой подход помогает изучать:

- алгоритмическое мышление;
- последовательность команд;
- координатную логику;
- основы движения робота в среде;
- взаимодействие клиента, сервера и базы данных;
- тестирование программной системы.

---

## 📌 Notes for Reviewers

This repository is not only a Unity scene, but a complete educational software prototype that includes:

- a visual Unity client;
- user authorization interface;
- configurable maze generation;
- visual command-based robot control;
- backend REST API;
- persistent PostgreSQL storage;
- automated tests for logic, API and database interaction;
- clear documentation and architecture diagrams.

---

<div align="center">

### 🤖 Learning Maze Robotics

**Unity • C# • ASP.NET Core • PostgreSQL • Testing • Visual Programming**

</div>
