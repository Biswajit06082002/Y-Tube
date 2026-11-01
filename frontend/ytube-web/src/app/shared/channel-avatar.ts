import { Component, computed, input } from '@angular/core';
import { hueFor } from './format';

@Component({
  selector: 'app-channel-avatar',
  template: `
    <span
      class="flex items-center justify-center rounded-[38%] font-display font-bold text-white select-none"
      [style.width.px]="size()"
      [style.height.px]="size()"
      [style.font-size.px]="size() * 0.42"
      [style.background]="color()"
    >
      {{ initial() }}
    </span>
  `,
  host: { class: 'inline-flex shrink-0' },
})
export class ChannelAvatar {
  readonly name = input.required<string>();
  readonly size = input(36);

  protected readonly initial = computed(() => this.name().charAt(0).toUpperCase());
  protected readonly color = computed(() => {
    const h = hueFor(this.name());
    return `linear-gradient(135deg, hsl(${h} 70% 55%), hsl(${(h + 40) % 360} 70% 40%))`;
  });
}
