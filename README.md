# TestShield AI

ASP.NET Core 8 API that ingests OpenAPI specs, generates deterministic contract tests, executes them, validates responses, and detects regressions. AI scenario generation is optional enrichment and never decides pass/fail.

The Angular dashboard in `src/TestShieldAI.Web` displays those API results. It does not recalculate decisions, risk, or coverage. See `src/TestShieldAI.Web/README.md` to run it.

## Local AI configuration

AI is **disabled by default**. Leave `Ai:Enabled` as `false` and `Ai:Provider` as `None` unless you are exercising Gate 2 adapters.

Do **not** put API keys in `appsettings.json`, source control, or logs.

### appsettings.json (no secrets)

```json
"Ai": {
  "Enabled": true,
  "Provider": "AzureOpenAI",
  "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
  "ModelOrDeployment": "YOUR_DEPLOYMENT_OR_MODEL",
  "MaxScenariosPerOperation": 6,
  "TimeoutSeconds": 30
}
```

`Provider` may be `None`, `AzureOpenAI`, or `OpenAI`. For the public OpenAI service, `Endpoint` may be left empty; `ModelOrDeployment` is the model name.

### API key

Set the key locally via environment variables:

```text
AI_PROVIDER_API_KEY=<set locally>
```

Provider-specific alternatives:

```text
AZURE_OPENAI_API_KEY=<set locally>
OPENAI_API_KEY=<set locally>
```

Or ASP.NET Core user secrets from `src/TestShieldAI.Api`:

```bash
dotnet user-secrets set "Ai:ApiKey" "<set locally>"
```

`AI_PROVIDER_API_KEY` wins over provider-specific variables, which win over `Ai:ApiKey` from user secrets.

When AI is disabled, the provider is `None`, or configuration is unknown, TestShield AI uses a no-op completion client and the deterministic Gate 1 flow continues.
