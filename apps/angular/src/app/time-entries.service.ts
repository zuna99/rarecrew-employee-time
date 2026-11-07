import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, timeout } from 'rxjs';
import { calculateReport } from './calculate-report';
import { TimeEntry, TimeReport } from './time-entry';

@Injectable({ providedIn: 'root' })
export class TimeEntriesService {
  private readonly http = inject(HttpClient);

  getReport(): Observable<TimeReport> {
    return this.http
      .get<(TimeEntry | null)[]>('/api/angular/time-entries')
      .pipe(timeout(25_000), map(calculateReport));
  }
}
