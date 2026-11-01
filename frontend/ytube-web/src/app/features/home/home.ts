import {
  Component,
  ElementRef,
  OnDestroy,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api/api.models';
import { Video, VideoService } from '../../core/videos/video.service';
import { ChannelAvatar } from '../../shared/channel-avatar';
import { Icon } from '../../shared/icon';
import { VideoCard } from '../../shared/video-card';
import { VideoThumbnail } from '../../shared/video-thumbnail';

const PAGE_SIZE = 12;

/** Public home feed (no login needed), with category chips and infinite scroll. */
@Component({
  selector: 'app-home',
  imports: [VideoCard, VideoThumbnail, ChannelAvatar, Icon, RouterLink],
  template: `
    @if (featured(); as f) {
      <!-- Featured hero: only on the unfiltered feed -->
      <a [routerLink]="['/watch', f.id]" class="group panel glass-interactive relative mt-1 mb-6 grid overflow-hidden md:grid-cols-[1.3fr_1fr]">
        <app-video-thumbnail [videoId]="f.id" [title]="f.title" [seconds]="f.durationSeconds" rounded="rounded-none" [fill]="true" />
        <div class="relative flex flex-col justify-center gap-3 p-6 md:p-10">
          <span class="inline-flex w-fit items-center gap-1.5 rounded-full bg-accent/12 px-3 py-1 text-xs font-semibold text-accent">
            <app-icon name="sparkle" [size]="14" /> Featured · {{ f.category }}
          </span>
          <h1 class="font-display text-2xl leading-tight font-bold tracking-tight xl:text-4xl">{{ f.title }}</h1>
          <p class="line-clamp-2 text-muted">{{ f.description }}</p>
          <div class="mt-2 flex items-center gap-3">
            <app-channel-avatar [name]="f.channelName" [size]="32" />
            <span class="text-sm font-medium">{{ f.channelName }}</span>
          </div>
          <span class="btn-primary mt-3 w-fit"><app-icon name="play" [size]="18" /> Watch now</span>
        </div>
      </a>
    }

    <!-- Category chips -->
    <!-- Category tabs: a floating glass segmented control (sticks under the top bar) -->
    <div class="sticky top-[84px] z-10 mb-4 flex">
      <div class="glass-strong flex max-w-full gap-1 overflow-x-auto rounded-2xl p-1.5 [scrollbar-width:none]" role="tablist">
        @for (c of chips(); track c) {
          <button
            (click)="selectCategory(c)"
            class="chip"
            role="tab"
            [class.active]="(category() ?? 'All') === c"
            [attr.aria-selected]="(category() ?? 'All') === c"
          >
            {{ c }}
          </button>
        }
      </div>
    </div>

    <div class="pt-2">
      @if (q()) {
        <p class="mb-4 px-2 text-sm text-muted">
          Showing loaded videos matching <span class="font-semibold text-fg">"{{ q() }}"</span>
          (real search comes in a later phase)
        </p>
      }

      @if (error()) {
        <div class="panel mx-auto mt-10 max-w-md p-10 text-center">
          <p>{{ error() }}</p>
          <button (click)="reload()" class="btn-soft mt-4">Try again</button>
        </div>
      } @else {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
          @for (v of gridItems(); track v.id) {
            <app-video-card [video]="v" />
          }
          @if (loading()) {
            @for (i of skeletons; track i) {
              <div class="animate-pulse p-2">
                <div class="aspect-video rounded-2xl bg-surface-2"></div>
                <div class="mt-3 flex gap-3 px-1">
                  <div class="h-9 w-9 rounded-[38%] bg-surface-2"></div>
                  <div class="flex-1 space-y-2">
                    <div class="h-4 w-11/12 rounded bg-surface-2"></div>
                    <div class="h-4 w-2/3 rounded bg-surface-2"></div>
                  </div>
                </div>
              </div>
            }
          }
        </div>

        @if (!loading() && visible().length === 0) {
          <p class="py-20 text-center text-muted">No videos found.</p>
        }
      }

      <!-- When this comes into view, the next page loads -->
      <div #sentinel class="h-1"></div>
    </div>
  `,
})
export class Home implements OnDestroy {
  private readonly videos = inject(VideoService);
  private readonly router = inject(Router);

  /** ?category= and ?q= query params (bound by withComponentInputBinding). */
  readonly category = input<string | null>(null);
  readonly q = input<string | null>(null);

  private readonly sentinel = viewChild.required<ElementRef<HTMLElement>>('sentinel');
  private observer?: IntersectionObserver;

  protected readonly categories = signal<string[]>([]);
  protected readonly chips = computed(() => ['All', ...this.categories()]);
  protected readonly items = signal<Video[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly skeletons = Array.from({ length: 8 }, (_, i) => i);
  protected readonly visible = computed(() => {
    const q = this.q()?.toLowerCase().trim();
    return q ? this.items().filter((v) => `${v.title} ${v.channelName}`.toLowerCase().includes(q)) : this.items();
  });
  /** The first video gets the hero spot on the unfiltered feed; the grid shows the rest. */
  protected readonly featured = computed(() => (!this.category() && !this.q() ? (this.items()[0] ?? null) : null));
  protected readonly gridItems = computed(() => (this.featured() ? this.visible().slice(1) : this.visible()));

  private page = 0;
  private hasMore = true;
  private loadedCategory: string | null | undefined;

  constructor() {
    this.videos.categories().subscribe({ next: (c) => this.categories.set(c), error: () => {} });

    // Chip clicks change the ?category= input; reload the grid when that happens.
    effect(() => {
      this.category();
      untracked(() => this.loadMore());
    });

    afterNextRender(() => {
      this.observer = new IntersectionObserver((entries) => {
        if (entries.some((e) => e.isIntersecting)) this.loadMore();
      }, { rootMargin: '600px' });
      this.observer.observe(this.sentinel().nativeElement);
    });
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  selectCategory(chip: string): void {
    const category = chip === 'All' ? null : chip;
    this.router.navigate([], { queryParams: { category }, queryParamsHandling: 'merge' });
  }

  reload(): void {
    this.error.set(null);
    this.loadMore();
  }

  private loadMore(): void {
    // Category changed (chip click / back button) → start over.
    if (this.loadedCategory !== this.category()) {
      this.loadedCategory = this.category();
      this.items.set([]);
      this.page = 0;
      this.hasMore = true;
    }
    if (this.loading() || !this.hasMore || this.error()) return;

    this.loading.set(true);
    const category = this.category();
    this.videos.feed(this.page + 1, PAGE_SIZE, category).subscribe({
      next: (result) => {
        if (category !== this.category()) {
          // A different category was picked while this page was loading: drop it and load the new one.
          this.loading.set(false);
          this.loadMore();
          return;
        }
        this.page = result.page;
        this.hasMore = result.hasMore;
        this.items.update((list) => [...list, ...result.items]);
        this.loading.set(false);
        this.checkSentinel();
      },
      error: (err) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  /** If the page is still short enough that the sentinel is visible, keep loading. */
  private checkSentinel(): void {
    queueMicrotask(() => {
      const rect = this.sentinel().nativeElement.getBoundingClientRect();
      if (rect.top < window.innerHeight + 600) this.loadMore();
    });
  }
}
