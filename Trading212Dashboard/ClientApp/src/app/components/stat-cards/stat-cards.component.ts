import { Component, input } from '@angular/core';
import { PortfolioResponse } from '../../shared/models/portfolio.model';
import { FormatGbpPipe } from '../../shared/pipes/format-gbp.pipe';
import { FormatPctPipe } from '../../shared/pipes/format-pct.pipe';

@Component({
  selector: 'app-stat-cards',
  imports: [FormatGbpPipe, FormatPctPipe],
  templateUrl: './stat-cards.component.html',
  styleUrl: './stat-cards.component.scss'
})
export class StatCardsComponent {
  portfolio = input<PortfolioResponse | null>(null);

  get freeCashPct(): number | null {
    const p = this.portfolio();
    if (!p || !p.cash.total) return null;
    return (p.cash.free / p.cash.total) * 100;
  }

  get plPct(): number | null {
    const p = this.portfolio();
    if (!p || !p.cash.invested) return null;
    return (p.cash.result / p.cash.invested) * 100;
  }
}
