import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ThemeService } from '../../shared/services/theme.service';

@Component({
  selector: 'app-header',
  imports: [DatePipe],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  lastUpdate = input<Date | null>(null);
  theme = input.required<ThemeService>();

  onRefresh = output<void>();
}
