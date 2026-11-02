import { HttpEventType } from '@angular/common/http';
import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { errorMessage } from '../../core/api/api.models';
import { VideoService } from '../../core/videos/video.service';
import { Icon } from '../../shared/icon';

// Mirrors the backend's UploadRules — checked here for instant feedback, enforced again on the server.
const MAX_VIDEO_BYTES = 2 * 1024 ** 3;
const MAX_THUMBNAIL_BYTES = 5 * 1024 ** 2;
const VIDEO_EXTENSIONS = ['.mp4', '.mov', '.m4v', '.mkv', '.webm', '.avi'];
const IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp'];
const MAX_TAGS = 15;
const MAX_TAG_LENGTH = 30;

const extensionOf = (name: string) => name.slice(name.lastIndexOf('.')).toLowerCase();
const formatBytes = (bytes: number) =>
  bytes >= 1024 ** 3 ? `${(bytes / 1024 ** 3).toFixed(2)} GB` : `${(bytes / 1024 ** 2).toFixed(1)} MB`;

@Component({
  selector: 'app-upload',
  imports: [Icon],
  template: `
    <div class="mx-auto max-w-6xl pt-2">
      <div class="mb-6 px-1">
        <h1 class="font-display text-3xl font-bold tracking-tight">Upload a video</h1>
        <p class="mt-1 text-muted">We'll create 1080p → 144p versions automatically so it plays well on any connection.</p>
      </div>

      <div class="grid gap-6 lg:grid-cols-[1.15fr_1fr]">
        <!-- Left: media -->
        <section class="space-y-6">
          <div class="panel p-5">
            <h2 class="mb-3 font-display text-lg font-semibold">Video</h2>

            @if (videoFile(); as file) {
              <video [src]="videoPreview()" controls class="aspect-video w-full rounded-2xl bg-black"></video>
              <div class="mt-3 flex items-center justify-between gap-3">
                <div class="min-w-0">
                  <p class="truncate text-sm font-semibold">{{ file.name }}</p>
                  <p class="text-xs text-muted">{{ size(file.size) }}</p>
                </div>
                <button (click)="videoInput.click()" [disabled]="uploading()" class="btn-soft">Change</button>
              </div>
            } @else {
              <button
                (click)="videoInput.click()"
                (dragover)="onDragOver($event)"
                (dragleave)="dragging.set(false)"
                (drop)="onDrop($event)"
                class="flex aspect-video w-full flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed px-6 text-center transition"
                [class]="dragging() ? 'border-accent bg-accent/10' : 'border-line hover:border-accent/60 hover:bg-fg/5'"
              >
                <span class="brand-gradient flex h-16 w-16 items-center justify-center rounded-2xl text-white shadow-lg shadow-accent/30">
                  <app-icon name="cloudUpload" [size]="32" />
                </span>
                <span class="font-semibold">Drag and drop a video, or <span class="text-accent">browse</span></span>
                <span class="text-xs text-muted">MP4, MOV, MKV, WebM or AVI · up to 2 GB</span>
              </button>
            }
            <input #videoInput type="file" accept="video/*,.mkv" class="hidden" (change)="onVideoPicked($event)" />
          </div>

          <div class="panel p-5">
            <div class="mb-3 flex items-baseline justify-between">
              <h2 class="font-display text-lg font-semibold">Thumbnail</h2>
              <span class="text-xs text-muted">Optional</span>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <button
                (click)="clearThumbnail()"
                [disabled]="uploading()"
                class="flex aspect-video flex-col items-center justify-center gap-2 rounded-2xl border-2 p-3 text-center text-sm transition"
                [class]="!thumbnailFile() ? 'border-accent bg-accent/10' : 'border-line hover:border-accent/50'"
              >
                <app-icon name="sparkle" [size]="26" class="text-accent" />
                <span class="font-semibold">Auto-generate</span>
                <span class="text-xs text-muted">A frame from your video</span>
              </button>

              @if (thumbnailPreview(); as preview) {
                <button (click)="thumbInput.click()" [disabled]="uploading()" class="group relative aspect-video overflow-hidden rounded-2xl border-2 border-accent">
                  <img [src]="preview" alt="Thumbnail preview" class="h-full w-full object-cover" />
                  <span class="absolute inset-0 flex items-center justify-center bg-black/50 text-sm font-semibold text-white opacity-0 transition group-hover:opacity-100">Change</span>
                </button>
              } @else {
                <button
                  (click)="thumbInput.click()"
                  [disabled]="uploading()"
                  class="flex aspect-video flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-line p-3 text-center text-sm transition hover:border-accent/50"
                >
                  <app-icon name="image" [size]="26" class="text-muted" />
                  <span class="font-semibold">Upload image</span>
                  <span class="text-xs text-muted">JPG, PNG or WebP · 16:9</span>
                </button>
              }
            </div>
            <input #thumbInput type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="onThumbnailPicked($event)" />
          </div>
        </section>

        <!-- Right: details -->
        <section class="panel h-fit space-y-5 p-5">
          <h2 class="font-display text-lg font-semibold">Details</h2>

          <label class="block">
            <span class="mb-1.5 flex justify-between text-sm font-medium">
              Title <span class="text-xs font-normal text-muted">{{ title().length }}/100</span>
            </span>
            <input class="input" maxlength="100" placeholder="Give your video a title" [value]="title()" (input)="title.set(value($event))" [disabled]="uploading()" />
          </label>

          <label class="block">
            <span class="mb-1.5 flex justify-between text-sm font-medium">
              Description <span class="text-xs font-normal text-muted">{{ description().length }}/5000</span>
            </span>
            <textarea class="input min-h-32 resize-y" maxlength="5000" placeholder="Tell viewers about your video"
              [value]="description()" (input)="description.set(value($event))" [disabled]="uploading()"></textarea>
          </label>

          <label class="block">
            <span class="mb-1.5 block text-sm font-medium">Category</span>
            <select class="input" [value]="category()" (change)="category.set(value($event))" [disabled]="uploading()">
              <option value="" disabled>Choose a category</option>
              @for (c of categories(); track c) {
                <option [value]="c">{{ c }}</option>
              }
            </select>
          </label>

          <div>
            <span class="mb-1.5 flex justify-between text-sm font-medium">
              Tags <span class="text-xs font-normal text-muted">{{ tags().length }}/{{ maxTags }}</span>
            </span>
            <div class="input flex min-h-12 flex-wrap items-center gap-1.5 !py-2">
              @for (tag of tags(); track tag) {
                <span class="inline-flex items-center gap-1 rounded-full bg-accent/15 py-0.5 pr-1 pl-2.5 text-xs font-semibold text-accent">
                  #{{ tag }}
                  <button (click)="removeTag(tag)" [disabled]="uploading()" class="rounded-full p-0.5 hover:bg-accent/20" [attr.aria-label]="'Remove ' + tag">
                    <app-icon name="close" [size]="12" />
                  </button>
                </span>
              }
              @if (tags().length < maxTags) {
                <input
                  #tagInput
                  class="min-w-28 flex-1 bg-transparent text-sm outline-none placeholder:text-muted"
                  placeholder="Add a tag, press Enter"
                  (keydown)="onTagKey($event, tagInput)"
                  (blur)="addTag(tagInput)"
                  [disabled]="uploading()"
                />
              }
            </div>
            <p class="mt-1.5 text-xs text-muted">Tags describe what the video is about and help people find it.</p>
          </div>

          @if (error()) {
            <p class="rounded-xl bg-red-500/10 px-3 py-2 text-sm text-red-500">{{ error() }}</p>
          }

          @if (uploading()) {
            <div class="rounded-2xl bg-fg/5 p-4">
              <div class="mb-2 flex justify-between text-sm">
                <span class="font-semibold">{{ progress() < 100 ? 'Uploading…' : 'Finishing up…' }}</span>
                <span class="text-muted">{{ progressText() }}</span>
              </div>
              <div class="h-2 overflow-hidden rounded-full bg-fg/10">
                <div class="brand-gradient h-full rounded-full transition-all duration-300" [style.width.%]="progress()"></div>
              </div>
              <button (click)="cancel()" class="mt-3 text-sm font-semibold text-muted hover:text-fg">Cancel</button>
            </div>
          } @else {
            <button (click)="submit()" [disabled]="!canSubmit()" class="btn-primary w-full !py-3">
              <app-icon name="cloudUpload" [size]="20" /> Upload
            </button>
          }
        </section>
      </div>
    </div>
  `,
})
export class Upload implements OnDestroy {
  private readonly videos = inject(VideoService);
  private readonly router = inject(Router);

