import {
  Component,
  ElementRef,
  ViewChild,
  OnDestroy,
  input,
  effect,
} from '@angular/core';
import { Chart, registerables } from 'chart.js';

function getThemeColors() {
  const s = getComputedStyle(document.documentElement);
  return {
    green: s.getPropertyValue('--green').trim(),
    cyan: s.getPropertyValue('--cyan').trim(),
    gold: s.getPropertyValue('--gold').trim(),
    red: s.getPropertyValue('--red').trim(),
    purple: s.getPropertyValue('--purple').trim(),
    muted: s.getPropertyValue('--muted').trim(),
    text: s.getPropertyValue('--text').trim(),
    dim: s.getPropertyValue('--dim').trim(),
    border: s.getPropertyValue('--border').trim(),
  };
}

function shortTicker(ticker: string): string {
  return ticker ? ticker.split('_')[0] : '';
}

@Component({
  selector: 'app-pl-bar-chart',
  standalone: true,
  imports: [],
  templateUrl: './pl-bar-chart.component.html',
  styleUrl: './pl-bar-chart.component.scss',
})
export class PlBarChartComponent implements OnDestroy {
  @ViewChild('chartCanvas', { static: true })
  chartCanvas!: ElementRef<HTMLCanvasElement>;

  data = input<any[]>([]);

  private chart: Chart | null = null;

  constructor() {
    Chart.register(...registerables);

    effect(() => {
      const d = this.data();
      if (d && d.length > 0) {
        this.buildChart(d);
      }
    });
  }

  private buildChart(data: any[]): void {
    if (this.chart) {
      this.chart.destroy();
      this.chart = null;
    }

    const theme = getThemeColors();
    const sorted = [...data].sort(
      (a, b) => (b.plPct ?? 0) - (a.plPct ?? 0)
    );
    const top12 = sorted.slice(0, 12);
    const labels = top12.map((item) => shortTicker(item.ticker));
    const values = top12.map((item) => item.plPct ?? 0);
    const bgColors = values.map((v) =>
      v >= 0 ? theme.green : theme.red
    );

    this.chart = new Chart(this.chartCanvas.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [
          {
            label: 'P/L %',
            data: values,
            backgroundColor: bgColors,
            borderRadius: 3,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        plugins: {
          legend: {
            display: false,
          },
          tooltip: {
            backgroundColor: 'rgba(8,8,20,0.92)',
            titleColor: '#fff',
            bodyColor: '#ccc',
            bodyFont: { family: 'JetBrains Mono', size: 11 },
            callbacks: {
              label: (ctx) => {
                const item = top12[ctx.dataIndex];
                const pct = Number(item.plPct ?? 0).toFixed(2);
                const pl = Number(item.profitLoss ?? 0).toFixed(2);
                return `${pct}% (\u00A3${pl})`;
              },
            },
          },
        },
        scales: {
          x: {
            ticks: {
              color: theme.text,
              font: { size: 10 },
            },
            grid: {
              display: false,
            },
          },
          y: {
            ticks: {
              color: theme.dim,
              font: { size: 10 },
              callback: (value) => `${Number(value).toFixed(0)}%`,
            },
            grid: {
              color: theme.border + '33',
            },
          },
        },
      },
    });
  }

  ngOnDestroy(): void {
    if (this.chart) {
      this.chart.destroy();
      this.chart = null;
    }
  }
}
