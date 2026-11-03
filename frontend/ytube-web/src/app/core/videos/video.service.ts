import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../api/api.models';

export type VideoStatus = 'Queued' | 'Processing' | 'Ready' | 'Failed';

export interface Video {
  id: string;
  title: string;
  description: string;
  category: string;
  tags: string[];
  /** Creator's user id; null for seeded sample videos (no channel page). */
  ownerId: string | null;
  channelId: string;
  channelName: string;
  views: number;
  durationSeconds: number;
  publishedAtUtc: string;
  likeCount: number;
  likedByMe: boolean;
  isOwner?: boolean;
  status: VideoStatus;
  processingProgress?: number;
  processingError?: string | null;
  /** Relative to the gateway ("/media/videos/…"); null for sample videos without media. */
  thumbnailUrl: string | null;
  /** HLS master playlist; null until processing finished. */
  streamUrl: string | null;
  qualities: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
}

export interface ChannelStats {
  videoCount: number;
  totalViews: number;
  totalLikes: number;
}

export interface UploadVideoInput {
  video: File;
  thumbnail: File | null;
  title: string;
  description: string;
  category: string;
  tags: string[];
}

/** Media paths from the API are relative to the gateway; turn them into absolute URLs. */
export function mediaUrl(path: string | null | undefined): string | null {
  return path ? `${environment.apiBaseUrl}${path}` : null;
}

const unwrap = <T>(source: Observable<ApiResponse<T>>) => source.pipe(map((res) => res.data as T));

@Injectable({ providedIn: 'root' })
export class VideoService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  // --- Public (works logged out) ---

  feed(page: number, pageSize: number, category: string | null): Observable<PagedResult<Video>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (category) params = params.set('category', category);
    return unwrap(this.http.get<ApiResponse<PagedResult<Video>>>(`${this.api}/api/feed`, { params }));
  }

  categories(): Observable<string[]> {
    return unwrap(this.http.get<ApiResponse<string[]>>(`${this.api}/api/feed/categories`));
  }

  video(id: string): Observable<Video> {
    return unwrap(this.http.get<ApiResponse<Video>>(`${this.api}/api/videos/${id}`));
  }

  channelVideos(ownerId: string, page: number, pageSize: number): Observable<PagedResult<Video>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return unwrap(this.http.get<ApiResponse<PagedResult<Video>>>(`${this.api}/api/videos/channel/${ownerId}`, { params }));
  }

  channelStats(ownerId: string): Observable<ChannelStats> {
    return unwrap(this.http.get<ApiResponse<ChannelStats>>(`${this.api}/api/videos/channel/${ownerId}/stats`));
  }

  /** Reports a qualified view (after a few seconds of playback). Works for guests too. */
  recordView(id: string): Observable<{ counted: boolean }> {
    return unwrap(this.http.post<ApiResponse<{ counted: boolean }>>(`${this.api}/api/videos/${id}/views`, null));
  }

  // --- Login required (the gateway returns 401 without a token) ---

  setLike(id: string, liked: boolean): Observable<Video> {
    const url = `${this.api}/api/videos/${id}/like`;
    return unwrap(liked ? this.http.put<ApiResponse<Video>>(url, null) : this.http.delete<ApiResponse<Video>>(url));
  }

  liked(): Observable<Video[]> {
    return unwrap(this.http.get<ApiResponse<Video[]>>(`${this.api}/api/videos/liked`));
  }

  mine(): Observable<Video[]> {
    return unwrap(this.http.get<ApiResponse<Video[]>>(`${this.api}/api/videos/mine`));
  }

  subscriptions(): Observable<PagedResult<Video>> {
    return unwrap(this.http.get<ApiResponse<PagedResult<Video>>>(`${this.api}/api/feed/subscriptions`));
  }

  /**
   * Multipart upload. Emits HttpEvents (not just the result) so the page can show a progress bar:
   * UploadProgress events while bytes go up, then the final Response.
   */
  upload(input: UploadVideoInput): Observable<HttpEvent<ApiResponse<Video>>> {
    const form = new FormData();
    form.append('video', input.video, input.video.name);
    if (input.thumbnail) form.append('thumbnail', input.thumbnail, input.thumbnail.name);
    form.append('title', input.title);
    form.append('description', input.description);
    form.append('category', input.category);
    form.append('tags', input.tags.join(','));

    return this.http.post<ApiResponse<Video>>(`${this.api}/api/videos`, form, {
      reportProgress: true,
      observe: 'events',
    });
  }
}
