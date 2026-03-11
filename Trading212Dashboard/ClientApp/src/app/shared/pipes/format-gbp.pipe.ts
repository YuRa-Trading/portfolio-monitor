import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'formatGbp', standalone: true })
export class FormatGbpPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value == null || isNaN(value)) {
      return '\u00A3\u2014';
    }
    const rounded = Math.round(value);
    return '\u00A3' + rounded.toLocaleString('en-GB');
  }
}
