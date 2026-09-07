export interface AntiforgeryResponse {
  token: string;
}

export interface CurrentUserResponse {
  email: string;
  id: string;
}

export interface EmailConfirmationRequest {
  email: string;
  token: string;
}

export interface EmailConfirmationResendRequest {
  email: string;
}

export interface ExternalLoginProviderDescriptor {
  displayName: string;
  name: string;
}

export interface ExternalProvidersResponse {
  providers: ExternalLoginProviderDescriptor[];
}

export interface LoginRequest {
  email: string;
  password: string;
  rememberMe: boolean;
}

export interface LoginResponse {
  code: string;
}

export interface MfaCodeRequest {
  code: string;
}

export interface MfaSetupResponse {
  authenticatorUri: string;
  sharedKey: string;
}

export interface PasswordResetConfirmRequest {
  email: string;
  newPassword: string;
  token: string;
}

export interface PasswordResetRequest {
  email: string;
}

export interface RecoveryCodesResponse {
  recoveryCodes: string[];
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface SessionInfo {
  createdAt: string;
  deviceName: string;
  expiresAt: string;
  id: string;
  ipAddress: string | null;
  lastSeenAt: string;
  userAgent: string | null;
}

export interface SessionsResponse {
  sessions: SessionInfo[];
}
