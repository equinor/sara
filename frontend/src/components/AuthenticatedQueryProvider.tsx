import { useEffect, useState, type ReactNode } from "react";
import { useAccount, useMsal } from "@azure/msal-react";
import { QueryClientProvider } from "@tanstack/react-query";
import { createQueryClient } from "../api/queries";

function QuerySession({ children }: { children: ReactNode }) {
  const [client] = useState(createQueryClient);

  useEffect(() => () => client.clear(), [client]);

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

export default function AuthenticatedQueryProvider({ children }: { children: ReactNode }) {
  const { accounts } = useMsal();
  const activeAccount = useAccount();
  const account = activeAccount ?? accounts[0];
  if (!account) return null;

  // Each account gets a separate in-memory cache; logout unmounts this provider.
  const sessionKey = JSON.stringify([
    account.environment,
    account.homeAccountId,
    account.tenantId,
    account.localAccountId,
  ]);
  return <QuerySession key={sessionKey}>{children}</QuerySession>;
}
