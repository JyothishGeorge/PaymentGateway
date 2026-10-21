# Payment Gateway

An ASP.NET Core (.NET 8) API that lets merchants process card payments through an acquiring bank (simulated) and retrieve previously made payments.

## Running

```bash
docker-compose up -d                                   # bank simulator on http://localhost:8080
dotnet run --project src/PaymentGateway.Api            # Swagger UI at /swagger
dotnet test                                            # all tests (simulator must be running)
dotnet test --filter "Category!=Integration"           # unit + in-memory API tests only
```

The bank URL is configured via `AcquiringBank:BaseUrl` in `appsettings.json`.

## Authentication

All endpoints require the header `X-Api-Key: pg_test_123e4567e89b12d3a456426614174000`. The key and the merchant identity it represents are configured in `Authentication:ApiKey` and `Authentication:MerchantId` in `appsettings.json`, and the app fails to start if either is missing. Requests without a key, or with a wrong key, get `401 Unauthorized` before any validation runs or the bank is called. In Swagger UI, click **Authorize** and enter the key.

## API

### `POST /api/payments`

```json
{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }
```

| Outcome | HTTP status | Body |
|---|---|---|
| Authorized | `201 Created` (+ `Location` header) | `{ id, status: "Authorized", cardNumberLastFour, expiryMonth, expiryYear, currency, amount }` |
| Declined | `201 Created` (+ `Location` header) | as above with `status: "Declined"` |
| Rejected (invalid request, bank not called) | `400 Bad Request` | `{ status: "Rejected", errors: [ ... ] }` |
| Acquiring bank unavailable / error | `502 Bad Gateway` | `ProblemDetails` |

### `GET /api/payments/{id}`

`200 OK` with the same payment shape as above, or `404 Not Found`.

`GET /health` returns `200 Healthy` and needs no API key (for load balancers and container probes).

## Design

```
POST: ValidationFilter (FluentValidation) -> PaymentsController -> ProcessPaymentCommandHandler -> IAcquiringBankClient (typed HttpClient)
                                                                                                 -> IPaymentsRepository (in-memory)
GET:  PaymentsController -> GetPaymentQueryHandler -> IPaymentsRepository
```

- **ApiKeyAuthenticationHandler** is a custom `AuthenticationHandler<ApiKeyOptions>`. It accepts one configured test key from the `X-Api-Key` header, to demonstrate, can be repalced by authetication like JWT token verififcation 
- **Controller** is thin: HTTP mapping only (201/400/401/404/502).
- **ValidationFilter** is a global MVC action filter. It resolves `IValidator<T>` for each action argument and, if validation fails, short-circuits with `400` `Rejected` before the controller runs, so the bank is never called.
- **CQRS (plain handlers, no mediator library to avoid over engineering)**: writes and reads are separate. `ProcessPaymentCommand` / `ProcessPaymentCommandHandler` (in `Commands/`) writes. `GetPaymentQuery` / `GetPaymentQueryHandler` (in `Queries/`) only reads.
- **PaymentRequestValidator** is a FluentValidation `AbstractValidator<PostPaymentRequest>` (registered as `IValidator<PostPaymentRequest>`). It returns all errors at once, so merchants can fix everything in one go. 
- **AcquiringBankClient** is the only component that knows the bank's contract (snake_case JSON, `MM/yyyy` expiry). Any non-2xx response, network failure or timeout is logged and returned as a failed `Result<BankPaymentResponse>`.  
- **Bank resilience** uses `Microsoft.Extensions.Http.Resilience`'s standard handler, demostrated retries (can be extended for Circuit Breaker,Rate Limiting, Timeouts etc)  **only when the connection to the bank could not be established**, because the request then never reached the bank. 5xx responses, timeouts and dropped responses are not retried, because the bank may already have charged the card. Failures surface as `502 Bad Gateway`.
- **Result pattern (small, hand-written `Result<T>`)**: the bank being unavailable is an expected outcome (e.g. the simulator returns 503 for cards ending in 0), so `AcquiringBankClient` returns `Result<BankPaymentResponse>` instead of throwing. `ProcessPaymentCommandHandler` passes a failure on as `Result<Payment>`, and the controller maps it to `502`. There is no try/catch outside the client. Exceptions remain for real faults; no result library was added.
- **Observability** uses OpenTelemetry: logs and traces are written with the built-in console exporter. A production setup would swap this for an OTLP exporter to a collector.
- **PaymentsRepository** is the provided in-memory test double, made thread-safe (`ConcurrentDictionary`)
- The stored `Payment` domain model never contains the full card number or CVV. Only the last four digits are kept, so they cannot leak through storage or the GET endpoint.
- Request fields are nullable so that "missing" and "invalid" produce distinct error messages. Malformed JSON or wrong types (e.g. `"amount": 10.5`) are also returned as a `Rejected` response, via `InvalidModelStateResponseFactory`, so merchants see one consistent shape.
- Enums are serialised as strings (`"Authorized"`) rather than numbers.


