import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'formatPct', standalone: true })
export class FormatPctPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value == null || isNaN(value)) {
      return '\u2014';
    }
    const sign = value >= 0 ? '+' : '';
    return `${sign}${value.toFixed(1)}%`;
  }
}
