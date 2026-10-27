# Fstonge.Aspire.Sdk.LiveKit

Configures the upstream `Livekit.Server.Sdk.Dotnet` SDK for a trusted .NET
server application:

```csharp
builder.Services.AddLiveKitSdk(builder.Configuration);
```

Set `LIVEKIT_URL`, `LIVEKIT_API_KEY`, and `LIVEKIT_API_SECRET` using
environment variables, user secrets, or the `LiveKit` configuration section.
The wrapper registers `RoomServiceClient` as transient and validates settings
without writing credential values to errors. See the repository's
[getting-started guide](../../docs/getting-started.md) for Aspire setup and
security guidance.