  protected readonly maxTags = MAX_TAGS;
  protected readonly size = formatBytes;

  protected readonly videoFile = signal<File | null>(null);
  protected readonly videoPreview = signal<string | null>(null);
  protected readonly thumbnailFile = signal<File | null>(null);
  protected readonly thumbnailPreview = signal<string | null>(null);
  protected readonly dragging = signal(false);

  protected readonly title = signal('');
  protected readonly description = signal('');
  protected readonly category = signal('');
  protected readonly categories = signal<string[]>([]);
  protected readonly tags = signal<string[]>([]);

  protected readonly uploading = signal(false);
  protected readonly progress = signal(0);
  private readonly sentBytes = signal(0);
  protected readonly error = signal<string | null>(null);
  private upload$: Subscription | null = null;

  protected readonly canSubmit = computed(
    () => !!this.videoFile() && this.title().trim().length > 0 && !!this.category() && !this.uploading(),
  );
  protected readonly progressText = computed(() => {
    const total = this.videoFile()?.size ?? 0;
    return `${this.progress()}% · ${formatBytes(this.sentBytes())} of ${formatBytes(total)}`;
  });

  constructor() {
    this.videos.categories().subscribe({ next: (c) => this.categories.set(c), error: () => {} });
  }

  ngOnDestroy(): void {
    this.upload$?.unsubscribe();
    this.revoke(this.videoPreview());
    this.revoke(this.thumbnailPreview());
  }

