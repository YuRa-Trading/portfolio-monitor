import { Component, inject, input, output, OnInit, signal } from '@angular/core';
import { PositionDetailResponse } from '../../shared/models/portfolio.model';
import { FormatGbpPipe } from '../../shared/pipes/format-gbp.pipe';
import { FormatPctPipe } from '../../shared/pipes/format-pct.pipe';
import { ApiService } from '../../shared/services/api.service';

@Component({
  selector: 'app-position-detail',
  imports: [FormatGbpPipe, FormatPctPipe],
  templateUrl: './position-detail.component.html',
  styleUrl: './position-detail.component.scss'
})
export class PositionDetailComponent implements OnInit {
  private api = inject(ApiService);

  ticker = input.required<string>();
  onBack = output<void>();

  detail = signal<PositionDetailResponse | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getPositionDetail(this.ticker()).subscribe({
      next: (data) => {
        this.detail.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.message ?? 'Failed to load position detail');
        this.loading.set(false);
      }
    });
  }

  goBack(): void {
    this.onBack.emit();
  }

  formatHoldingPeriod(days: number): string {
    if (days < 30) return days + 'd';
    const months = Math.floor(days / 30);
    if (months < 12) return months + 'mo';
    const years = Math.floor(months / 12);
    const remainingMonths = months % 12;
    return remainingMonths > 0 ? years + 'y ' + remainingMonths + 'mo' : years + 'y';
  }

  formatPrice(val: number | null, currency: string): string {
    if (val == null) return '\u2014';
    if (currency === 'GBX') return val.toFixed(2) + 'p';
    const sym = currency === 'USD' ? '$' : currency === 'EUR' ? '\u20AC' : '';
    return sym + val.toFixed(2);
  }

  dailyPLAvg(detail: PositionDetailResponse): number {
    if (!detail.holdingDays) return 0;
    return detail.profitLoss / detail.holdingDays;
  }

  priceChange(detail: PositionDetailResponse): number {
    if (!detail.averagePrice) return 0;
    return ((detail.currentPrice - detail.averagePrice) / detail.averagePrice) * 100;
  }
}
