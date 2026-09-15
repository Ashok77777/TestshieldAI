import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { apiErrorMessage } from '../../core/api-error';
import {
  AiRunSummary,
  ApiTestExecutionResult,
  Coverage,
  GenerateTestsResponse,
  Project,
  RegressionFinding,
  RiskSummary,
  RunTestsResponse
} from '../../core/models/testshield.models';
import { ProjectService } from '../../core/services/project.service';
import { SelectedProjectService } from '../../core/services/selected-project.service';
import { TestShieldApiService } from '../../core/services/testshield-api.service';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  project: Project | null = null;
  specification = '';
  importMessage = '';
  error = '';
  busy: 'generate' | 'run' | 'import' | null = null;
  lastAction: 'generate' | 'run' | null = null;
  expandedResult: number | null = null;

  decision: string | null = null;
  risk: RiskSummary | null = null;
  coverage: Coverage | null = null;
  findings: RegressionFinding[] = [];
  results: ApiTestExecutionResult[] = [];
  generatedTests: GenerateTestsResponse['tests'] = [];
  ai: AiRunSummary | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly projectsApi: ProjectService,
    private readonly api: TestShieldApiService,
    private readonly selectedProject: SelectedProjectService
  ) {}

  ngOnInit(): void {
    const projectId = this.route.snapshot.paramMap.get('projectId');
    if (!projectId) {
      this.error = 'A project must be selected.';
      return;
    }

    this.projectsApi.list().subscribe({
      next: projects => {
        if (!Array.isArray(projects)) {
          this.error = 'The TestShield API returned an empty or invalid response.';
          return;
        }

        const project = projects.find(item => item.id === projectId) ?? null;
        this.project = project;
        this.selectedProject.select(project);
        if (!project) {
          this.error = 'The requested project was not found.';
        }
      },
      error: err => {
        this.error = apiErrorMessage(err);
      }
    });
  }

  get loading(): boolean {
    return this.busy !== null;
  }

  importSpec(): void {
    if (!this.project || this.loading) {
      return;
    }

    this.busy = 'import';
    this.error = '';
    this.importMessage = '';
    this.api.importSpec(this.project.id, this.specification).subscribe({
      next: response => {
        this.busy = null;
        if (!response?.operations) {
          this.error = 'The TestShield API returned an empty or invalid response.';
          return;
        }
        this.importMessage = `Imported ${response.operations.length} operation(s).`;
      },
      error: err => {
        this.busy = null;
        this.error = apiErrorMessage(err);
      }
    });
  }

  generateTests(): void {
    if (!this.project || this.loading) {
      return;
    }

    this.busy = 'generate';
    this.error = '';
    this.api.generateTests(this.project.id).subscribe({
      next: response => {
        this.busy = null;
        if (!response?.tests || !response.coverage || !response.risk) {
          this.error = 'The TestShield API returned an empty or invalid response.';
          return;
        }
        this.lastAction = 'generate';
        this.generatedTests = response.tests;
        this.coverage = response.coverage;
        this.risk = response.risk;
        this.decision = null;
        this.findings = [];
        this.results = [];
        this.ai = null;
        this.expandedResult = null;
      },
      error: err => {
        this.busy = null;
        this.error = apiErrorMessage(err);
      }
    });
  }

  runTests(): void {
    if (!this.project || this.loading) {
      return;
    }

    this.busy = 'run';
    this.error = '';
    this.api.runTests(this.project.id).subscribe({
      next: response => {
        if (!response?.decision || !response.coverage || !response.risk || !response.results || !response.findings || !response.ai) {
          this.busy = null;
          this.error = 'The TestShield API returned an empty or invalid response.';
          return;
        }
        this.applyRun(response);
      },
      error: err => {
        this.busy = null;
        this.error = apiErrorMessage(err);
      }
    });
  }

  toggleResult(index: number): void {
    this.expandedResult = this.expandedResult === index ? null : index;
  }

  decisionClass(decision: string | null): string {
    const value = (decision ?? '').toLowerCase();
    if (value === 'safe') {
      return 'decision-safe';
    }
    if (value === 'review') {
      return 'decision-review';
    }
    if (value === 'block') {
      return 'decision-block';
    }
    return 'decision-none';
  }

  private applyRun(response: RunTestsResponse): void {
    this.busy = null;
    this.lastAction = 'run';
    this.decision = response.decision;
    this.risk = response.risk;
    this.coverage = response.coverage;
    this.findings = response.findings;
    this.results = response.results;
    this.ai = response.ai;
    this.generatedTests = [];
    this.expandedResult = null;
  }
}
