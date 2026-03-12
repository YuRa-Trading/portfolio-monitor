export interface PortfolioResponse {
  positions: Position[];
  cash: CashInfo;
  timestamp: string;
}

export interface Position {
  ticker: string;
  name: string;
  quantity: number;
  averagePrice: number;
  currentPrice: number;
  investedValue: number;
  currentValue: number;
  profitLoss: number;
  profitLossPercent: number;
  currency: string;
  weight: number;
  exchange: string;
  boughtAt: string;
  type: string;
}

export interface CashInfo {
  free: number;
  invested: number;
  total: number;
  result: number;
  pieValue: number;
}

export interface AlertsResponse {
  alerts: Alert[];
}

export interface Alert {
  severity: string;
  type: string;
  ticker: string;
  name: string;
  message: string;
  value: number;
}

export interface OrderItem {
  id: number;
  date: string;
  symbol: string;
  name: string;
  side: string;
  quantity: number;
  fillPrice: number | null;
  currency: string;
  netValue: number | null;
  realisedPL: number | null;
}

export interface OrdersResponse {
  items: OrderItem[];
  totalNetValue: number;
  totalRealisedPL: number;
}

export interface NewsItem {
  title: string;
  link: string;
  source: string;
  date: string;
  relevance: number;
}

export interface AnalyticsResponse {
  byExchange: BreakdownItem[];
  byCurrency: BreakdownItem[];
  byType: BreakdownItem[];
  best3: PerformanceItem[];
  worst3: PerformanceItem[];
  top3: TopPosition[];
  top3Pct: number;
  small: SmallPosition[];
  smallThreshold: number;
  comparison: ComparisonItem[];
}

export interface BreakdownItem {
  region?: string;
  currency?: string;
  type?: string;
  value: number;
  count: number;
  pct: number;
}

export interface PerformanceItem {
  ticker: string;
  name: string;
  plPct: number;
  profitLoss: number;
}

export interface TopPosition {
  ticker: string;
  currentValue: number;
  weight: number;
}

export interface SmallPosition {
  ticker: string;
  name: string;
  currentValue: number;
}

export interface ComparisonItem {
  ticker: string;
  name: string;
  investedValue: number;
  currentValue: number;
  profitLoss: number;
  plPct: number;
}

export interface DividendsResponse {
  items: DividendItem[];
  totalIncome: number;
  count: number;
}

export interface DividendItem {
  ticker: string;
  amount: number;
  quantity: number;
  type: string;
  paidOn: string;
}

export interface InterestResponse {
  items: InterestItem[];
  totalInterest: number;
  count: number;
  dailyAverage: number;
  projectedAnnual: number;
  byMonth: MonthlyInterest[];
}

export interface InterestItem {
  date: string;
  amount: number;
  currency: string;
}

export interface MonthlyInterest {
  month: string;
  total: number;
  count: number;
}

export interface SnapshotsResponse {
  snapshots: Snapshot[];
  count: number;
}

export interface Snapshot {
  date: string;
  totalValue: number;
  invested: number;
  pnl: number;
  pnlPct: number;
  freeCash: number;
  positionCount: number;
}

export interface DividendCalendarResponse {
  items: DividendCalendarItem[];
}

export interface DividendCalendarItem {
  ticker: string;
  symbol: string;
  paymentCount: number;
  lastPayment: string;
  lastAmount: number;
  avgAmount: number;
  frequency: string;
  nextExpected: string;
  daysUntilNext: number;
  projectedAnnual: number;
}

export interface PositionDetailResponse {
  ticker: string;
  name: string;
  isin: string;
  currency: string;
  exchange: string;
  quantity: number;
  averagePrice: number;
  currentPrice: number;
  investedValue: number;
  currentValue: number;
  profitLoss: number;
  profitLossPercent: number;
  weight: number;
  fxImpact: number | null;
  boughtAt: string;
  holdingDays: number;
  totalBought: number;
  totalSold: number;
  totalDividends: number;
  totalReturn: number;
  orderCount: number;
  dividendCount: number;
  orders: DetailOrder[];
  dividends: DetailDividend[];
}

export interface DetailOrder {
  id: number;
  date: string;
  side: string;
  quantity: number;
  fillPrice: number | null;
  netValue: number | null;
  realisedPL: number | null;
}

export interface DetailDividend {
  paidOn: string;
  type: string;
  amount: number;
  quantity: number;
}

export interface BenchmarkResponse {
  portfolio: BenchmarkPoint[];
  benchmarks: BenchmarkSeries[];
}

export interface BenchmarkPoint {
  date: string;
  value: number;
}

export interface BenchmarkSeries {
  name: string;
  data: BenchmarkPoint[];
}

export interface ConfigResponse {
  environment: string;
  accountId: number;
  currencyCode: string;
}

export interface EarningsCalendarResponse {
  items: EarningsCalendarItem[];
  count: number;
}

export interface EarningsCalendarItem {
  ticker: string;
  symbol: string;
  name: string;
  nextEarningsDate: string | null;
  daysUntilEarnings: number | null;
  nextEpsEstimate: number | null;
  nextRevenueEstimate: number | null;
  lastEarningsDate: string | null;
  lastEps: number | null;
  lastEpsEstimate: number | null;
  lastRevenue: number | null;
  lastRevenueEstimate: number | null;
  epsSurprise: number | null;
  epsBeat: boolean | null;
}
