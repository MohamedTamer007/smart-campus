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
  actors,
  type Account,
  type Language,
  type Role,
  type Scenario,
} from "../services/contracts";
interface AppContext {
  language: Language;
  t: (arabic: string, english: string) => string;
  changeLanguage: () => void;
  account: Account;
  updateAccount: (account: Account) => void;
  setActor: (role: Role) => void;
  actorKey: Role | "registered";
  expireSession: () => void;
  sessionExpired: boolean;
  actionScenario: Scenario;
  setActionScenario: (scenario: Scenario) => void;
  scenario: Scenario;
  setScenario: (scenario: Scenario) => void;
}
const Context = createContext<AppContext | null>(null);
export function AppProvider({ children }: { children: ReactNode }) {
  const [language, setLanguage] = useState<Language>(() =>
    localStorage.getItem("smart-campus-language") === "en" ? "en" : "ar",
  );
  const [account, setAccount] = useState(actors.guest);
  const [actorKey, setActorKey] = useState<Role | "registered">("guest");
  const [sessionExpired, setSessionExpired] = useState(false);
  const navigate = useNavigate();
  const expireSession = useCallback(() => {
    setAccount(actors.guest);
    setActorKey("guest");
    setSessionExpired(true);
    navigate("/login");
  }, [navigate]);
  const [actionScenario, setActionScenario] = useState<Scenario>("normal");
  const [scenario, setScenario] = useState<Scenario>("normal");
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
        updateAccount: (account) => {
          setAccount(account);
          setActorKey(
            (Object.keys(actors) as Role[]).find(
              (key) => actors[key].id === account.id,
            ) ?? "registered",
          );
          setSessionExpired(false);
        },
        sessionExpired,
        expireSession,
        setActor: (role) => {
          setAccount({ ...actors[role] });
          setActorKey(role);
          setSessionExpired(false);
        },
        actorKey,
        actionScenario,
        setActionScenario,
        scenario,
        setScenario,
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
