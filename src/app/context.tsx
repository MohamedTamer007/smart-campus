import { useNavigate } from "react-router-dom";
import {
  createContext,
  useContext,
  useCallback,
  useEffect,
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
  const navigate = useNavigate();
  const expireSession = useCallback(() => {
    setAccount(guestAccount);
    setSessionExpired(true);
    navigate("/login");
  }, [navigate]);
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
