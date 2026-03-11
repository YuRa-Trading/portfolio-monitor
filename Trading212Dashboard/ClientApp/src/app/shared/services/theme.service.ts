import { Injectable, signal } from '@angular/core';

const COOKIE_KEY = 'yura-dash-theme';
type Theme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly mode = signal<Theme>(this.readCookie());

  constructor() {
    this.applyTheme(this.mode());
  }

  toggle(): void {
    const next: Theme = this.mode() === 'dark' ? 'light' : 'dark';
    this.mode.set(next);
    this.writeCookie(next);
    this.applyTheme(next);
  }

  private applyTheme(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
  }

  private readCookie(): Theme {
    const match = document.cookie
      .split('; ')
      .find((c) => c.startsWith(`${COOKIE_KEY}=`));
    const value = match?.split('=')[1];
    return value === 'light' ? 'light' : 'dark';
  }

  private writeCookie(theme: Theme): void {
    document.cookie = `${COOKIE_KEY}=${theme};path=/;max-age=31536000;SameSite=Lax`;
  }
}
