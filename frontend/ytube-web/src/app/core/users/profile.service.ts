import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../api/api.models';
import { AuthService } from '../auth/auth.service';

export interface Profile {
  userId: string;
  handle: string;
  displayName: string;
  bio: string;
  joinedAtUtc: string;
  followerCount: number;
  followingCount: number;
  isFollowedByMe: boolean;
  isMe: boolean;
}

export interface ProfileSummary {
  userId: string;
  handle: string;
  displayName: string;
}

export interface UpdateProfileInput {
  displayName?: string;
  handle?: string;
  bio?: string;
}

const unwrap = <T>(source: Observable<ApiResponse<T>>) => source.pipe(map((res) => res.data as T));

/** Channel profiles and following (User service). */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly api = `${environment.apiBaseUrl}/api/users`;

  /** The signed-in user's own profile (name and @handle for the account menu). */
  readonly me = signal<Profile | null>(null);
  readonly myId = computed(() => this.auth.user()?.id ?? null);

  constructor() {
    // Load (and lazily create) my profile whenever a user signs in; clear it on sign-out.
    effect(() => {
      const id = this.myId();
      untracked(() => {
        if (!id) return this.me.set(null);
        this.http.get<ApiResponse<Profile>>(`${this.api}/me`).subscribe({
          next: (res) => this.me.set(res.data),
          error: () => {},
        });
      });
    });
  }

  profile(userId: string): Observable<Profile> {
    return unwrap(this.http.get<ApiResponse<Profile>>(`${this.api}/${userId}/profile`));
  }

  updateMe(input: UpdateProfileInput): Observable<Profile> {
    return unwrap(this.http.put<ApiResponse<Profile>>(`${this.api}/me/profile`, input)).pipe(tap((p) => this.me.set(p)));
  }

  /** Follow or unfollow; returns the followee's updated profile (new follower count). */
  setFollow(userId: string, follow: boolean): Observable<Profile> {
    const url = `${this.api}/${userId}/follow`;
    return unwrap(follow ? this.http.put<ApiResponse<Profile>>(url, null) : this.http.delete<ApiResponse<Profile>>(url)).pipe(
      tap(() => this.refreshMe()),
    );
  }

  /** @mention autocomplete: users whose @handle or name starts with the query. */
  search(query: string): Observable<ProfileSummary[]> {
    return unwrap(this.http.get<ApiResponse<ProfileSummary[]>>(`${this.api}/search`, { params: { q: query } }));
  }

  following(): Observable<Profile[]> {
    return unwrap(this.http.get<ApiResponse<Profile[]>>(`${this.api}/me/following`));
  }

  private refreshMe(): void {
    this.http.get<ApiResponse<Profile>>(`${this.api}/me`).subscribe({ next: (res) => this.me.set(res.data), error: () => {} });
  }
}
