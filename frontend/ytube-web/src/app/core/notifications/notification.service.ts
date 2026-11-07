import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../api/api.models';
import { AuthService } from '../auth/auth.service';

export type NotificationType = 'NewVideo' | 'VideoLive' | 'Mention' | 'Reply' | 'VideoComment' | 'NewFollower';

export interface AppNotification {
  id: number;
  type: NotificationType;
  actorId: string | null;
  actorName: string;
  text: string;
  snippet: string | null;
  link: string;
  imageUrl: string | null;
  isRead: boolean;
  createdAtUtc: string;
}

interface NotificationPage {
  items: AppNotification[];
  nextCursor: number | null;
  unreadCount: number | null;
}

/**
 * The bell's data. On sign-in: load the latest page over HTTP (what happened while you were away),
 * then open a SignalR connection so new notifications arrive instantly. After a dropped connection
 * reconnects, reload the first page — anything sent while we were offline is in the database.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly api = `${environment.apiBaseUrl}/api/notifications`;

  readonly items = signal<AppNotification[]>([]);
  readonly unread = signal(0);
  readonly nextCursor = signal<number | null>(null);
  readonly loading = signal(false);
  readonly live = signal(false);
  /** The most recent live notification, shown briefly as a toast. */
  readonly toast = signal<AppNotification | null>(null);

  private connection: HubConnection | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // Connect for the signed-in user; disconnect and forget everything on sign-out (or user switch).
    effect(() => {
      const userId = this.auth.user()?.id ?? null;
      untracked(() => {
        void this.disconnect();
        this.items.set([]);
        this.unread.set(0);
        this.nextCursor.set(null);
        if (userId) {
          this.reload();
          void this.connect();
        }
      });
    });
  }

  /** First page + unread count (also used to catch up after a reconnect). */
  reload(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<NotificationPage>>(this.api).subscribe({
      next: (res) => {
        const page = res.data!;
        this.items.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.unread.set(page.unreadCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  loadMore(): void {
    const cursor = this.nextCursor();
    if (cursor === null || this.loading()) return;
    this.loading.set(true);
    this.http.get<ApiResponse<NotificationPage>>(this.api, { params: { cursor } }).subscribe({
      next: (res) => {
        const known = new Set(this.items().map((n) => n.id));
        this.items.update((list) => [...list, ...res.data!.items.filter((n) => !known.has(n.id))]);
        this.nextCursor.set(res.data!.nextCursor);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  markRead(n: AppNotification): void {
    if (n.isRead) return;
    // Optimistic: update the UI now; the server call just records it.
    this.items.update((list) => list.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
    this.unread.update((c) => Math.max(0, c - 1));
    this.http.post(`${this.api}/${n.id}/read`, null).subscribe({ error: () => {} });
  }

  markAllRead(): void {
    this.items.update((list) => list.map((x) => ({ ...x, isRead: true })));
    this.unread.set(0);
    this.http.post(`${this.api}/read-all`, null).subscribe({ error: () => {} });
  }

  dismissToast(): void {
    clearTimeout(this.toastTimer);
    this.toast.set(null);
  }

  /** @param isRetry true after a failed or closed connection: catch up over HTTP once connected again. */
  private async connect(isRetry = false): Promise<void> {
    const connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/notifications`, {
        // Asked again on every (re)connect, so a reconnect after 20 minutes uses a fresh token.
        accessTokenFactory: () => this.freshToken(),
      })
      .withAutomaticReconnect() // retries after 0, 2, 10 and 30 seconds
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('notification', (n: AppNotification) => this.receive(n));
    connection.onreconnecting(() => this.live.set(false));
    connection.onreconnected(() => {
      this.live.set(true);
      this.reload(); // catch up on anything sent while we were disconnected
    });
    // Automatic reconnect gives up after ~40 s. A longer outage closes the connection: keep trying slowly.
    connection.onclose(() => {
      this.live.set(false);
      this.retryLater(connection);
    });
    this.connection = connection;

    try {
      await connection.start();
      if (this.connection !== connection) return;
      this.live.set(true);
      if (isRetry) this.reload();
    } catch {
      // Gateway or Notification service down: the bell still works from HTTP; try again shortly.
      this.retryLater(connection);
    }
  }

  private retryLater(connection: HubConnection): void {
    setTimeout(() => {
      // Only if this is still the current connection (not signed out or replaced meanwhile).
      if (this.connection === connection && connection.state === HubConnectionState.Disconnected) void this.connect(true);
    }, 15_000);
  }

  private async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.live.set(false);
    if (connection && connection.state !== HubConnectionState.Disconnected) await connection.stop();
  }

  private async freshToken(): Promise<string> {
    if (this.auth.accessTokenExpiringSoon()) {
      try {
        await firstValueFrom(this.auth.refresh());
      } catch {
        /* the auth service signs out if the refresh token is gone */
      }
    }
    return this.auth.accessToken ?? '';
  }

  private receive(n: AppNotification): void {
    if (this.items().some((x) => x.id === n.id)) return; // already loaded via HTTP
    this.items.update((list) => [n, ...list]);
    this.unread.update((c) => c + 1);

    clearTimeout(this.toastTimer);
    this.toast.set(n);
    this.toastTimer = setTimeout(() => this.toast.set(null), 6000);
  }
}
