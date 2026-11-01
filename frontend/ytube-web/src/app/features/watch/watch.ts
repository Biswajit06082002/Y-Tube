import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../../core/api/api.models';
import { SignInPromptService } from '../../core/auth/sign-in-prompt.service';
import { Video, VideoService } from '../../core/videos/video.service';
import { ChannelAvatar } from '../../shared/channel-avatar';
import { compactNumber, duration, timeAgo } from '../../shared/format';
import { Icon } from '../../shared/icon';
import { VideoThumbnail } from '../../shared/video-thumbnail';

/** Watch page: public to view; like / subscribe go through the sign-in gate. */
@Component({
  selector: 'app-watch',
  imports: [RouterLink, Icon, ChannelAvatar, VideoThumbnail],
  template: `
    <div class="mx-auto flex max-w-[1600px] flex-col gap-6 pt-2 xl:flex-row">
      <section class="min-w-0 flex-1">
        @if (error()) {
          <div class="panel py-24 text-center">
            <p class="text-lg">{{ error() }}</p>
            <a routerLink="/" class="btn-soft mt-4">Back to Discover</a>
          </div>
        } @else if (video(); as v) {
          <!-- Player placeholder: real streaming arrives with uploads + processing -->
          <div class="relative overflow-hidden rounded-3xl shadow-2xl shadow-accent/20">
            <app-video-thumbnail [videoId]="v.id" [title]="v.title" rounded="rounded-3xl" />
            <div class="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-black/35 px-4 text-center text-white">
              <span class="flex h-20 w-20 items-center justify-center rounded-full bg-white/90 text-neutral-900 shadow-xl">
                <app-icon name="play" [size]="40" />
              </span>
              <span class="text-sm text-white/85">Playback arrives once videos can be uploaded</span>
            </div>
            <div class="absolute inset-x-0 bottom-0 flex items-center gap-3 bg-gradient-to-t from-black/60 to-transparent px-5 pt-10 pb-4 text-xs text-white">
              <span>0:00</span>
              <span class="h-1 flex-1 overflow-hidden rounded-full bg-white/30"><span class="brand-gradient block h-full w-[8%]"></span></span>
              <span>{{ length() }}</span>
            </div>
          </div>

          <div class="mt-5 flex flex-wrap items-start justify-between gap-4">
            <div class="min-w-0">
              <span class="text-xs font-semibold tracking-wider text-accent uppercase">{{ v.category }}</span>
              <h1 class="mt-1 font-display text-2xl leading-tight font-bold tracking-tight">{{ v.title }}</h1>
              <p class="mt-1 text-sm text-muted">{{ meta() }}</p>
            </div>
            <div class="flex items-center gap-2">
              <button
                (click)="toggleLike()"
                [disabled]="liking()"
                class="inline-flex items-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition active:scale-95"
                [class]="v.likedByMe ? 'brand-gradient text-white shadow-lg shadow-accent/30' : 'glass hover:bg-surface-2'"
                [attr.aria-pressed]="v.likedByMe"
              >
                <app-icon name="like" [size]="18" />
                {{ likes() }}
              </button>
              <button (click)="share()" class="btn-soft">
                <app-icon name="share" [size]="18" />
                {{ copied() ? 'Copied!' : 'Share' }}
              </button>
            </div>
          </div>

          <div class="panel mt-5 flex flex-wrap items-center justify-between gap-4 p-4">
            <div class="flex items-center gap-3">
              <app-channel-avatar [name]="v.channelName" [size]="44" />
              <div>
                <p class="font-semibold">{{ v.channelName }}</p>
                <p class="text-xs text-muted">Creator</p>
              </div>
            </div>
            <button (click)="subscribe()" class="btn-primary">Follow</button>
            <p class="w-full text-sm leading-relaxed whitespace-pre-line text-muted">{{ v.description }}</p>
          </div>
        } @else {
          <div class="aspect-video animate-pulse rounded-3xl bg-surface-2"></div>
          <div class="mt-5 h-7 w-2/3 animate-pulse rounded-lg bg-surface-2"></div>
        }
      </section>

      <!-- Up next -->
      <aside class="w-full shrink-0 xl:w-[380px]">
        <h2 class="mb-3 px-2 font-display text-lg font-semibold">Up next</h2>
        <div class="space-y-1">
          @for (n of upNext(); track n.id) {
            <a [routerLink]="['/watch', n.id]" class="group flex gap-3 rounded-2xl border border-transparent p-2 transition hover:border-[var(--glass-border)] hover:bg-fg/5">
              <div class="w-40 shrink-0">
                <app-video-thumbnail [videoId]="n.id" [title]="n.title" [seconds]="n.durationSeconds" rounded="rounded-xl" />
              </div>
              <div class="min-w-0 py-0.5">
                <h3 class="line-clamp-2 text-sm leading-snug font-semibold transition group-hover:text-accent">{{ n.title }}</h3>
                <p class="mt-1 text-xs text-muted">{{ n.channelName }}</p>
                <p class="text-xs text-muted">{{ format(n.views) }} views · {{ ago(n.publishedAtUtc) }}</p>
              </div>
            </a>
          }
        </div>
      </aside>
    </div>
  `,
})
export class Watch {
  private readonly videos = inject(VideoService);
  private readonly gate = inject(SignInPromptService);

  /** :id route param. */
  readonly id = input.required<string>();

  protected readonly video = signal<Video | null>(null);
  protected readonly upNext = signal<Video[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly liking = signal(false);
  protected readonly copied = signal(false);

  protected readonly likes = computed(() => compactNumber(this.video()?.likeCount ?? 0));
  protected readonly length = computed(() => duration(this.video()?.durationSeconds ?? 0));
  protected readonly meta = computed(() => {
    const v = this.video();
    return v ? `${compactNumber(v.views)} views · ${timeAgo(v.publishedAtUtc)}` : '';
  });

  protected readonly format = compactNumber;
  protected readonly ago = timeAgo;

  constructor() {
    // Re-runs when navigating from one video to another (same component instance).
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });
  }

  toggleLike(): void {
    const v = this.video();
    if (!v) return;
    if (!this.gate.require({ title: 'Like this video?', message: 'Sign in to make your opinion count and find it later in Liked.' })) return;

    // Optimistic update: flip the button immediately, roll back if the server says no.
    const previous = v;
    this.video.set({ ...v, likedByMe: !v.likedByMe, likeCount: v.likeCount + (v.likedByMe ? -1 : 1) });
    this.liking.set(true);

    this.videos.setLike(v.id, !v.likedByMe).subscribe({
      next: (updated) => {
        this.video.set(updated);
        this.liking.set(false);
      },
      error: () => {
        this.video.set(previous);
        this.liking.set(false);
      },
    });
  }

  subscribe(): void {
    if (!this.gate.require({ title: 'Follow this creator?', message: 'Sign in to follow creators and see their new videos in Following.' })) return;
    this.gate.prompt.set({
      title: 'Following is coming soon',
      message: 'Creators and following arrive with the User service in Phase 2.',
      infoOnly: true,
    });
  }

  share(): void {
    navigator.clipboard?.writeText(window.location.href).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }

  private load(id: string): void {
    this.video.set(null);
    this.error.set(null);
    window.scrollTo({ top: 0 });

    this.videos.video(id).subscribe({
      next: (v) => this.video.set(v),
      error: (err) => this.error.set(errorMessage(err)),
    });
    this.videos.feed(1, 16, null).subscribe({
      next: (page) => this.upNext.set(page.items.filter((v) => v.id !== id)),
      error: () => {},
    });
  }
}