  protected value(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.setVideo(file);
  }

  onVideoPicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) this.setVideo(input.files[0]);
    input.value = ''; // allow picking the same file again
  }

  onThumbnailPicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    if (!IMAGE_EXTENSIONS.includes(extensionOf(file.name))) return this.error.set('Thumbnails must be JPG, PNG or WebP.');
    if (file.size > MAX_THUMBNAIL_BYTES) return this.error.set('Thumbnails can be at most 5 MB.');

    this.error.set(null);
    this.revoke(this.thumbnailPreview());
    this.thumbnailFile.set(file);
    this.thumbnailPreview.set(URL.createObjectURL(file));
  }

  clearThumbnail(): void {
    this.revoke(this.thumbnailPreview());
    this.thumbnailFile.set(null);
    this.thumbnailPreview.set(null);
  }

  onTagKey(event: KeyboardEvent, input: HTMLInputElement): void {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      this.addTag(input);
    } else if (event.key === 'Backspace' && !input.value) {
      this.tags.update((t) => t.slice(0, -1));
    }
  }

  /**
   * Reads and clears the input element directly. With a [value]="signal()" binding, typing and
   * Enter in the same frame can go '' → 'abc' → '' before Angular renders, so the DOM never clears.
   */
  addTag(input: HTMLInputElement): void {
    const tag = input.value.trim().replace(/^#+/, '').toLowerCase().slice(0, MAX_TAG_LENGTH);
    input.value = '';
    if (!tag || this.tags().includes(tag) || this.tags().length >= MAX_TAGS) return;
    this.tags.update((t) => [...t, tag]);
  }

  removeTag(tag: string): void {
    this.tags.update((t) => t.filter((x) => x !== tag));
  }

  submit(): void {
    const video = this.videoFile();
    if (!video || !this.canSubmit()) return;

    this.error.set(null);
    this.uploading.set(true);
    this.progress.set(0);

    this.upload$ = this.videos
      .upload({
        video,
        thumbnail: this.thumbnailFile(),
        title: this.title().trim(),
        description: this.description().trim(),
        category: this.category(),
        tags: this.tags(),
      })
      .subscribe({
        next: (event) => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            this.sentBytes.set(event.loaded);
            this.progress.set(Math.round((100 * event.loaded) / event.total));
          } else if (event.type === HttpEventType.Response) {
            // Processing continues in the background; the watch page shows live progress.
            this.router.navigate(['/watch', event.body!.data!.id]);
          }
        },
        error: (err) => {
          this.uploading.set(false);
          this.error.set(errorMessage(err));
        },
      });
  }

  cancel(): void {
    this.upload$?.unsubscribe(); // aborts the HTTP request
    this.upload$ = null;
    this.uploading.set(false);
    this.progress.set(0);
  }

  private setVideo(file: File): void {
    if (!VIDEO_EXTENSIONS.includes(extensionOf(file.name)))
      return this.error.set(`Supported video types: ${VIDEO_EXTENSIONS.join(', ')}.`);
    if (file.size > MAX_VIDEO_BYTES) return this.error.set('Videos can be at most 2 GB.');

    this.error.set(null);
    this.revoke(this.videoPreview());
    this.videoFile.set(file);
    this.videoPreview.set(URL.createObjectURL(file));
    // Prefill the title from the file name, like most upload flows do.
    if (!this.title()) this.title.set(file.name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ').slice(0, 100));
  }

  private revoke(url: string | null): void {
    if (url) URL.revokeObjectURL(url);
  }
}
