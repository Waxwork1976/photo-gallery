/** Authenticated user information stored in client state. */
export interface AuthUser {
  name: string
  email: string
  oid: string
  accessToken: string
}

/** Response from POST /api/auth-validate */
export interface AuthValidateResponse {
  valid: boolean
  user: {
    oid: string
    email: string
    name: string
  }
}
