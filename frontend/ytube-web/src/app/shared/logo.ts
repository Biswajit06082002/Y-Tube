import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** YTube mark: a rounded "lens" with a play notch, in the active theme's gradient. */
@Component({
  selector: 'app-logo',
  imports: [RouterLink],
  template: `
    <a routerLink="/" class="flex items-center gap-2.5" aria-label="YTube home">
      <span class="brand-gradient relative flex h-9 w-9 items-center justify-center rounded-[14px] shadow-lg shadow-accent/30">
        <svg viewBox="0 0 24 24" width="16" height="16" class="text-white" fill="currentColor" aria-hidden="true">
          <path d="M8 5.5v13a1 1 0 0 0 1.53.85l10.2-6.5a1 1 0 0 0 0-1.7L9.53 4.65A1 1 0 0 0 8 5.5z" />
        </svg>
      </span>
      @if (showWordmark()) {
        <span class="font-display text-[22px] font-bold tracking-tight">
          ytube<span class="text-accent">.</span>
        </span>
      }
    </a>
  `,
})
export class Logo {
  readonly showWordmark = input(true);
}
