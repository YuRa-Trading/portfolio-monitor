import { Component, computed, input, output, signal } from '@angular/core';
import { Position } from '../../shared/models/portfolio.model';
import { FormatGbpPipe } from '../../shared/pipes/format-gbp.pipe';
import { FormatPctPipe } from '../../shared/pipes/format-pct.pipe';

type SortCol = 'name' | 'weight' | 'value' | 'pl' | 'plPct';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-positions',
  imports: [FormatGbpPipe, FormatPctPipe],
  templateUrl: './positions.component.html',
  styleUrl: './positions.component.scss'
})
export class PositionsComponent {
  positions = input<Position[] | null>(null);
  onSelect = output<string>();

  searchTerm = signal('');
  sortCol = signal<SortCol>('weight');
  sortDir = signal<SortDir>('desc');

  filtered = computed(() => {
    const list = this.positions();
    if (!list) return [];
    const term = this.searchTerm().toLowerCase();
    let result = term
      ? list.filter(p =>
          p.ticker.toLowerCase().includes(term) ||
          p.name.toLowerCase().includes(term))
      : [...list];

    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;
    result.sort((a, b) => {
      let av: number | string;
      let bv: number | string;
      switch (col) {
        case 'name':    av = a.name.toLowerCase(); bv = b.name.toLowerCase(); break;
        case 'weight':  av = a.weight; bv = b.weight; break;
        case 'value':   av = a.currentValue; bv = b.currentValue; break;
        case 'pl':      av = a.profitLoss; bv = b.profitLoss; break;
        case 'plPct':   av = a.profitLossPercent; bv = b.profitLossPercent; break;
      }
      return av < bv ? -dir : av > bv ? dir : 0;
    });
    return result;
  });

  totalValue = computed(() => {
    const list = this.filtered();
    return list.reduce((sum, p) => sum + p.currentValue, 0);
  });

  totalPL = computed(() => {
    const list = this.filtered();
    return list.reduce((sum, p) => sum + p.profitLoss, 0);
  });

  toggleSort(col: SortCol): void {
    if (this.sortCol() === col) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortCol.set(col);
      this.sortDir.set('desc');
    }
  }

  sortArrow(col: SortCol): string {
    if (this.sortCol() !== col) return '';
    return this.sortDir() === 'asc' ? '\u25B2' : '\u25BC';
  }

  onSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  selectPosition(ticker: string): void {
    this.onSelect.emit(ticker);
  }

  formatPrice(val: number | null, currency: string): string {
    if (val == null) return '\u2014';
    if (currency === 'GBX') return val.toFixed(2) + 'p';
    const sym = currency === 'USD' ? '$' : currency === 'EUR' ? '\u20AC' : '';
    return sym + val.toFixed(2);
  }
}
