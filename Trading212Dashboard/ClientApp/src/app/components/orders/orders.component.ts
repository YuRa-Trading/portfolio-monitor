import { Component, computed, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormatGbpPipe } from '../../shared/pipes/format-gbp.pipe';
import { ShortTickerPipe } from '../../shared/pipes/short-ticker.pipe';
import { OrdersResponse } from '../../shared/models/portfolio.model';

type SortKey = 'date' | 'ticker' | 'side' | 'quantity' | 'fillPrice' | 'netValue' | 'realisedPL';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-orders',
  imports: [DatePipe, FormatGbpPipe, ShortTickerPipe],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.scss'
})
export class OrdersComponent {
  orders = input<OrdersResponse | null>(null);

  filter = signal('');
  sortKey = signal<SortKey>('date');
  sortDir = signal<SortDir>('desc');

  filteredAndSorted = computed(() => {
    const data = this.orders();
    if (!data?.items?.length) return [];

    const term = this.filter().toLowerCase().trim();
    let items = data.items;

    if (term) {
      items = items.filter(o =>
        o.symbol.toLowerCase().includes(term) ||
        o.name.toLowerCase().includes(term) ||
        o.side.toLowerCase().includes(term)
      );
    }

    const key = this.sortKey();
    const dir = this.sortDir();

    return [...items].sort((a, b) => {
      let cmp = 0;
      switch (key) {
        case 'date':
          cmp = new Date(a.date).getTime() - new Date(b.date).getTime();
          break;
        case 'ticker':
          cmp = a.symbol.localeCompare(b.symbol);
          break;
        case 'side':
          cmp = a.side.localeCompare(b.side);
          break;
        case 'quantity':
          cmp = a.quantity - b.quantity;
          break;
        case 'fillPrice':
          cmp = (a.fillPrice ?? 0) - (b.fillPrice ?? 0);
          break;
        case 'netValue':
          cmp = (a.netValue ?? 0) - (b.netValue ?? 0);
          break;
        case 'realisedPL':
          cmp = (a.realisedPL ?? 0) - (b.realisedPL ?? 0);
          break;
      }
      return dir === 'asc' ? cmp : -cmp;
    });
  });

  totalCount = computed(() => this.filteredAndSorted().length);

  buyCount = computed(() =>
    this.filteredAndSorted().filter(o => o.side === 'BUY').length
  );

  sellCount = computed(() =>
    this.filteredAndSorted().filter(o => o.side === 'SELL').length
  );

  totalValue = computed(() =>
    this.filteredAndSorted().reduce((sum, o) => sum + (o.netValue ?? 0), 0)
  );

  totalRealisedPL = computed(() =>
    this.filteredAndSorted().reduce((sum, o) => sum + (o.realisedPL ?? 0), 0)
  );

  toggleSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortKey.set(key);
      this.sortDir.set(key === 'date' ? 'desc' : 'asc');
    }
  }

  sortArrow(key: SortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? ' \u25B2' : ' \u25BC';
  }

  onFilter(event: Event): void {
    this.filter.set((event.target as HTMLInputElement).value);
  }

  formatPrice(val: number | null, currency: string): string {
    if (val == null) return '\u2014';
    if (currency === 'GBX') return val.toFixed(2) + 'p';
    if (currency === 'USD') return '$' + val.toFixed(2);
    if (currency === 'EUR') return '\u20AC' + val.toFixed(2);
    return val.toFixed(2);
  }
}
