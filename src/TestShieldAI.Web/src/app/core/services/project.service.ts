import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateProjectRequest, Project } from '../models/testshield.models';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<Project[]> {
    return this.http.get<Project[]>('/api/projects');
  }

  create(request: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>('/api/projects', request);
  }
}
