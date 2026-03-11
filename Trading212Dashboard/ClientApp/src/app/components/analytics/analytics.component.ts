import { Component, computed, input } from '@angular/core';
import {
  AnalyticsResponse,
  DividendsResponse,
  InterestResponse,
  SnapshotsResponse,
  DividendCalendarResponse,
  BenchmarkResponse,
  Position,
  DividendCalendarItem,
} from '../../shared/models/portfolio.model';
import { DoughnutChartComponent } from '../../shared/components/doughnut-chart/doughnut-chart.component';
import { LineChartComponent } from '../../shared/components/line-chart/line-chart.component';
import { BarChartComponent } from '../../shared/components/bar-chart/bar-chart.component';
import { PlBarChartComponent } from '../../shared/components/pl-bar-chart/pl-bar-chart.component';
import { FormatGbpPipe } from '../../shared/pipes/format-gbp.pipe';
import { FormatPctPipe } from '../../shared/pipes/format-pct.pipe';
import { ShortTickerPipe } from '../../shared/pipes/short-ticker.pipe';

@Component({
  selector: 'app-analytics',
  imports: [
    DoughnutChartComponent,
    LineChartComponent,
    BarChartComponent,
    PlBarChartComponent,
    FormatGbpPipe,
    FormatPctPipe,
    ShortTickerPipe,
  ],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss',
})
export class AnalyticsComponent {
  data = input<AnalyticsResponse | null>(null);
  dividends = input<DividendsResponse | null>(null);
  interest = input<InterestResponse | null>(null);
  snapshots = input<SnapshotsResponse | null>(null);
  divCalendar = input<DividendCalendarResponse | null>(null);
  benchmark = input<BenchmarkResponse | null>(null);
  positions = input<Position[] | null>(null);

  positionWeightData = computed(() => {
    const list = this.positions();
    if (!list || list.length === 0) return [];

    const sorted = [...list].sort((a, b) => b.weight - a.weight);
    const top8 = sorted.slice(0, 8);
    const rest = sorted.slice(8);

    const items = top8.map((p) => ({
      label: this.shortTicker(p.ticker),
      value: p.currentValue,
      pct: p.weight,
    }));

    if (rest.length > 0) {
      const otherValue = rest.reduce((sum, p) => sum + p.currentValue, 0);
      const otherPct = rest.reduce((sum, p) => sum + p.weight, 0);
      items.push({ label: 'Other', value: otherValue, pct: otherPct });
    }

    return items;
  });

  benchmarkValueKeys = computed(() => {
    const b = this.benchmark();
    if (!b) return [] as string[];
    const keys: string[] = [];
    if (b.portfolio?.length > 0) keys.push('value');
    b.benchmarks?.forEach((_, i) => keys.push(`bench${i}`));
    return keys;
  });

  benchmarkColors = computed(() => {
    const b = this.benchmark();
    if (!b) return [] as string[];
    const colors: string[] = [];
    const palette = ['cyan', 'gold', 'red', 'purple'];
    if (b.portfolio?.length > 0) colors.push('green');
    b.benchmarks?.forEach((_, i) => colors.push(palette[i % palette.length]));
    return colors;
  });

  benchmarkLabels = computed(() => {
    const b = this.benchmark();
    if (!b) return [] as string[];
    const labels: string[] = [];
    if (b.portfolio?.length > 0) labels.push('Portfolio');
    b.benchmarks?.forEach((s) => labels.push(s.name));
    return labels;
  });

  benchmarkMergedData = computed(() => {
    const b = this.benchmark();
    if (!b) return [];
    // Need at least benchmarks OR portfolio data
    const hasBenchmarks = b.benchmarks?.some(s => s.data?.length > 0);
    const hasPortfolio = b.portfolio?.length > 0;
    if (!hasBenchmarks && !hasPortfolio) return [];

    const dateMap = new Map<string, Record<string, number>>();

    // Include portfolio data points
    if (hasPortfolio) {
      b.portfolio.forEach((p) => {
        const entry = dateMap.get(p.date) || {};
        entry['value'] = p.value;
        dateMap.set(p.date, entry);
      });
    }

    b.benchmarks?.forEach((series, i) => {
      series.data.forEach((p) => {
        const entry = dateMap.get(p.date) || {};
        entry[`bench${i}`] = p.value;
        dateMap.set(p.date, entry);
      });
    });

    return Array.from(dateMap.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([date, vals]) => ({ date, ...vals }));
  });

  sortedCalendarItems = computed(() => {
    const cal = this.divCalendar();
    if (!cal?.items?.length) return [];
    return [...cal.items].sort(
      (a, b) => new Date(a.nextExpected).getTime() - new Date(b.nextExpected).getTime()
    );
  });

  projectedAnnualDividends = computed(() => {
    const cal = this.divCalendar();
    if (!cal?.items?.length) return 0;
    return cal.items.reduce((sum, item) => sum + (item.projectedAnnual || 0), 0);
  });

  truncate(s: string, n: number): string {
    return s?.length > n ? s.slice(0, n) + '\u2026' : s || '';
  }

  shortTicker(t: string): string {
    return t?.split('_')[0] || '';
  }

  calendarBadge(item: DividendCalendarItem): string {
    if (item.daysUntilNext < 14) return 'soon';
    if (item.daysUntilNext < 60) return 'upcoming';
    return 'later';
  }

  calendarBadgeLabel(item: DividendCalendarItem): string {
    if (item.daysUntilNext < 14) return 'Soon';
    if (item.daysUntilNext < 60) return 'Upcoming';
    return 'Later';
  }
}
