# Indigosoft.TestTask.ExchangeSimulator

WebSocket simulator for three exchange feeds with intentionally different JSON wire formats.

## Run

From the solution directory:

```powershell
dotnet run --project .\Indigosoft.TestTask.ExchangeSimulator\Indigosoft.TestTask.ExchangeSimulator.csproj
```

By default ASP.NET Core prints the listening URL in the console, usually `http://localhost:5000` or a generated launch profile port.

## WebSocket Endpoints

- `ws://localhost:5000/ws/exchange-a`
- `ws://localhost:5000/ws/exchange-b`
- `ws://localhost:5000/ws/exchange-c`

Tickers: `BTCUSDT`, `ETHUSDT`, `SOLUSDT`, `XRPUSDT`, `DOGEUSDT`.

The message rate is configured in `appsettings.json`:

```json
"ExchangeSimulator": {
  "Exchanges": {
    "ExchangeA": { "MessagesPerSecond": 150, "SendIntervalMs": 100 },
    "ExchangeB": { "MessagesPerSecond": 150, "SendIntervalMs": 100 },
    "ExchangeC": { "MessagesPerSecond": 150, "SendIntervalMs": 100 }
  },
  "Faults": {
    "DuplicateProbability": 0.05
  }
}
```

Messages are sent in batches once per `SendIntervalMs`. Fractional messages per timer tick are accumulated, so the configured `MessagesPerSecond` is accurate on average.

## Message Formats

Exchange A:

```json
{
  "ticker": "BTCUSDT",
  "price": 65000.12,
  "volume": 0.42,
  "timestamp": "2026-06-30T12:00:00.000Z",
  "sequence": 123
}
```

Exchange B:

```json
{
  "s": "ETH-USDT",
  "p": "3500.55",
  "q": "2.15",
  "ts": 1782812345678,
  "seq": 456
}
```

Exchange C:

```json
{
  "instrument": "SOL/USDT",
  "last": {
    "amount": 123.45,
    "currency": "USDT"
  },
  "size": 10,
  "time": {
    "unixSeconds": 1782812345,
    "nanoseconds": 123000000
  },
  "eventId": "exchange-c-789"
}
```

## Fault Endpoints

Exchange names in routes can be `exchange-a`, `exchange-b`, `exchange-c` or `ExchangeA`, `ExchangeB`, `ExchangeC`.

- `GET /faults` - returns current fault settings.
- `POST /faults/{exchangeName}/disconnect-once` - closes the next active WebSocket connection for the exchange.
- `POST /faults/{exchangeName}/duplicates/enable` - enables periodic duplicate messages.
- `POST /faults/{exchangeName}/duplicates/disable` - disables duplicate messages.
- `POST /faults/{exchangeName}/pause?durationMs=5000` - pauses message sending without closing the WebSocket.

## Quick Checks

Using `wscat`:

```powershell
npx wscat -c ws://localhost:5000/ws/exchange-a
```

Using `websocat`:

```powershell
websocat ws://localhost:5000/ws/exchange-b
```

Fault examples:

```powershell
curl http://localhost:5000/faults
curl -X POST http://localhost:5000/faults/exchange-a/duplicates/enable
curl -X POST "http://localhost:5000/faults/exchange-b/pause?durationMs=5000"
curl -X POST http://localhost:5000/faults/exchange-c/disconnect-once
```
