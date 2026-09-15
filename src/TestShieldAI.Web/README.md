# TestShield AI Dashboard

Angular 19 dashboard for TestShield AI. It consumes the existing ASP.NET Core API and displays project, decision, risk, coverage, findings, test results, and AI summary values returned by the backend.

The dashboard does not recalculate Safe/Review/Block, risk, or coverage.

## Run locally

1. Start the API (http profile, port 5126):

   ```bash
   dotnet run --project src/TestShieldAI.Api --launch-profile http
   ```

2. Start the dashboard (proxies `/api` to `http://localhost:5126`):

   ```bash
   cd src/TestShieldAI.Web
   npm start
   ```

Open `http://localhost:4200/`.

## Build and test

```bash
cd src/TestShieldAI.Web
npm run build
npx ng test --watch=false --browsers=ChromeHeadless
```

Unit tests mock `HttpClient` and do not call a live API or AI provider.
