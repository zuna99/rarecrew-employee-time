import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  input,
  OnDestroy,
  viewChild,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Chart, PieController, ArcElement, Tooltip } from 'chart.js';
import { EmployeeSummary } from './time-entry';

Chart.register(PieController, ArcElement, Tooltip);

@Component({
  selector: 'app-pie-chart',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="chart-layout">
      <div class="canvas-container">
        <canvas
          #canvas
          role="img"
          aria-label="Pie chart showing each employee's share of total working time. Values are listed in the legend and employee table."
        ></canvas>
      </div>
      <ul class="chart-legend" aria-label="Chart values">
        @for (employee of employees(); track employee.name; let index = $index) {
          <li>
            <span
              class="legend-dot"
              [style.background-color]="color(index)"
              aria-hidden="true"
            ></span
            ><span>{{ employee.name }}</span
            ><strong>{{ employee.sharePercent | number: '1.1-1' }}%</strong>
          </li>
        }
      </ul>
    </div>
  `,
})
export class PieChart implements OnDestroy {
  readonly employees = input.required<readonly EmployeeSummary[]>();
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart?: Chart<'pie', number[], string>;
  private ready = false;
  private readonly palette = [
    '#1F6D5A',
    '#DC764C',
    '#6E82B6',
    '#D8AC42',
    '#9A6B91',
    '#4E969D',
    '#947653',
    '#99AA64',
    '#D68491',
    '#607184',
  ];

  constructor() {
    afterNextRender(() => {
      this.ready = true;
      this.draw();
    });
    effect(() => {
      this.employees();
      if (this.ready) this.draw();
    });
  }

  color(index: number): string {
    return this.palette[index % this.palette.length];
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private draw(): void {
    this.chart?.destroy();
    const employees = this.employees();
    this.chart = new Chart(this.canvas().nativeElement, {
      type: 'pie',
      data: {
        labels: employees.map((employee) => employee.name),
        datasets: [
          {
            data: employees.map((employee) => employee.totalHours),
            backgroundColor: employees.map((_, index) => this.color(index)),
            borderColor: '#ffffff',
            borderWidth: 3,
            hoverOffset: 5,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        plugins: {
          tooltip: {
            callbacks: {
              label: (context) => {
                const employee = employees[context.dataIndex];
                return `${employee.totalHours.toFixed(2)} hours (${employee.sharePercent.toFixed(1)}%)`;
              },
            },
          },
        },
      },
    });
  }
}
