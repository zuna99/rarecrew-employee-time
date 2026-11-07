import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { TimeEntriesService } from './time-entries.service';

describe('TimeEntriesService', () => {
  let service: TimeEntriesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TimeEntriesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the local proxy and calculates the report in Angular', () => {
    let total: number | undefined;
    service.getReport().subscribe((report) => (total = report.totalHours));
    const request = http.expectOne('/api/angular/time-entries');
    expect(request.request.method).toBe('GET');
    expect(request.request.url).not.toContain('code=');
    request.flush([
      {
        Id: '1',
        EmployeeName: 'Alex',
        StarTimeUtc: '2026-01-01T00:00:00',
        EndTimeUtc: '2026-01-01T02:30:00',
        DeletedOn: null,
      },
    ]);
    expect(total).toBe(2.5);
  });

  it('propagates a failed request for the component to handle', () => {
    let failed = false;
    service.getReport().subscribe({ error: () => (failed = true) });
    http
      .expectOne('/api/angular/time-entries')
      .flush({}, { status: 502, statusText: 'Bad Gateway' });
    expect(failed).toBe(true);
  });
});
