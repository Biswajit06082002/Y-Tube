import { Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { Icon, IconName } from './icon';

/** What a login-only page shows to signed-out visitors (instead of bouncing them to /login). */
@Component({
  selector: 'app-sign-in-empty-state',
  imports: [Icon],
  template: `
    <div class="panel mx-auto mt-6 flex max-w-xl flex-col items-center px-6 py-16 text-center">
      <span class="brand-gradient mb-6 flex h-20 w-20 items-center justify-center rounded-3xl text-white shadow-xl shadow-accent/30">
        <app-icon [name]="icon()" [size]="40" />
      </span>
      <h2 class="font-display text-2xl font-semibold">{{ title() }}</h2>
      <p class="mt-2 max-w-md text-muted">{{ message() }}</p>
      <button (click)="signIn()" class="btn-primary mt-6">Sign in</button>
    </div>
  `,
})
export class SignInEmptyState {
  private readonly router = inject(Router);

  readonly icon = input<IconName>('account');
  readonly title = input.required<string>();
  readonly message = input.required<string>();

  signIn(): void {
    this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
  }
}
