import { Component, computed, input } from '@angular/core';
import { mediaUrl } from '../core/videos/video.service';
import { Icon } from './icon';
import { duration, hueFor } from './format';

/** Generated "mesh gradient" thumbnail (no real images until uploads exist). */
@Component({
  selector: 'app-video-thumbnail',
  imports: [Icon],
  template: `
    <div
      class="relative overflow-hidden"
      [class]="rounded() + (fill() ? ' h-full min-h-56' : ' aspect-video')"
      [style.background]="background()"
    >
      @if (src()) {
        <img [src]="src()" alt="" loading="lazy" class="absolute inset-0 h-full w-full object-cover" />
      } @else {
        <span
          class="absolute inset-0 flex items-center justify-center font-display text-4xl font-bold tracking-tight text-white/25 select-none sm:text-5xl"
          aria-hidden="true"
        >
          {{ glyph() }}
        </span>
      }
      @if (category()) {
        <span class="absolute top-2.5 left-2.5 rounded-full bg-black/30 px-2.5 py-1 text-[11px] font-semibold text-white backdrop-blur-md">
          {{ category() }}
        </span>
      }
      <span class="absolute inset-0 flex items-center justify-center opacity-0 transition duration-300 group-hover:opacity-100">
        <span class="flex h-14 w-14 scale-75 items-center justify-center rounded-full bg-white/90 text-neutral-900 shadow-xl transition duration-300 group-hover:scale-100">
          <app-icon name="play" [size]="28" />
        </span>
      </span>
      @if (seconds()) {
        <span class="absolute right-2.5 bottom-2.5 rounded-lg bg-black/45 px-2 py-0.5 text-xs font-semibold text-white backdrop-blur-md">
          {{ length() }}
        </span>
      }
    </div>
  `,
})
export class VideoThumbnail {
  readonly videoId = input.required<string>();
  readonly title = input('');
  /** Real thumbnail (relative "/media/…" path). Without one, a gradient is generated from the id. */
  readonly imageUrl = input<string | null>(null);
  readonly category = input('');
  readonly seconds = input(0);
  readonly rounded = input('rounded-2xl');
  /** Fill the parent's height instead of keeping a 16:9 box (used by the hero). */
  readonly fill = input(false);

  protected readonly src = computed(() => mediaUrl(this.imageUrl()));
  protected readonly length = computed(() => duration(this.seconds()));
  protected readonly glyph = computed(() =>
    this.title()
      .split(/\s+/)
      .filter((w) => /^[A-Za-z0-9.]/.test(w))
      .slice(0, 2)
      .map((w) => w[0].toUpperCase())
      .join(''),
  );
  protected readonly background = computed(() => {
    const h = hueFor(this.videoId());
    const h2 = (h + 60) % 360;
    const h3 = (h + 200) % 360;
    return [
      `radial-gradient(at 15% 20%, hsl(${h} 85% 62%) 0px, transparent 55%)`,
      `radial-gradient(at 85% 30%, hsl(${h2} 80% 55%) 0px, transparent 50%)`,
      `radial-gradient(at 50% 100%, hsl(${h3} 70% 45%) 0px, transparent 60%)`,
      `hsl(${h} 45% 22%)`,
    ].join(', ');
  });
}
