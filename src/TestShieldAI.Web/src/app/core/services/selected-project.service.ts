import { Injectable, signal } from '@angular/core';
import { Project } from '../models/testshield.models';

@Injectable({ providedIn: 'root' })
export class SelectedProjectService {
  readonly project = signal<Project | null>(null);

  select(project: Project | null): void {
    this.project.set(project);
  }
}
