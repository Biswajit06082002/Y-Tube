import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../api/api.models';

export interface Video {
  id: string;
  title: string;
  description: string;
  category: string;
  channelId: string;
  channelName: string;
  views: number;
  durationSeconds: number;
  publishedAtUtc: string;
  likeCount: number;
  likedByMe: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
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

  // --- Login required (the gateway returns 401 without a token) ---

  setLike(id: string, liked: boolean): Observable<Video> {
    const url = `${this.api}/api/videos/${id}/like`;
    return unwrap(liked ? this.http.put<ApiResponse<Video>>(url, null) : this.http.delete<ApiResponse<Video>>(url));
  }

  liked(): Observable<Video[]> {
    return unwrap(this.http.get<ApiResponse<Video[]>>(`${this.api}/api/videos/liked`));
  }

  subscriptions(): Observable<PagedResult<Video>> {
    return unwrap(this.http.get<ApiResponse<PagedResult<Video>>>(`${this.api}/api/feed/subscriptions`));
  }
}
