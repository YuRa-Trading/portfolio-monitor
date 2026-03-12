import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, shareReplay, catchError, of, Subject, switchMap, startWith, map } from 'rxjs';
import {
  PortfolioResponse, Alert, AlertsResponse, NewsItem, AnalyticsResponse,
  DividendsResponse, OrdersResponse, InterestResponse,
  SnapshotsResponse, DividendCalendarResponse,
  PositionDetailResponse, BenchmarkResponse,
  EarningsCalendarResponse, ConfigResponse,
} from '../models/portfolio.model';

const BASE = '/api';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private refresh$ = new Subject<void>();

  private portfolio$ = this.fetch<PortfolioResponse>('/portfolio');
  private alerts$ = this.fetch<AlertsResponse>('/alerts').pipe(map(r => r?.alerts ?? []));
  private news$ = this.fetch<NewsItem[]>('/news?limit=8');
  private analytics$ = this.fetch<AnalyticsResponse>('/analytics');
  private dividends$ = this.fetch<DividendsResponse>('/dividends?limit=20');
  private orders$ = this.fetch<OrdersResponse>('/orders?limit=50');
  private interest$ = this.fetch<InterestResponse>('/interest?limit=50');
  private snapshots$ = this.fetch<SnapshotsResponse>('/snapshots');
  private divCalendar$ = this.fetch<DividendCalendarResponse>('/dividend-calendar');
  private benchmark$ = this.fetch<BenchmarkResponse>('/benchmark');
  private earningsCalendar$ = this.fetch<EarningsCalendarResponse>('/earnings-calendar');
  private config$ = this.http.get<ConfigResponse>(`${BASE}/config`).pipe(
    catchError(() => of(null as unknown as ConfigResponse)),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  getPortfolio() { return this.portfolio$; }
  getAlerts() { return this.alerts$; }
  getNews() { return this.news$; }
  getAnalytics() { return this.analytics$; }
  getDividends() { return this.dividends$; }
  getOrders() { return this.orders$; }
  getInterest() { return this.interest$; }
  getSnapshots() { return this.snapshots$; }
  getDividendCalendar() { return this.divCalendar$; }
  getBenchmark() { return this.benchmark$; }
  getEarningsCalendar() { return this.earningsCalendar$; }
  getConfig() { return this.config$; }

  getPositionDetail(ticker: string): Observable<PositionDetailResponse> {
    return this.http.get<PositionDetailResponse>(`${BASE}/position/${encodeURIComponent(ticker)}`);
  }

  refreshAll(): void {
    // Fetch fresh data from T212 API, then re-trigger all GETs
    this.http.post(`${BASE}/refresh`, {}).pipe(
      catchError(() => of(null))
    ).subscribe(() => this.refresh$.next());
  }

  private fetch<T>(path: string): Observable<T> {
    return this.refresh$.pipe(
      startWith(undefined),
      switchMap(() => this.http.get<T>(`${BASE}${path}`).pipe(
        catchError(() => of(null as unknown as T))
      )),
      shareReplay({ bufferSize: 1, refCount: true }),
    );
  }
}
