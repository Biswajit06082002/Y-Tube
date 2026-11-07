import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../api/api.models';

export interface Mention {
  userId: string;
  handle: string;
}

export interface Comment {
  id: number;
  videoId: string;
  parentId: number | null;
  authorId: string;
  authorName: string;
  authorHandle: string;
  isByCreator: boolean;
  text: string;
  mentions: Mention[];
  replyCount: number;
  createdAtUtc: string;
  editedAtUtc: string | null;
  canEdit: boolean;
  canDelete: boolean;
}

/** One page with a cursor: pass nextCursor back for the next page (null = no more). */
export interface CommentPage {
  items: Comment[];
  nextCursor: number | null;
  totalCount: number | null;
}

export const MAX_COMMENT_LENGTH = 2000;

const unwrap = <T>(source: Observable<ApiResponse<T>>) => source.pipe(map((res) => res.data as T));

/** Comments and replies (Comment service). Reading is public; writing needs a login. */
@Injectable({ providedIn: 'root' })
export class CommentService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiBaseUrl}/api/comments`;

  forVideo(videoId: string, cursor: number | null = null): Observable<CommentPage> {
    const params: Record<string, number> = cursor === null ? {} : { cursor };
    return unwrap(this.http.get<ApiResponse<CommentPage>>(`${this.api}/video/${videoId}`, { params }));
  }

  replies(commentId: number, cursor: number | null = null): Observable<CommentPage> {
    const params: Record<string, number> = cursor === null ? {} : { cursor };
    return unwrap(this.http.get<ApiResponse<CommentPage>>(`${this.api}/${commentId}/replies`, { params }));
  }

  /** parentId = reply. Replying to a reply is fine; the server keeps it in the same thread. */
  post(videoId: string, text: string, parentId: number | null = null): Observable<Comment> {
    return unwrap(this.http.post<ApiResponse<Comment>>(`${this.api}/video/${videoId}`, { text, parentId }));
  }

  edit(id: number, text: string): Observable<Comment> {
    return unwrap(this.http.put<ApiResponse<Comment>>(`${this.api}/${id}`, { text }));
  }

  delete(id: number): Observable<unknown> {
    return this.http.delete(`${this.api}/${id}`);
  }
}
