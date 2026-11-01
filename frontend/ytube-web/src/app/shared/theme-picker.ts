import { Component, inject, input } from '@angular/core';
import { THEMES, ThemeService } from '../core/theme/theme.service';

/** Grid of theme swatches. Used in the palette popover and in the account menu. */
@Component({
  selector: 'app-theme-picker',
  template: `
    <p class="mb-3 text-xs font-semibold tracking-wider text-muted uppercase">Theme</p>
    <div class="grid grid-cols-3 gap-2">
      @for (t of themes; track t.id) {
        <button
          (click)="theme.set(t.id)"
          class="group flex flex-col items-center gap-1.5 rounded-2xl p-2 transition hover:bg-surface-2"
          [class.ring-2]="theme.preference() === t.id"
          [class.ring-accent]="theme.preference() === t.id"
          [attr.aria-pressed]="theme.preference() === t.id"
        >
          <span class="relative h-10 w-full overflow-hidden rounded-xl border border-line" [style.background]="t.swatch[0]">
            <span class="absolute bottom-1.5 left-1.5 h-3 w-3 rounded-full" [style.background]="t.swatch[1]"></span>
            <span class="absolute bottom-1.5 left-5 h-3 w-3 rounded-full" [style.background]="t.swatch[2]"></span>
          </span>
          <span class="text-xs font-medium">{{ t.name }}</span>
        </button>
      }
    </div>
    @if (hint()) {
      <p class="mt-3 text-xs text-muted">{{ hint() }}</p>
    }
  `,
})
export class ThemePicker {
  protected readonly theme = inject(ThemeService);
  protected readonly themes = THEMES;
  readonly hint = input<string | null>(null);
}
