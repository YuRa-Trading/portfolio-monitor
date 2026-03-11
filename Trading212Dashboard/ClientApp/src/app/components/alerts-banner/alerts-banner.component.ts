import { Component, computed, input } from '@angular/core';
import { Alert } from '../../shared/models/portfolio.model';

const SEVERITY_ORDER: Record<string, number> = { HIGH: 0, WARN: 1, SIZE: 2 };

@Component({
  selector: 'app-alerts-banner',
  imports: [],
  templateUrl: './alerts-banner.component.html',
  styleUrl: './alerts-banner.component.scss'
})
export class AlertsBannerComponent {
  alerts = input<Alert[] | null>(null);

  sorted = computed(() => {
    const list = this.alerts();
    if (!list) return [];
    return [...list].sort((a, b) =>
      (SEVERITY_ORDER[a.severity] ?? 9) - (SEVERITY_ORDER[b.severity] ?? 9)
    );
  });

  collapsed = true;
  dismissed = false;

  toggle(): void {
    this.collapsed = !this.collapsed;
  }

  dismiss(): void {
    this.dismissed = true;
  }

  severityColor(severity: string): string {
    switch (severity.toUpperCase()) {
      case 'HIGH': return '#ef4444';
      case 'WARN': return '#eab308';
      case 'SIZE': return '#f97316';
      default: return '#6b7280';
    }
  }
}
