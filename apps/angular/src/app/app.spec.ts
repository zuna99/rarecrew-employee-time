import { HttpErrorResponse } from '@angular/common/http';
import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { App } from './app';
import { PieChart } from './pie-chart';
import { EmployeeSummary, TimeReport } from './time-entry';
import { TimeEntriesService } from './time-entries.service';
import { calculateReport } from './calculate-report';

@Component({ selector: 'app-pie-chart', template: '' })
class StubPieChart {
  readonly employees = input.required<readonly EmployeeSummary[]>();
}

describe('App report states', () => {
  let response: Subject<TimeReport>;

  beforeEach(() => {
    response = new Subject<TimeReport>();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [{ provide: TimeEntriesService, useValue: { getReport: () => response } }],
    }).overrideComponent(App, {
      remove: { imports: [PieChart] },
      add: { imports: [StubPieChart] },
    });
  });

  it('shows loading followed by an empty state', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Gathering the hours');
    response.next(calculateReport([]));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No working time to display');
  });

  it('offers a retry after a request fails', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    response.error(new Error('test failure'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('We couldn’t load the report');
    expect(fixture.nativeElement.textContent).toContain('Try again');
    response = new Subject<TimeReport>();
    const button = [...fixture.nativeElement.querySelectorAll('button')].find((element: unknown) =>
      (element as HTMLButtonElement).textContent?.includes('Try again'),
    ) as HTMLButtonElement;
    button.click();
    fixture.detectChanges();
    response.next(calculateReport([]));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No working time to display');
  });

  it('shows the backend problem detail, and a generic message without one', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    response.error(
      new HttpErrorResponse({
        status: 503,
        error: { detail: 'The API key is not configured. Follow the local setup steps.' },
      }),
    );
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('The API key is not configured');

    response = new Subject<TimeReport>();
    fixture.componentInstance.loadReport();
    response.error(new Error('network down'));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('temporarily unavailable');
  });

  it('marks the under-100 row and renders names as text', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    response.next(
      calculateReport([
        {
          Id: '1',
          EmployeeName: '<script>alert(1)</script>',
          StarTimeUtc: '2026-01-01T00:00:00',
          EndTimeUtc: '2026-01-01T01:00:00',
          DeletedOn: null,
        },
      ]),
    );
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tr.below-threshold')).toHaveLength(1);
    expect(fixture.nativeElement.querySelector('tbody script')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('<script>alert(1)</script>');
  });

  it('shows zero hours without mounting a chart', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    response.next(
      calculateReport([
        {
          Id: '1',
          EmployeeName: 'Alex',
          StarTimeUtc: '2026-01-01T00:00:00',
          EndTimeUtc: '2026-01-01T00:00:00',
          DeletedOn: null,
        },
      ]),
    );
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No positive hours yet');
    expect(fixture.nativeElement.querySelector('app-pie-chart')).toBeNull();
  });
});
