import { Component, inject, signal, DestroyRef } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { Router, RouterOutlet, RouterLink, RouterLinkActive, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiService } from './shared/services/api.service';
import { ThemeService } from './shared/services/theme.service';
import { HeaderComponent } from './components/header/header.component';
import { StatCardsComponent } from './components/stat-cards/stat-cards.component';
import { AlertsBannerComponent } from './components/alerts-banner/alerts-banner.component';
import { PositionsComponent } from './components/positions/positions.component';
import { PositionDetailComponent } from './components/position-detail/position-detail.component';
import { AnalyticsComponent } from './components/analytics/analytics.component';
import { OrdersComponent } from './components/orders/orders.component';
import { NewsFeedComponent } from './components/news-feed/news-feed.component';

type Tab = 'positions' | 'analytics' | 'orders' | 'news';

@Component({
  selector: 'app-root',
  imports: [
    AsyncPipe,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    HeaderComponent,
    StatCardsComponent,
    AlertsBannerComponent,
    PositionsComponent,
    PositionDetailComponent,
    AnalyticsComponent,
    OrdersComponent,
    NewsFeedComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly api = inject(ApiService);
  readonly theme = inject(ThemeService);

  readonly portfolio$ = this.api.getPortfolio();
  readonly alerts$ = this.api.getAlerts();
  readonly news$ = this.api.getNews();
  readonly analytics$ = this.api.getAnalytics();
  readonly dividends$ = this.api.getDividends();
  readonly orders$ = this.api.getOrders();
  readonly interest$ = this.api.getInterest();
  readonly snapshots$ = this.api.getSnapshots();
  readonly divCalendar$ = this.api.getDividendCalendar();
  readonly benchmark$ = this.api.getBenchmark();
  readonly earningsCalendar$ = this.api.getEarningsCalendar();
  readonly config$ = this.api.getConfig();

  readonly activeTab = signal<Tab>('positions');
  readonly selectedTicker = signal<string | null>(null);

  constructor() {
    // Sync signals from router URL
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(e => this.parseUrl(e.urlAfterRedirects));

    // Parse initial URL (before first NavigationEnd fires)
    this.parseUrl(window.location.pathname);
  }

  toDate(timestamp: string | undefined | null): Date | null {
    return timestamp ? new Date(timestamp) : null;
  }

  selectTicker(ticker: string): void {
    this.router.navigate(['/position', ticker]);
  }

  backToPositions(): void {
    this.router.navigate(['/positions']);
  }

  private parseUrl(url: string): void {
    const segments = url.split('/').filter(Boolean);
    const first = segments[0] || 'positions';

    if (first === 'position' && segments[1]) {
      this.activeTab.set('positions');
      this.selectedTicker.set(decodeURIComponent(segments[1]));
    } else if (['positions', 'analytics', 'orders', 'news'].includes(first)) {
      this.activeTab.set(first as Tab);
      this.selectedTicker.set(null);
    } else {
      this.activeTab.set('positions');
      this.selectedTicker.set(null);
    }
  }
}
