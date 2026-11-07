import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NotificationService } from '../core/notifications/notification.service';
import { mediaUrl } from '../core/videos/video.service';
import { ChannelAvatar } from '../shared/channel-avatar';
import { Icon } from '../shared/icon';

/** A live notification pops up bottom-right for a few seconds (it's also in the bell). */
@Component({
  selector: 'app-notification-toast',
  imports: [ChannelAvatar, Icon],
  template: `
    @if (notifications.toast(); as n) {
      <div class="toast-in glass-strong fixed right-4 bottom-4 left-4 z-[60] flex items-center gap-3 rounded-2xl p-3 pr-2 sm:left-auto sm:w-[380px]" role="status" aria-live="polite">
        <button (click)="open()" class="flex min-w-0 flex-1 items-center gap-3 text-left">
          <app-channel-avatar [name]="n.actorName" [size]="40" />
          <span class="min-w-0 flex-1">
            <span class="line-clamp-2 text-sm font-semibold leading-snug">{{ n.text }}</span>
            @if (n.snippet) {
              <span class="mt-0.5 block truncate text-xs text-muted">{{ n.snippet }}</span>
            }
          </span>
          @if (image(); as src) {
            <img [src]="src" alt="" class="h-12 w-20 shrink-0 rounded-lg object-cover" />
          }
        </button>
        <button (click)="notifications.dismissToast()" class="icon-btn !h-8 !w-8 shrink-0 text-muted" aria-label="Dismiss">
          <app-icon name="close" [size]="18" />
        </button>
      </div>
    }
  `,
  styles: `
    .toast-in { animation: toast-in 0.35s cubic-bezier(0.2, 0.9, 0.3, 1.2); }
    @keyframes toast-in { from { opacity: 0; transform: translateY(16px) scale(0.97); } }
    @media (prefers-reduced-motion: reduce) { .toast-in { animation: none; } }
  `,
})
export class NotificationToast {
  protected readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected image(): string | null {
    return mediaUrl(this.notifications.toast()?.imageUrl);
  }

  protected open(): void {
    const n = this.notifications.toast();
    if (!n) return;
    this.notifications.markRead(n);
    this.notifications.dismissToast();
    this.router.navigateByUrl(n.link);
  }
}
