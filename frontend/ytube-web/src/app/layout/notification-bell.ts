import { Component, ElementRef, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AppNotification, NotificationService } from '../core/notifications/notification.service';
import { mediaUrl } from '../core/videos/video.service';
import { ChannelAvatar } from '../shared/channel-avatar';
import { timeAgo } from '../shared/format';
import { Icon } from '../shared/icon';

/** Bell in the top bar: unread badge, and a panel listing notifications (newest first). */
@Component({
  selector: 'app-notification-bell',
  imports: [Icon, ChannelAvatar],
  template: `
    <!-- Not "relative": the panel anchors to the whole header pill, so it lines up with its right edge. -->
    <div>
      <button
        (click)="toggle()"
        class="icon-btn relative"
        [attr.aria-label]="notifications.unread() ? 'Notifications, ' + notifications.unread() + ' unread' : 'Notifications'"
        [attr.aria-expanded]="open()"
        title="Notifications"
      >
        <app-icon name="bell" />
        @if (notifications.unread() > 0) {
          <span class="brand-gradient absolute top-1 right-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full px-1 text-[10px] leading-none font-bold text-white shadow-md shadow-accent/40 ring-2 ring-[var(--bg)]">
            {{ notifications.unread() > 99 ? '99+' : notifications.unread() }}
          </span>
        }
      </button>

      @if (open()) {
        <!-- Positioned against the header pill (its backdrop-filter makes it the containing block),
             which ends 1rem from the screen edge — so 100vw - 2rem fills a phone screen edge to edge. -->
        <div class="glass-strong notif-panel absolute right-0 z-50 mt-2 flex max-h-[min(640px,calc(100vh-96px))] w-[calc(100vw-2rem)] flex-col overflow-hidden rounded-3xl sm:w-[400px]" role="dialog" aria-label="Notifications">
          <div class="flex items-center justify-between gap-2 border-b border-line/60 px-5 py-3.5">
            <div class="flex items-center gap-2">
              <h2 class="font-display text-base font-semibold">Notifications</h2>
              <span class="h-2 w-2 rounded-full" [class]="notifications.live() ? 'bg-emerald-400' : 'bg-muted/50'" [title]="notifications.live() ? 'Live: new notifications appear instantly' : 'Reconnecting…'"></span>
            </div>
            @if (notifications.unread() > 0) {
              <button (click)="notifications.markAllRead()" class="inline-flex items-center gap-1.5 rounded-lg px-2 py-1 text-xs font-semibold text-accent transition hover:bg-accent/10">
                <app-icon name="doneAll" [size]="16" /> Mark all as read
              </button>
            }
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto p-2" (scroll)="onScroll($event)">
            @for (n of notifications.items(); track n.id) {
              <button
                (click)="openNotification(n)"
                class="group flex w-full gap-3 rounded-2xl p-3 text-left transition hover:bg-fg/6"
                [class.bg-accent/8]="!n.isRead"
              >
                <app-channel-avatar [name]="n.actorName" [size]="40" />
                <div class="min-w-0 flex-1">
                  <p class="text-sm leading-snug" [class.font-semibold]="!n.isRead" [class.text-muted]="n.isRead">{{ n.text }}</p>
                  @if (n.snippet) {
                    <p class="mt-0.5 line-clamp-2 text-xs text-muted">{{ n.snippet }}</p>
                  }
                  <p class="mt-1 flex items-center gap-1.5 text-xs" [class.text-accent]="!n.isRead" [class.text-muted]="n.isRead">
                    <app-icon [name]="iconFor(n)" [size]="13" /> {{ ago(n.createdAtUtc) }}
                  </p>
                </div>
                @if (image(n); as src) {
                  <img [src]="src" alt="" class="h-14 w-24 shrink-0 rounded-xl object-cover" loading="lazy" />
                }
                @if (!n.isRead) {
                  <span class="mt-2 h-2 w-2 shrink-0 rounded-full bg-accent" aria-label="Unread"></span>
                }
              </button>
            } @empty {
              @if (!notifications.loading()) {
                <div class="flex flex-col items-center px-6 py-12 text-center">
                  <span class="flex h-14 w-14 items-center justify-center rounded-2xl bg-fg/6 text-muted">
                    <app-icon name="bell" [size]="28" />
                  </span>
                  <p class="mt-4 font-semibold">You're all caught up</p>
                  <p class="mt-1 text-sm text-muted">New videos from creators you follow, replies and @mentions show up here.</p>
                </div>
              }
            }
            @if (notifications.loading()) {
              <p class="py-4 text-center text-xs text-muted">Loading…</p>
            }
          </div>
        </div>
      }
    </div>
  `,
  host: { '(document:click)': 'closeOnOutsideClick($event)' },
  styles: `
    /* A list of text over a busy page needs more body than the usual glass: keep the sheen, thicken the tint. */
    .notif-panel {
      background:
        linear-gradient(150deg, var(--glass-sheen-1), var(--glass-sheen-2) 55%),
        color-mix(in oklab, var(--surface) 94%, transparent);
    }
  `,
})
export class NotificationBell {
  protected readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly open = signal(false);
  protected readonly ago = timeAgo;

  /** Any click outside the bell and its panel closes it. */
  protected closeOnOutsideClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.open.set(false);
  }

  protected toggle(): void {
    this.open.update((o) => !o);
  }

  protected openNotification(n: AppNotification): void {
    this.notifications.markRead(n);
    this.open.set(false);
    this.router.navigateByUrl(n.link);
  }

  /** Near the bottom of the list → load older ones. */
  protected onScroll(event: Event): void {
    const el = event.target as HTMLElement;
    if (el.scrollTop + el.clientHeight > el.scrollHeight - 120) this.notifications.loadMore();
  }

  protected image(n: AppNotification): string | null {
    return mediaUrl(n.imageUrl);
  }

  protected iconFor(n: AppNotification) {
    switch (n.type) {
      case 'NewVideo':
      case 'VideoLive':
        return 'play' as const;
      case 'NewFollower':
        return 'account' as const;
      case 'Reply':
        return 'reply' as const;
      default:
        return 'comment' as const;
    }
  }
}
