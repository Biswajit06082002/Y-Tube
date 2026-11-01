import { Component, input } from '@angular/core';
import { Logo } from '../../shared/logo';

/** Split layout for the login / sign-up / OTP pages: brand panel + form card. */
@Component({
  selector: 'app-auth-card',
  imports: [Logo],
  template: `
    <div class="grid min-h-screen lg:grid-cols-2">
      <div class="brand-gradient relative hidden overflow-hidden p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div class="absolute -top-24 -right-24 h-96 w-96 rounded-full bg-white/10 blur-2xl"></div>
        <div class="absolute -bottom-32 -left-16 h-[28rem] w-[28rem] rounded-full bg-black/10 blur-2xl"></div>
        <span class="relative font-display text-2xl font-bold">ytube.</span>
        <div class="relative">
          <p class="font-display text-5xl leading-[1.05] font-bold tracking-tight">Watch what<br />moves you.</p>
          <p class="mt-4 max-w-sm text-lg text-white/85">Follow creators, keep the videos you love, and make the app look the way you like.</p>
        </div>
        <p class="relative text-sm text-white/70">A system design learning project</p>
      </div>

      <div class="flex items-center justify-center px-4 py-10">
        <div class="panel w-full max-w-md p-8 sm:p-10">
          <div class="mb-10"><app-logo /></div>
          <h1 class="font-display text-3xl font-bold tracking-tight">{{ title() }}</h1>
          <p class="mt-2 mb-8 text-muted">{{ subtitle() }}</p>
          <ng-content />
        </div>
      </div>
    </div>
  `,
})
export class AuthCard {
  readonly title = input.required<string>();
  readonly subtitle = input('');
}
