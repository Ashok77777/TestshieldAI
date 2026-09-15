import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SelectedProjectService } from './core/services/selected-project.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  constructor(readonly selectedProject: SelectedProjectService) {}

  dashboardLink(): string[] {
    const project = this.selectedProject.project();
    return project ? ['/projects', project.id] : ['/projects'];
  }
}
