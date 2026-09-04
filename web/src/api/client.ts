// -----------------------------------------------------------------------------
// File        : api/client.ts
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : The single point through which this application talks to the
//               Web API. It attaches the access token, parses the ProblemDetails
//               error format, and surfaces the server's own message so that no
//               business rule ever has to be restated in the user interface.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

// Read once at module load. Vite substitutes this at build time.
const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080/api/v1'

// Key under which the session is kept for the lifetime of the browser tab.
const TOKEN_KEY = 'voltshare.token'

/**
 * An error raised by the API.
 *
 * The code is the stable machine readable identifier the server sends, such as
 * RESERVATION_OUTSIDE_7_DAYS. Screens switch on the code when they need to
 * react to a specific rule, and display the message for everything else.
 */
export class ApiError extends Error {
  public readonly status: number
  public readonly code: string

  constructor(status: number, code: string, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }

  /** True when the caller is not signed in, or the token has expired. */
  get isUnauthorised(): boolean {
    return this.status === 401
  }

  /** True when the caller is signed in but not allowed to do this. */
  get isForbidden(): boolean {
    return this.status === 403
  }
}

/** Stores the access token for the current browser tab. */
export function setToken(token: string | null): void {
  // Wrapped because storage access throws outright in some privacy modes, and
  // losing the session is far better than crashing the whole application.
  try {
    if (token === null) {
      sessionStorage.removeItem(TOKEN_KEY)
    } else {
      sessionStorage.setItem(TOKEN_KEY, token)
    }
  } catch {
    // Ignored on purpose: the app still works, the session just will not
    // survive a page refresh.
  }
}

/** Reads the access token for the current browser tab. */
export function getToken(): string | null {
  try {
    return sessionStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

/** Shape of the ProblemDetails body the API returns for a failed request. */
interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errorCode?: string
}

/**
 * Turns a failed response into an ApiError carrying the server's own wording.
 */
async function toApiError(response: Response): Promise<ApiError> {
  let code = 'UNKNOWN'
  let message = `Request failed with status ${response.status}.`

  // A failed request does not always carry a JSON body; a 401 from the
  // authentication middleware, for example, has none at all.
  try {
    const problem = (await response.json()) as ProblemDetails

    if (problem.errorCode) code = problem.errorCode
    else if (problem.title) code = problem.title

    if (problem.detail) message = problem.detail
  } catch {
    if (response.status === 401) {
      message = 'Your session has expired. Please sign in again.'
      code = 'UNAUTHORISED'
    } else if (response.status === 403) {
      message = 'You do not have permission to perform this action.'
      code = 'FORBIDDEN'
    }
  }

  return new ApiError(response.status, code, message)
}

/**
 * Performs a request against the API.
 *
 * Every call in the application goes through here, so the token handling and
 * error translation exist in exactly one place.
 */
async function request<T>(
  path: string,
  options: { method?: string; body?: unknown; signal?: AbortSignal } = {},
): Promise<T> {
  const { method = 'GET', body, signal } = options

  const headers: Record<string, string> = {}
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  const token = getToken()
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    // A network level failure never reaches the server, so there is no
    // ProblemDetails body to read.
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }

    // The browser reports a blocked cross origin request and a service that is
    // genuinely down in exactly the same way, so name both possibilities and
    // include the two addresses involved. Without this the message is the same
    // whatever the cause, which makes the real problem hard to find.
    throw new ApiError(
      0,
      'NETWORK_ERROR',
      `Could not reach the VoltShare service at ${BASE_URL}. ` +
        `Either the API is not running, or this page's address ` +
        `(${window.location.origin}) is not an allowed origin on the API.`,
    )
  }

  if (!response.ok) {
    const apiError = await toApiError(response)

    // An expired or invalid token is cleared immediately so the application
    // does not keep retrying with a credential that will never work again.
    if (apiError.isUnauthorised) {
      setToken(null)
    }

    throw apiError
  }

  // 204 No Content has an empty body, which JSON.parse would choke on.
  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

/** Verb helpers used by the resource modules. */
export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { signal }),

  post: <T>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body }),

  put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),

  patch: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PATCH', body }),

  del: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}

/**
 * Builds a query string from the filters a screen has set, leaving out any
 * value that is empty so an unused filter never narrows the results.
 */
export function buildQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue
    search.append(key, String(value))
  }

  const query = search.toString()
  return query ? `?${query}` : ''
}
