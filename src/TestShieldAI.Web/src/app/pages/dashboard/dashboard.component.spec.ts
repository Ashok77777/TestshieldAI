import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import {
  generateResponse,
  runResponse,
  sampleCoverage,
  sampleFinding,
  sampleProject,
  sampleRisk
} from '../../testing/api-fixtures';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ projectId: sampleProject.id }) }
          }
        }
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function loadDashboard() {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    http.expectOne('/api/projects').flush([sampleProject]);
    fixture.detectChanges();
    return fixture;
  }

  it('loads the selected project dashboard', () => {
    const fixture = loadDashboard();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Orders API');
    expect(text).toContain('https://api.example.com');
  });

  it('displays a Safe decision from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ decision: 'Safe' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Safe');
  });

  it('displays a Review decision from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ decision: 'Review' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Review');
  });

  it('displays a Block decision from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ decision: 'Block' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Block');
  });

  it('displays the backend risk summary without recalculating it', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ risk: sampleRisk }));
    fixture.detectChanges();

    const risk = fixture.nativeElement.querySelector('[data-testid="risk"]')?.textContent ?? '';
    expect(risk).toContain('Medium');
    expect(risk).toContain('Review recommended');
    expect(risk).toContain('The current run requires human review before release.');
    expect(risk).toContain('A status mismatch was detected');
    expect(risk).toContain('Review the current test results.');
  });

  it('displays backend coverage values without recalculating them', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ coverage: sampleCoverage }));
    fixture.detectChanges();

    const coverage = fixture.nativeElement.querySelector('[data-testid="coverage"]')?.textContent ?? '';
    expect(coverage).toContain('8');
    expect(coverage).toContain('3');
    expect(coverage).toContain('5');
    expect(coverage).toContain('12.34%');
    expect(coverage).toContain('2');
    expect(coverage).not.toContain('37.5');
  });

  it('displays coverage gaps from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse());
    fixture.detectChanges();

    const coverage = fixture.nativeElement.querySelector('[data-testid="coverage"]')?.textContent ?? '';
    expect(coverage).toContain('Orders');
    expect(coverage).toContain('GET');
    expect(coverage).toContain('/orders/{id}');
    expect(coverage).toContain('No happy-path or negative-missing-required-body test');
  });

  it('displays findings from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ findings: [sampleFinding] }));
    fixture.detectChanges();

    const findings = fixture.nativeElement.querySelector('[data-testid="findings"]')?.textContent ?? '';
    expect(findings).toContain('Contract');
    expect(findings).toContain('High');
    expect(findings).toContain('Expected status 200 but received 500');
    expect(fixture.nativeElement.querySelector('tr.high')).toBeTruthy();
  });

  it('shows the no-findings empty state', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ findings: [] }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="findings-empty"]')?.textContent).toContain(
      'No regression or contract drift findings.'
    );
  });

  it('displays the AI summary from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse());
    fixture.detectChanges();

    const ai = fixture.nativeElement.querySelector('[data-testid="ai-summary"]')?.textContent ?? '';
    expect(ai).toContain('Generated');
    expect(ai).toContain('2');
    expect(ai).toContain('One AI scenario was dropped');
  });

  it('displays a neutral AI disabled message', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(
      runResponse({
        ai: { enabled: false, generatedCount: 0, acceptedCount: 0, warningCount: 0, warnings: [] }
      })
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ai-summary"]')?.textContent).toContain(
      'AI scenario generation is currently disabled.'
    );
  });

  it('calls generate on the correct endpoint and does not treat tests as executed', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.generateTests();
    const request = http.expectOne(`/api/projects/${sampleProject.id}/tests/generate`);
    expect(request.request.method).toBe('POST');
    request.flush(generateResponse());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('have not been executed');
    expect(text).toContain('HappyPath');
    expect(fixture.nativeElement.querySelector('[data-testid="decision"] .decision-value')).toBeNull();
  });

  it('calls run on the correct endpoint and shows execution results', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    const request = http.expectOne(`/api/projects/${sampleProject.id}/tests/run`);
    expect(request.request.method).toBe('POST');
    request.flush(runResponse());
    fixture.detectChanges();

    const results = fixture.nativeElement.querySelector('[data-testid="test-results"]')?.textContent ?? '';
    expect(results).toContain('Passed');
    expect(results).toContain('200');
  });

  it('disables generate and run actions while a request is in progress', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.generateTests();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="generate-tests"]').disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('[data-testid="run-tests"]').disabled).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Working');

    http.expectOne(`/api/projects/${sampleProject.id}/tests/generate`).flush(generateResponse());
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="generate-tests"]').disabled).toBeFalse();
  });

  it('shows a user-friendly API error', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(
      { title: 'Project not found', detail: "No project exists with id 'missing'." },
      { status: 404, statusText: 'Not Found' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="error"]')?.textContent).toContain(
      "No project exists with id 'missing'."
    );
  });
});
