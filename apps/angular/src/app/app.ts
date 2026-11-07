import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TimeEntriesService } from './time-entries.service';
import { TimeReport } from './time-entry';
import { PieChart } from './pie-chart';

const fallbackError = 'The time entries are temporarily unavailable. Please try again.';

type ReportState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'loaded'; report: TimeReport };

// The backend returns RFC 9457 problem details whose `detail` is already safe to show.
function errorMessage(error: unknown): string {
  const detail = error instanceof HttpErrorResponse ? error.error?.detail : undefined;
  return typeof detail === 'string' && detail ? detail : fallbackError;
}

@Component({
  selector: 'app-root',
  imports: [DecimalPipe, PieChart],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App implements OnInit {
  private readonly service = inject(TimeEntriesService);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = signal<ReportState>({ status: 'loading' });

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    if (this.state().status === 'loading' && this.requestInProgress) return;
    this.requestInProgress = true;
    this.state.set({ status: 'loading' });
    this.service
      .getReport()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (report) => {
          this.requestInProgress = false;
          this.state.set({ status: 'loaded', report });
        },
        error: (error: unknown) => {
          this.requestInProgress = false;
          this.state.set({ status: 'error', message: errorMessage(error) });
        },
      });
  }

  private requestInProgress = false;
}
