import { useMutation, useQuery } from "@tanstack/vue-query";
import * as services from "./services";

export function useIssueAntiforgeryQuery(...args: Parameters<typeof services.issueAntiforgery>) {
  return useQuery({ queryKey: ["account.antiforgery", ...args], queryFn: () => services.issueAntiforgery(...args) });
}

export function useConfirmEmailMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.confirmEmail>[0]) => services.confirmEmail(body) });
}

export function useResendConfirmationMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.resendConfirmation>[0]) => services.resendConfirmation(body) });
}

export function useExternalLoginCallbackQuery(...args: Parameters<typeof services.externalLoginCallback>) {
  return useQuery({ queryKey: ["account.externalCallback", ...args], queryFn: () => services.externalLoginCallback(...args) });
}

export function useExternalLoginChallengeQuery(...args: Parameters<typeof services.externalLoginChallenge>) {
  return useQuery({ queryKey: ["account.externalChallenge", ...args], queryFn: () => services.externalLoginChallenge(...args) });
}

export function useLinkExternalProviderMutation() {
  return useMutation({ mutationFn: (value: Parameters<typeof services.linkExternalProvider>[0]) => services.linkExternalProvider(value) });
}

export function useListExternalProvidersQuery(...args: Parameters<typeof services.listExternalProviders>) {
  return useQuery({ queryKey: ["account.externalProviders", ...args], queryFn: () => services.listExternalProviders(...args) });
}

export function useUnlinkExternalProviderMutation() {
  return useMutation({ mutationFn: (value: Parameters<typeof services.unlinkExternalProvider>[0]) => services.unlinkExternalProvider(value) });
}

export function useLoginMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.login>[0]) => services.login(body) });
}

export function useLogoutMutation() {
  return useMutation({ mutationFn: () => services.logout() });
}

export function useMeQuery(...args: Parameters<typeof services.me>) {
  return useQuery({ queryKey: ["account.me", ...args], queryFn: () => services.me(...args) });
}

export function useChallengeMfaMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.challengeMfa>[0]) => services.challengeMfa(body) });
}

export function useDisableMfaMutation() {
  return useMutation({ mutationFn: () => services.disableMfa() });
}

export function useRegenerateRecoveryCodesMutation() {
  return useMutation({ mutationFn: () => services.regenerateRecoveryCodes() });
}

export function useGetMfaSetupQuery(...args: Parameters<typeof services.getMfaSetup>) {
  return useQuery({ queryKey: ["account.mfaSetup", ...args], queryFn: () => services.getMfaSetup(...args) });
}

export function useVerifyMfaMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.verifyMfa>[0]) => services.verifyMfa(body) });
}

export function useConfirmPasswordResetMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.confirmPasswordReset>[0]) => services.confirmPasswordReset(body) });
}

export function useRequestPasswordResetMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.requestPasswordReset>[0]) => services.requestPasswordReset(body) });
}

export function useRegisterMutation() {
  return useMutation({ mutationFn: (body: Parameters<typeof services.register>[0]) => services.register(body) });
}

export function useRevokeAllSessionsMutation() {
  return useMutation({ mutationFn: () => services.revokeAllSessions() });
}

export function useRevokeSessionMutation() {
  return useMutation({ mutationFn: (value: Parameters<typeof services.revokeSession>[0]) => services.revokeSession(value) });
}

export function useListSessionsQuery(...args: Parameters<typeof services.listSessions>) {
  return useQuery({ queryKey: ["account.sessions", ...args], queryFn: () => services.listSessions(...args) });
}

