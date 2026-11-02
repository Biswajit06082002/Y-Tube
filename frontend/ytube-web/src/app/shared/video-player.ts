import { Component, ElementRef, OnDestroy, computed, effect, input, signal, untracked, viewChild } from '@angular/core';
import Hls, { Level } from 'hls.js';
import { Icon } from './icon';

interface QualityOption {
  /** hls.js level index, or -1 for automatic. */
  level: number;
  label: string;
  height: number;
}

/**
 * HLS player with a quality menu.
 *
 * How adaptive streaming works here:
 *  - hls.js loads master.m3u8, which lists every rendition (1080p … 144p) with its bandwidth.
 *  - "Auto": hls.js measures how fast segments download and picks the best rendition that keeps
 *    the buffer healthy (ABR — adaptive bitrate). It can move up or down every few seconds.
 *  - Manual: setting hls.currentLevel switches immediately — it drops the buffered segments of the
 *    old quality and loads the new one from the current position. Because every rendition has
 *    keyframes at the same timestamps, playback continues seamlessly.
 * Safari plays HLS natively (no hls.js), so there the browser does ABR and the menu is hidden.
 */
@Component({
  selector: 'app-video-player',
  imports: [Icon],
  template: `
    <div class="group/player relative overflow-hidden rounded-3xl bg-black shadow-2xl shadow-accent/20">
      <video
        #video
        class="aspect-video w-full bg-black"
        controls
        playsinline
        [poster]="poster() ?? ''"
        (loadedmetadata)="ready.set(true)"
      ></video>

      @if (qualities().length > 1) {
        <div class="absolute top-3 right-3">
          <button
            (click)="menuOpen.set(!menuOpen())"
            class="flex items-center gap-1.5 rounded-xl bg-black/55 px-3 py-1.5 text-xs font-semibold text-white backdrop-blur-md transition hover:bg-black/75"
            aria-label="Video quality"
          >
            <app-icon name="settings" [size]="16" />
            {{ currentLabel() }}
          </button>

          @if (menuOpen()) {
            <div class="absolute right-0 mt-2 w-44 overflow-hidden rounded-2xl bg-black/80 py-1.5 text-sm text-white shadow-2xl backdrop-blur-xl">
              <p class="px-4 pt-1 pb-2 text-[11px] font-semibold tracking-wider text-white/50 uppercase">Quality</p>
              @for (q of qualities(); track q.level) {
                <button
                  (click)="select(q.level)"
                  class="flex w-full items-center justify-between px-4 py-2 text-left transition hover:bg-white/10"
                >
                  <span>{{ q.label }}</span>
                  @if (selectedLevel() === q.level) {
                    <app-icon name="check" [size]="16" />
                  }
                </button>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class VideoPlayer implements OnDestroy {
  readonly src = input.required<string>();
  readonly poster = input<string | null>(null);

  private readonly videoRef = viewChild.required<ElementRef<HTMLVideoElement>>('video');
  private hls: Hls | null = null;

  protected readonly ready = signal(false);
  protected readonly menuOpen = signal(false);
  private readonly levels = signal<Level[]>([]);
  /** -1 = Auto. */
  protected readonly selectedLevel = signal(-1);
  /** The level actually playing (in Auto this changes as bandwidth changes). */
  private readonly playingLevel = signal(-1);

  protected readonly qualities = computed<QualityOption[]>(() => {
    const levels = this.levels()
      .map((l, i) => ({ level: i, height: Math.min(l.height, l.width), label: l.name || `${Math.min(l.height, l.width)}p` }))
      .sort((a, b) => b.height - a.height);
    return levels.length ? [{ level: -1, label: 'Auto', height: 0 }, ...levels] : [];
  });

  protected readonly currentLabel = computed(() => {
    const playing = this.levels()[this.playingLevel()];
    const playingName = playing ? playing.name || `${playing.height}p` : '';
    return this.selectedLevel() === -1 ? `Auto${playingName ? ` · ${playingName}` : ''}` : playingName;
  });

  constructor() {
    effect(() => {
      const src = this.src();
      untracked(() => this.load(src));
    });
  }

  select(level: number): void {
    this.menuOpen.set(false);
    this.selectedLevel.set(level);
    if (this.hls) this.hls.currentLevel = level; // -1 re-enables automatic switching
  }

  ngOnDestroy(): void {
    this.hls?.destroy();
  }

  private load(src: string): void {
    const video = this.videoRef().nativeElement;
    this.hls?.destroy();
    this.hls = null;
    this.levels.set([]);
    this.selectedLevel.set(-1);

    if (Hls.isSupported()) {
      const hls = new Hls({ capLevelToPlayerSize: true }); // don't fetch 1080p for a small player
      hls.on(Hls.Events.MANIFEST_PARSED, () => this.levels.set([...hls.levels]));
      hls.on(Hls.Events.LEVEL_SWITCHED, (_, data) => this.playingLevel.set(data.level));
      hls.on(Hls.Events.ERROR, (_, data) => {
        if (!data.fatal) return;
        // Standard recovery: retry network errors, try to recover decoding errors, otherwise give up.
        if (data.type === Hls.ErrorTypes.NETWORK_ERROR) hls.startLoad();
        else if (data.type === Hls.ErrorTypes.MEDIA_ERROR) hls.recoverMediaError();
        else hls.destroy();
      });
      hls.loadSource(src);
      hls.attachMedia(video);
      this.hls = hls;
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
      video.src = src; // Safari / iOS: native HLS
    }
  }
}
