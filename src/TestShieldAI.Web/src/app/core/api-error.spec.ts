import { HttpErrorResponse } from '@angular/common/http';
import { apiErrorMessage } from './api-error';

describe('apiErrorMessage', () => {
  it('describes an unavailable API', () => {
    expect(apiErrorMessage(new HttpErrorResponse({ status: 0 }))).toContain('unavailable');
  });

  it('uses a friendly 400 message', () => {
    expect(
      apiErrorMessage(
        new HttpErrorResponse({
          status: 400,
          error: { title: 'Invalid OpenAPI specification', detail: 'Missing paths.' }
        })
      )
    ).toBe('Missing paths.');
  });

  it('uses a friendly 404 message', () => {
    expect(
      apiErrorMessage(
        new HttpErrorResponse({
          status: 404,
          error: { title: 'Project not found', detail: "No project exists with id 'abc'." }
        })
      )
    ).toBe("No project exists with id 'abc'.");
  });

  it('hides 500 details from the user', () => {
    expect(
      apiErrorMessage(
        new HttpErrorResponse({
          status: 500,
          error: { detail: 'System.Exception: stack trace here' }
        })
      )
    ).toBe('The TestShield API encountered an error. Try again.');
  });

  it('handles an empty 400 body', () => {
    expect(apiErrorMessage(new HttpErrorResponse({ status: 400, error: null }))).toBe(
      'The request was invalid. Check the input and try again.'
    );
  });

  it('handles a malformed body', () => {
    expect(apiErrorMessage(new HttpErrorResponse({ status: 404, error: {} }))).toBe(
      'The requested project or specification was not found.'
    );
  });
});
