import { useNavigate } from "react-router-dom";
import { auth } from "../services/auth";
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
}
const Context = createContext<AppContext | null>(null);
export function AppProvider({ children }: { children: ReactNode }) {
  const [language, setLanguage] = useState<Language>(() =>
    localStorage.getItem("smart-campus-language") === "en" ? "en" : "ar",
  );
  const [account, setAccount] = useState(guestAccount);
  const [sessionExpired, setSessionExpired] = useState(false);
  const sessionGeneration = useRef(0);
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
    auth.me().then(current => { if (active && sessionGeneration.current === generation) setAccount(current); }).catch(error => {
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
        },
        sessionExpired,
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
