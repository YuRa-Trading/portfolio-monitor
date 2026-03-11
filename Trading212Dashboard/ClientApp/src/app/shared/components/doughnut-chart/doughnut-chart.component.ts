import {
  Component,
  ElementRef,
  ViewChild,
  OnDestroy,
  input,
  effect,
  signal,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Chart, registerables } from 'chart.js';

interface LegendItem {
  label: string;
  color: string;
  pct: number;
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
  selector: 'app-doughnut-chart',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './doughnut-chart.component.html',
  styleUrl: './doughnut-chart.component.scss',
})
export class DoughnutChartComponent implements OnDestroy {
  @ViewChild('chartCanvas', { static: true })
  chartCanvas!: ElementRef<HTMLCanvasElement>;

  data = input<any[]>([]);
  labelKey = input<string>('');
  valueKey = input<string>('pct');
  colorKeys = input<string[] | undefined>(undefined);

  legendItems = signal<LegendItem[]>([]);

  private chart: Chart | null = null;

  constructor() {
    Chart.register(...registerables);

    effect(() => {
      const d = this.data();
      const lk = this.labelKey();
      const vk = this.valueKey();
      const ck = this.colorKeys();
      if (d && d.length > 0 && lk) {
        this.buildChart(d, lk, vk, ck);
      }
    });
  }

  private buildChart(
    data: any[],
    labelKey: string,
    valueKey: string,
    colorKeys?: string[]
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
      theme.muted,
      theme.dim,
    ];

    const labels = data.map((item) => item[labelKey]);
    const values = data.map((item) => item[valueKey]);
    const bgColors = colorKeys
      ? colorKeys.map(
          (key) =>
            (theme as Record<string, string>)[key] ||
            defaultColors[0]
        )
      : data.map((_, i) => defaultColors[i % defaultColors.length]);

    // Build legend items
    this.legendItems.set(
      data.map((item, i) => ({
        label: item[labelKey] || '?',
        color: bgColors[i] || defaultColors[0],
        pct: item.pct != null ? Number(item.pct) : Number(item[valueKey]) || 0,
      }))
    );

    this.chart = new Chart(this.chartCanvas.nativeElement, {
      type: 'doughnut',
      data: {
        labels,
        datasets: [
          {
            data: values,
            backgroundColor: bgColors,
            borderWidth: 0,
          },
        ],
      },
      options: {
        cutout: '65%',
        responsive: true,
        maintainAspectRatio: true,
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: 'rgba(8,8,20,0.92)',
            titleColor: '#fff',
            bodyColor: '#ccc',
            bodyFont: { family: 'JetBrains Mono', size: 11 },
            callbacks: {
              label: (ctx) => {
                const item = data[ctx.dataIndex];
                const pct =
                  item.pct != null
                    ? Number(item.pct).toFixed(1)
                    : ctx.formattedValue;
                const value =
                  item.value != null
                    ? Number(item.value).toFixed(2)
                    : '';
                return `${ctx.label}: ${pct}%${value ? ` (\u00A3${value})` : ''}`;
              },
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
