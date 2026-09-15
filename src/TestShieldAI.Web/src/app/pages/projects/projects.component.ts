import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { apiErrorMessage } from '../../core/api-error';
import { Project } from '../../core/models/testshield.models';
import { ProjectService } from '../../core/services/project.service';
import { SelectedProjectService } from '../../core/services/selected-project.service';

@Component({
  selector: 'app-projects',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.css'
})
export class ProjectsComponent implements OnInit {
  private readonly projectsApi = inject(ProjectService);
  private readonly selectedProject = inject(SelectedProjectService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  projects: Project[] = [];
  loading = false;
  creating = false;
  error = '';
  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    baseUrl: ['', Validators.required]
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.error = '';
    this.projectsApi.list().subscribe({
      next: projects => {
        this.loading = false;
        if (!Array.isArray(projects)) {
          this.projects = [];
          this.error = 'The TestShield API returned an empty or invalid response.';
          return;
        }
        this.projects = projects;
      },
      error: err => {
        this.loading = false;
        this.error = apiErrorMessage(err);
      }
    });
  }

  select(project: Project): void {
    this.selectedProject.select(project);
    void this.router.navigate(['/projects', project.id]);
  }

  create(): void {
    if (this.form.invalid || this.creating) {
      this.form.markAllAsTouched();
      return;
    }

    this.creating = true;
    this.error = '';
    this.projectsApi.create(this.form.getRawValue()).subscribe({
      next: project => {
        this.creating = false;
        this.form.reset();
        this.select(project);
      },
      error: err => {
        this.creating = false;
        this.error = apiErrorMessage(err);
      }
    });
  }
}
