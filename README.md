# Indigosoft TestTask: Real-time Market Data Aggregator

Проект имитирует систему сбора, обработки и хранения биржевых котировок в реальном времени.

В решении есть несколько WebSocket-источников, которые имитируют биржи. Агрегатор подключается к ним параллельно, получает сообщения в разных форматах, приводит их к единой модели, дедуплицирует и сохраняет raw tick data в PostgreSQL пачками.

## Состав решения

### Indigosoft.TestTask.ExchangeSimulator

WebSocket-сервер, который имитирует три биржи:

- `/ws/exchange-a`
- `/ws/exchange-b`
- `/ws/exchange-c`

У каждой биржи свой формат сообщения. Это сделано намеренно, чтобы агрегатор не зависел от одного конкретного формата данных.

Также в симуляторе есть fault endpoints:

```powershell
curl.exe http://localhost:5100/faults
curl.exe -X POST http://localhost:5100/faults/exchange-a/disconnect-once
curl.exe -X POST http://localhost:5100/faults/exchange-a/duplicates/enable
curl.exe -X POST http://localhost:5100/faults/exchange-a/duplicates/disable
curl.exe -X POST "http://localhost:5100/faults/exchange-b/pause?durationMs=15000"
```

### Indigosoft.TestTask.Aggregator

ASP.NET Core сервис, который:

- подключается к WebSocket-источникам;
- парсит разные форматы сообщений;
- нормализует тики в единую модель;
- отбрасывает дубликаты;
- кладет тики в bounded channel;
- пишет данные в PostgreSQL батчами;
- отдает runtime metrics в JSON.

Основные HTTP endpoints:

```powershell
curl.exe http://localhost:5200/health
curl.exe http://localhost:5200/metrics
```

### Indigosoft.TestTask.Core

Общие модели, интерфейсы и options для агрегатора.

### Indigosoft.TestTask.Database

Работа с PostgreSQL: инициализация базы, создание таблицы `raw_ticks`, индексы и batch writer.

### Indigosoft.TestTask.Aggregator.Tests

Unit-тесты для парсеров, дедупликации, channel, workers, database initialization и metrics.

## Запуск через Docker Compose

Нужен Docker Desktop.

Запуск всего стенда:

```powershell
docker compose up --build
```

Состав стенда:

- `postgres`: PostgreSQL 16, снаружи доступен на `localhost:55433`;
- `exchange-simulator`: доступен на `http://localhost:5100`;
- `aggregator`: доступен на `http://localhost:5200`.

В `docker-compose.yml` намеренно не указан `POSTGRES_DB=indigosoft_ticks`. База создается самим Aggregator при старте. Это часть логики приложения: сервис подключается к maintenance database, проверяет наличие целевой базы, создает ее при необходимости, затем создает таблицу `raw_ticks` и индексы.

Если нужно стартовать с чистой базой:

```powershell
docker compose down -v
docker compose up --build
```

Проверка после запуска:

```powershell
curl.exe http://localhost:5100/faults
curl.exe http://localhost:5200/health
curl.exe http://localhost:5200/metrics
```

Проверка данных в PostgreSQL:

```powershell
docker exec indigosoft-postgres psql -U postgres -d indigosoft_ticks -c "SELECT source, COUNT(*) FROM raw_ticks GROUP BY source ORDER BY source;"
```

Остановка:

```powershell
docker compose down
```

## Локальный запуск без Docker

Нужен .NET 8 SDK. Для агрегатора также нужен PostgreSQL.

По умолчанию Aggregator использует строку подключения из `Indigosoft.TestTask.Aggregator/appsettings.json`:

```json
"Postgres": {
  "ConnectionString": "Host=localhost;Port=5433;Database=indigosoft_ticks;Username=postgres;Password=postgres",
  "MaintenanceDatabase": "postgres"
}
```

Если PostgreSQL работает на стандартном порту, нужно заменить `Port=5433` на `Port=5432`.

Пользователь PostgreSQL должен иметь право создавать базу данных. Aggregator сам создает базу `indigosoft_ticks`, если ее еще нет. После этого он создает таблицу `raw_ticks` и индексы.

Сборка и тесты:

```powershell
dotnet build
dotnet test
```

Запуск симулятора:

```powershell
dotnet run --project .\Indigosoft.TestTask.ExchangeSimulator\Indigosoft.TestTask.ExchangeSimulator.csproj
```

Запуск агрегатора в другом терминале:

```powershell
dotnet run --project .\Indigosoft.TestTask.Aggregator\Indigosoft.TestTask.Aggregator.csproj
```

## Что сохраняется в БД

Агрегатор пишет в таблицу `raw_ticks`:

- источник (`source`);
- тикер (`ticker`);
- цену и объем;
- timestamp тика;
- sequence, если он есть;
- исходный JSON сообщения (`raw_json`);
- время записи (`created_at`).

