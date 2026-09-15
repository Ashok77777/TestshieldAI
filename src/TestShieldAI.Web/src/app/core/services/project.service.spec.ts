import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { sampleProject } from '../../testing/api-fixtures';
import { ProjectService } from './project.service';

describe('ProjectService', () => {
  let service: ProjectService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ProjectService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists projects', () => {
    let result: unknown;
    service.list().subscribe(value => (result = value));

    const request = http.expectOne('/api/projects');
    expect(request.request.method).toBe('GET');
    request.flush([sampleProject]);

    expect(result).toEqual([sampleProject]);
  });

  it('creates a project', () => {
    let result: unknown;
    service.create({ name: 'Orders API', baseUrl: 'https://api.example.com' }).subscribe(value => (result = value));

    const request = http.expectOne('/api/projects');
    expect(request.request.method).toBe('POST');
    request.flush(sampleProject);

    expect(result).toEqual(sampleProject);
  });
});
