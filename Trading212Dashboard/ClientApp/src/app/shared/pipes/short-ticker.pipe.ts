import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'shortTicker', standalone: true })
export class ShortTickerPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    return value.split('_')[0];
  }
}
