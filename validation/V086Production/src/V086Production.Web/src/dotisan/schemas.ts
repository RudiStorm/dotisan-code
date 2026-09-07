import { z } from "zod";

export const AntiforgeryResponseSchema = z.object({
  token: z.string(),
});

export const CurrentUserResponseSchema = z.object({
  email: z.string(),
  id: z.string(),
});

export const EmailConfirmationRequestSchema = z.object({
  email: z.string(),
  token: z.string(),
});

export const EmailConfirmationResendRequestSchema = z.object({
  email: z.string(),
});

export const ExternalLoginProviderDescriptorSchema = z.object({
  displayName: z.string(),
  name: z.string(),
});

export const ExternalProvidersResponseSchema = z.object({
  providers: z.array(ExternalLoginProviderDescriptorSchema),
});

export const LoginRequestSchema = z.object({
  email: z.string(),
  password: z.string(),
  rememberMe: z.boolean(),
});

export const LoginResponseSchema = z.object({
  code: z.string(),
});

export const MfaCodeRequestSchema = z.object({
  code: z.string(),
});

export const MfaSetupResponseSchema = z.object({
  authenticatorUri: z.string(),
  sharedKey: z.string(),
});

export const PasswordResetConfirmRequestSchema = z.object({
  email: z.string(),
  newPassword: z.string(),
  token: z.string(),
});

export const PasswordResetRequestSchema = z.object({
  email: z.string(),
});

export const RecoveryCodesResponseSchema = z.object({
  recoveryCodes: z.array(z.string()),
});

export const RegisterRequestSchema = z.object({
  email: z.string(),
  password: z.string(),
});

export const SessionInfoSchema = z.object({
  createdAt: z.string(),
  deviceName: z.string(),
  expiresAt: z.string(),
  id: z.string(),
  ipAddress: z.string().nullable(),
  lastSeenAt: z.string(),
  userAgent: z.string().nullable(),
});

export const SessionsResponseSchema = z.object({
  sessions: z.array(SessionInfoSchema),
});

