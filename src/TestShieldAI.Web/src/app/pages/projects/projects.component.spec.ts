import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { sampleProject } from '../../testing/api-fixtures';
import { SelectedProjectService } from '../../core/services/selected-project.service';
import { ProjectsComponent } from './projects.component';

describe('ProjectsComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function create() {
    const fixture = TestBed.createComponent(ProjectsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('loads the project list', () => {
    const fixture = create();
    http.expectOne('/api/projects').flush([sampleProject]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Orders API');
    expect(text).toContain('https://api.example.com');
  });

  it('shows an empty state when there are no projects', () => {
    const fixture = create();
    http.expectOne('/api/projects').flush([]);
    fixture.detectChanges();

    const empty = fixture.nativeElement.querySelector('[data-testid="empty-projects"]');
    expect(empty?.textContent).toContain('No projects yet');
  });

  it('selects a project and stores the project id', async () => {
    const fixture = create();
    http.expectOne('/api/projects').flush([sampleProject]);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    fixture.nativeElement.querySelector('.project-list button').click();
    fixture.detectChanges();

    expect(TestBed.inject(SelectedProjectService).project()?.id).toBe(sampleProject.id);
    expect(navigate).toHaveBeenCalledWith(['/projects', sampleProject.id]);
  });

  it('shows a user-friendly API error', () => {
    const fixture = create();
    http.expectOne('/api/projects').flush(
      { title: 'Unavailable' },
      { status: 500, statusText: 'Server Error' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="error"]')?.textContent).toContain(
      'encountered an error'
    );
  });
});
