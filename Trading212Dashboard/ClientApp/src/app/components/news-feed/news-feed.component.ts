import { Component, computed, input } from '@angular/core';
import { NewsItem } from '../../shared/models/portfolio.model';

@Component({
  selector: 'app-news-feed',
  imports: [],
  templateUrl: './news-feed.component.html',
  styleUrl: './news-feed.component.scss'
})
export class NewsFeedComponent {
  news = input<NewsItem[] | null>(null);

  sorted = computed(() => {
    const items = this.news();
    if (!items?.length) return [];
    return [...items].sort(
      (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime()
    );
  });

  relevanceDots(relevance: number): string {
    const clamped = Math.max(0, Math.min(5, relevance));
    return '\u25CF'.repeat(clamped);
  }

  timeAgo(dateStr: string): string {
    const now = Date.now();
    const then = new Date(dateStr).getTime();
    const diffMs = now - then;
    const minutes = Math.floor(diffMs / 60_000);

    if (minutes < 60) return `${minutes}m ago`;

    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;

    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  }
}
