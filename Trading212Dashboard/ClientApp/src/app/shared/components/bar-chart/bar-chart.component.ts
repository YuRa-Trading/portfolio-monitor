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
  selector: 'app-bar-chart',
  standalone: true,
  imports: [],
  templateUrl: './bar-chart.component.html',
  styleUrl: './bar-chart.component.scss',
})
export class BarChartComponent implements OnDestroy {
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
    const top12 = data.slice(0, 12);
    const labels = top12.map((item) => shortTicker(item.ticker));
    const invested = top12.map((item) => item.investedValue);
    const current = top12.map((item) => item.currentValue);

    this.chart = new Chart(this.chartCanvas.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [
          {
            label: 'Invested',
            data: invested,
            backgroundColor: theme.cyan,
            borderRadius: 3,
          },
          {
            label: 'Current',
            data: current,
            backgroundColor: theme.green,
            borderRadius: 3,
          },
        ],
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'top',
            labels: {
              color: theme.text,
              usePointStyle: true,
              pointStyleWidth: 10,
              font: { size: 11 },
            },
          },
          tooltip: {
            backgroundColor: 'rgba(8,8,20,0.92)',
            titleColor: '#fff',
            bodyColor: '#ccc',
            bodyFont: { family: 'JetBrains Mono', size: 11 },
          },
        },
        scales: {
          x: {
            ticks: {
              color: theme.dim,
              font: { size: 10 },
              callback: (value) => {
                const num = Number(value);
                return `\u00A3${(num / 1000).toFixed(num % 1000 === 0 ? 0 : 1)}k`;
              },
            },
            grid: {
              color: theme.border + '33',
            },
          },
          y: {
            ticks: {
              color: theme.text,
              font: { size: 10 },
            },
            grid: {
              display: false,
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