Исходный JSON сохраняется специально: это позволяет позже проверить парсинг, отладить ошибки и при необходимости переобработать данные.

## Fault injection

Симулятор позволяет вручную проверить сбои источников.

Посмотреть текущее состояние fault-настроек:

```powershell
curl.exe http://localhost:5100/faults
```

Одноразовый разрыв соединения:

```powershell
curl.exe -X POST http://localhost:5100/faults/exchange-a/disconnect-once
```

Включить дубликаты:

```powershell
curl.exe -X POST http://localhost:5100/faults/exchange-a/duplicates/enable
```

Выключить дубликаты:

```powershell
curl.exe -X POST http://localhost:5100/faults/exchange-a/duplicates/disable
```

Пауза источника для проверки idle timeout:

```powershell
curl.exe -X POST "http://localhost:5100/faults/exchange-b/pause?durationMs=15000"
```

Ожидаемое поведение:

- агрегатор переподключается к источнику;
- остальные источники продолжают работать;
- дубликаты не записываются повторно;
- зависшее соединение закрывается по idle timeout и создается заново.

## Metrics

Endpoint:

```powershell
curl.exe http://localhost:5200/metrics
```

Пример ответа:

```json
{
  "receivedMessages": 41670,
  "parsedMessages": 41670,
  "parseFailures": 0,
  "duplicateTicks": 11,
  "enqueuedTicks": 41659,
  "writtenTicks": 41254,
  "droppedTicks": 0,
  "failedBatches": 0,
  "channelCount": 0,
  "channelCapacity": 100000,
  "channelFillRatio": 0
}
```

`receivedMessages`, `parsedMessages`, `parseFailures`, `duplicateTicks`, `enqueuedTicks`, `writtenTicks`, `droppedTicks` и `failedBatches` являются накопительными счетчиками с момента старта процесса.

`channelCount` и `channelFillRatio` показывают текущее состояние канала.

`enqueuedTicks` не уменьшается после записи в БД. Это счетчик общего количества тиков, которые были успешно положены в канал.

Разница между `enqueuedTicks` и `writtenTicks` может быть нормальной. Часть тиков может уже быть прочитана из канала, но еще находиться во внутреннем batch writer до следующей записи в PostgreSQL.

## Основные решения

### Подключения к источникам

Для каждого WebSocket-источника запускается отдельный worker. Если один источник падает или зависает, остальные продолжают работать.

При обрыве соединения используется reconnect с exponential backoff. Начальная и максимальная задержка задаются в конфигурации.

Если источник перестает присылать данные, соединение считается зависшим. После idle timeout агрегатор закрывает соединение и переподключается.

### Нормализация

Для каждой биржи есть отдельный parser. Все форматы приводятся к единой модели `NormalizedTick`.

Новая биржа добавляется через реализацию `IExchangeMessageParser`.

### Дедупликация

Дедупликация сделана в памяти через потокобезопасный словарь.

Ключ дедупликации:

- `source`;
- `ticker`;
- `price`;
- `volume`;
- `timestamp`.

`sequence` намеренно не входит в ключ. Это позволяет считать дублем одно и то же рыночное событие даже при отличающемся техническом идентификаторе сообщения.

Окно дедупликации задается в конфигурации. По умолчанию используется 60 секунд.

### Backpressure

Между WebSocket workers и writer используется bounded channel.

Если writer не успевает писать в БД, producers ждут свободное место в канале. Данные не теряются молча.

### Запись в PostgreSQL

Данные пишутся пачками через multi-values INSERT одной командой.

Raw JSON сохраняется в `jsonb`.

При ошибках записи выполняется retry с backoff. Если retry исчерпаны, batch считается dropped, ошибка логируется, а счетчики обновляются.

### Graceful shutdown

При остановке приложения producers завершают работу и закрывают `TickChannel`.

Writer пытается дочитать уже принятые тики и записать оставшийся batch в БД. Drain ограничен timeout, чтобы приложение не зависало бесконечно при проблемах с БД.

## Тесты

Покрыты основные части:

- парсеры сообщений бирж;
- дедупликация;
- concurrent deduplication;
- bounded channel и backpressure;
- WebSocket stream;
- reconnect после обрыва;
- изоляция источников;
- инициализация PostgreSQL;
- batch writer;
- retry и drop batch;
- graceful drain;
- metrics counters.

Запуск тестов:

```powershell
dotnet test
```

## Ограничения

- Метрики хранятся в памяти и сбрасываются после рестарта.
- Дедупликация хранится в памяти и тоже сбрасывается после рестарта.
- Для записи используется batch INSERT, а не PostgreSQL Binary COPY.
- Integration tests с настоящим PostgreSQL не добавлялись, чтобы не усложнять запуск.
- В проекте нет отдельной системы мониторинга вроде Prometheus или OpenTelemetry.