## Assumptions
- **No DDD (Domain Driven Design) or Event Driven Architecture considered. Also BDD (Gherken) or TestContainer** based integration tests to avoid over-engineering for this simple solution.
- No **docker** deployment configurations
- **Supported currencies** are `GBP`, `USD` and `EUR` (exact, upper-case).
- **Amount** must be a positive integer (> 0) in minor units and fits in an `int`.
- **Card number** is a string, ASCII digits (no spaces or dashes).
- **CVV** is a string so leading zeros (e.g. `012`) are preserved. It must be 3-4 ASCII digits.

## Tests

### Unit Tests
- `ValidationFilterTests`: the filter short-circuits invalid requests and lets valid ones through.
- `PaymentRequestValidatorTests`: every validation rule and its boundaries, using a Moq-mocked `TimeProvider` as a fixed clock.
- `PaymentHandlersTests`: command and query handlers with a Moq-mocked bank and a Moq-mocked `IPaymentsRepository`.
- `AcquiringBankResilienceTests`: the real HttpClient pipeline retries connection failures but not 5xx or failures after the request was sent.
- `AcquiringBankClientTests`: request mapping to the bank contract and response/error handling, using a stub `HttpMessageHandler`.
- `PaymentsControllerTests`: in-memory API tests (`WebApplicationFactory`) with a Moq-mocked bank, covering Authorized/Declined/Rejected/bank failure/retrieve/404/401 and the anonymous `/health` endpoint.
- `TestDoubles.cs`: shared test data (one valid payment in its request, command and bank-request forms), `BankMock` (small Moq helpers: `RespondsWith`, `VerifyCalledOnceWith`, `VerifyNotCalled`) and a stub `HttpMessageHandler`, so each test only states what is different. The HTTP handler is a hand-written stub rather than a Moq mock because its `SendAsync` is protected, and mocking it with Moq is harder to read.
### Integration Tests
- `BankSimulatorIntegrationTests` (`Category=Integration`): end-to-end tests that run the real API against the real bank simulator (no fakes).

## Test Data

Major cases from the assessment brief. Send each body to `POST /api/payments` with the `X-Api-Key` header (e.g. via Swagger UI) while the simulator is running. Use an expiry date in the future (e.g. `"expiryMonth": 4, "expiryYear": 2027`).

| Case | Request body | Expected |
|---|---|---|
| Authorized (card ends in odd digit) | `{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }` | `201`, `"status": "Authorized"`, `"cardNumberLastFour": "8877"` |
| Declined (card ends in even digit) | `{ "cardNumber": "2222405343248878", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }` | `201`, `"status": "Declined"` |
| Bank unavailable (card ends in 0) | `{ "cardNumber": "2222405343248870", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }` | `502 Bad Gateway`, `ProblemDetails` || Rejected: missing required fields | `{}` | `400`, `"status": "Rejected"` |
| Rejected: missing required fields | `{}` | `400`, `"status": "Rejected"` |
| Rejected: card number not numeric | `{ "cardNumber": "2222-4053-4324", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }` | `400`, `"status": "Rejected"` |
| Rejected: expiry month not 1-12 | `{ "cardNumber": "2222405343248877", "expiryMonth": 13, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }` | `400`, `"status": "Rejected"` |
| Rejected: expiry in the past | `{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2025, "currency": "GBP", "amount": 1050, "cvv": "123" }` | `400`, `"status": "Rejected"` |
| Rejected: unsupported currency | `{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027, "currency": "JPY", "amount": 1050, "cvv": "123" }` | `400`, `"status": "Rejected"` |
| Rejected: amount not an integer | `{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 10.50, "cvv": "123" }` | `400`, `"status": "Rejected"` |
| Rejected: CVV not 3-4 digits | `{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "12a" }` | `400`, `"status": "Rejected"` |
 
