import { HttpErrorResponse } from '@angular/common/http';

export function apiErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'The TestShield API is unavailable. Confirm the API is running and try again.';
    }

    const detail = problemDetail(error.error);
    if (error.status === 404) {
      return detail || 'The requested project or specification was not found.';
    }

    if (error.status === 400) {
      return detail || 'The request was invalid. Check the input and try again.';
    }

    if (error.status >= 500) {
      return 'The TestShield API encountered an error. Try again.';
    }

    return detail || `Request failed (${error.status}).`;
  }

  return 'An unexpected error occurred.';
}

function problemDetail(body: unknown): string | null {
  if (!body || typeof body !== 'object') {
    return typeof body === 'string' && body.trim() ? body.trim() : null;
  }

  const problem = body as { title?: unknown; detail?: unknown };
  const detail = typeof problem.detail === 'string' ? problem.detail.trim() : '';
  const title = typeof problem.title === 'string' ? problem.title.trim() : '';
  return detail || title || null;
}
