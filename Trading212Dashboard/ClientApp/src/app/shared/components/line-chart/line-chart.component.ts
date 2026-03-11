import {
  Component,
  ElementRef,
  ViewChild,
  OnDestroy,
  input,
  effect,
  signal,
} from '@angular/core';
import { Chart, registerables } from 'chart.js';

interface LegendItem {
  label: string;
  color: string;
}

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

@Component({
  selector: 'app-line-chart',
  standalone: true,
  imports: [],
  templateUrl: './line-chart.component.html',
  styleUrl: './line-chart.component.scss',
})
export class LineChartComponent implements OnDestroy {
  @ViewChild('chartCanvas', { static: true })
  chartCanvas!: ElementRef<HTMLCanvasElement>;

  data = input<any[]>([]);
  labelKey = input<string>('');
  valueKeys = input<string[]>([]);
  colors = input<string[]>([]);
  labels = input<string[]>([]);
  yPrefix = input<string>('£');

  legendItems = signal<LegendItem[]>([]);

  private chart: Chart | null = null;

  constructor() {
    Chart.register(...registerables);

    effect(() => {
      const d = this.data();
      const lk = this.labelKey();
      const vk = this.valueKeys();
      const c = this.colors();
      const l = this.labels();
      const yp = this.yPrefix();
      if (d && d.length > 0 && lk && vk.length > 0) {
        this.buildChart(d, lk, vk, c, l, yp);
      }
    });
  }

  private buildChart(
    data: any[],
    labelKey: string,
    valueKeys: string[],
    colors: string[],
    datasetLabels: string[],
    yPrefix: string
  ): void {
    if (this.chart) {
      this.chart.destroy();
      this.chart = null;
    }

    const theme = getThemeColors();
    const defaultColors = [
      theme.green,
      theme.cyan,
      theme.gold,
      theme.red,
      theme.purple,
    ];

    const resolveColor = (key: string, i: number) =>
      (theme as Record<string, string>)[key] || key || defaultColors[i % defaultColors.length];

    const xLabels = data.map((item) => item[labelKey]);
    const hidePoints = data.length > 30;
    const singleDataset = valueKeys.length === 1;

    const datasets = valueKeys.map((key, i) => {
      const color = resolveColor(colors[i], i);
      return {
        label: datasetLabels[i] || key,
        data: data.map((item) => item[key]),
        borderColor: color,
        backgroundColor: singleDataset ? color + '22' : 'transparent',
        fill: singleDataset,
        tension: 0.3,
        pointRadius: hidePoints ? 0 : 3,
        pointHoverRadius: hidePoints ? 4 : 5,
        borderWidth: 2,
      };
    });

    // Build custom legend items
    if (!singleDataset) {
      this.legendItems.set(
        datasets.map((ds) => ({
          label: ds.label,
          color: ds.borderColor,
        }))
      );
    } else {
      this.legendItems.set([]);
    }

    // Determine if values are small (percentages) or large (portfolio values)
    const allValues = datasets.flatMap((ds) => ds.data.filter((v): v is number => v != null));
    const maxVal = Math.max(...allValues.map(Math.abs), 0);
    const isPercentage = yPrefix === '%' || maxVal < 200;

    this.chart = new Chart(this.chartCanvas.nativeElement, {
      type: 'line',
      data: {
        labels: xLabels,
        datasets,
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          mode: 'index',
          intersect: false,
        },
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: 'rgba(8,8,20,0.92)',
            titleColor: '#fff',
            bodyColor: '#ccc',
            bodyFont: { family: 'JetBrains Mono', size: 11 },
            callbacks: {
              label: (ctx) => {
                const val = ctx.parsed.y ?? 0;
                if (isPercentage) {
                  return `${ctx.dataset.label}: ${val >= 0 ? '+' : ''}${val.toFixed(2)}%`;
                }
                return `${ctx.dataset.label}: £${val.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
              },
            },
          },
        },
        scales: {
          x: {
            ticks: {
              color: theme.dim,
              maxTicksLimit: 8,
              font: { size: 10 },
            },
            grid: {
              color: theme.border + '33',
            },
          },
          y: {
            ticks: {
              color: theme.dim,
              font: { size: 10 },
              callback: (value) => {
                const num = Number(value);
                if (isPercentage) {
                  return `${num >= 0 ? '+' : ''}${num.toFixed(1)}%`;
                }
                if (Math.abs(num) >= 1000) {
                  return `£${(num / 1000).toFixed(num % 1000 === 0 ? 0 : 1)}k`;
                }
                return `£${num.toFixed(0)}`;
              },
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
