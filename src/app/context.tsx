import { useNavigate } from "react-router-dom";
import { auth } from "../services/auth";
import { accountService } from "../services/frontend";
import { verified } from "../services/contracts";
import { ServiceError } from "../services/contracts";
import {
  createContext,
  useContext,
  useCallback,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  guestAccount,
  type Account,
  type Language,
} from "../services/contracts";

interface AppContext {
  language: Language;
  t: (arabic: string, english: string) => string;
  changeLanguage: () => void;
  account: Account;
  updateAccount: (account: Account) => void;
  expireSession: () => void;
  sessionExpired: boolean;
  sessionUnavailable: boolean;
  hasUnreadNotifications: boolean;
}
const Context = createContext<AppContext | null>(null);
export function AppProvider({ children }: { children: ReactNode }) {
  const [language, setLanguage] = useState<Language>(() =>
    localStorage.getItem("smart-campus-language") === "en" ? "en" : "ar",
  );
  const [account, setAccount] = useState(guestAccount);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [sessionUnavailable, setSessionUnavailable] = useState(false);
  const sessionGeneration = useRef(0);
  const [notificationStatus, setNotificationStatus] = useState({ accountId: "", hasUnread: false });
  const navigate = useNavigate();
  const expireSession = useCallback(() => {
    sessionGeneration.current++;
    setAccount(guestAccount);
    setSessionExpired(true);
    navigate("/login");
  }, [navigate]);
  useEffect(() => {
    let active = true;
    const generation = sessionGeneration.current;
    auth.me().then(current => {
      if (active && sessionGeneration.current === generation) {
        setAccount(current);
        setSessionUnavailable(false);
      }
    }).catch(error => {
      if (active && sessionGeneration.current === generation && error instanceof ServiceError && (error.code === "internal" || error.code === "unavailable"))
        setSessionUnavailable(true);
      if (!(error instanceof ServiceError)) console.error("Session restoration failed");
    });
    return () => { active = false; };
  }, []);
  useEffect(() => {
    window.addEventListener("smart-campus-session-expired", expireSession);
    return () => window.removeEventListener("smart-campus-session-expired", expireSession);
  }, [expireSession]);
  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = language === "ar" ? "rtl" : "ltr";
  }, [language]);
  useEffect(() => {
    if (!verified(account)) return;
    let active = true;
    let request = 0;
    const generation = sessionGeneration.current;
    async function refresh() {
      if (document.visibilityState === "hidden") return;
      const currentRequest = ++request;
      try {
        const hasUnread = await accountService.hasUnreadNotifications(account);
        if (active && currentRequest === request && generation === sessionGeneration.current)
          setNotificationStatus({ accountId: account.id, hasUnread });
      } catch (error) {
        if (!(error instanceof ServiceError)) console.error("Notification status refresh failed");
      }
    }
    void refresh();
    const timer = window.setInterval(() => { void refresh(); }, 30_000);
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", refresh);
    window.addEventListener("smart-campus-notifications-changed", refresh);
    return () => {
      active = false;
      window.clearInterval(timer);
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", refresh);
      window.removeEventListener("smart-campus-notifications-changed", refresh);
    };
  }, [account]);
  function changeLanguage() {
    const next = language === "ar" ? "en" : "ar";
    setLanguage(next);
    localStorage.setItem("smart-campus-language", next);
  }
  return (
    <Context
      value={{
        language,
        t: (ar, en) => (language === "ar" ? ar : en),
        changeLanguage,
        account,
        updateAccount: (nextAccount) => {
          sessionGeneration.current++;
          setAccount(nextAccount);
          setSessionExpired(false);
          setSessionUnavailable(false);
        },
        sessionExpired,
        sessionUnavailable,
        hasUnreadNotifications: verified(account) && notificationStatus.accountId === account.id && notificationStatus.hasUnread,
        expireSession,
      }}
    >
      {children}
    </Context>
  );
}
export function useApp() {
  const value = useContext(Context);
  if (!value) throw new Error("AppProvider required");
  return value;
}
