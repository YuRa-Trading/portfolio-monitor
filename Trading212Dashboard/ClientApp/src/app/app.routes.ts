import { Routes } from '@angular/router';
import { Component } from '@angular/core';

// Placeholder — routes exist only for URL matching, rendering is in AppComponent
@Component({ template: '', standalone: true })
class Blank {}

export const routes: Routes = [
  { path: '', redirectTo: 'positions', pathMatch: 'full' },
  { path: 'positions', component: Blank },
  { path: 'position/:ticker', component: Blank },
  { path: 'analytics', component: Blank },
  { path: 'orders', component: Blank },
  { path: 'news', component: Blank },
  { path: '**', redirectTo: 'positions' },
];
