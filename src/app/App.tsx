import {
  Requests,
  AdminOverview,
  AdminAccounts,
} from "../features/admin/Admin";
import {
  RegisterFound,
  SearchItems,
  FoundList,
  FoundDetail,
  ClaimsList,
  ClaimDetail,
} from "../features/lost-found/LostFound";
import {
  NewMaintenance,
  MaintenanceList,
  MaintenanceDetail,
} from "../features/maintenance/Maintenance";
import {
  Auth,
  Verification,
  Recovery,
  ResetPassword,
} from "../features/auth/Auth";
import { Settings, Notifications } from "../features/account/Account";
import { Access } from "./access";
import { Link, NavLink, Route, Routes } from "react-router-dom";
import { AppProvider, useApp } from "./context";
import { BrandMark, Icon } from "../components/Icon";
import { Home } from "./Home";
import { Feedback, Page } from "../components/ui";
import { actors, type Role, type Scenario } from "../services/contracts";
function Shell() {
  const {
    t,
    language,
    changeLanguage,
    account,
    sessionExpired,
    setActor,
    actorKey,
    scenario,
    setScenario,
    actionScenario,
    setActionScenario,
  } = useApp();
  const roleNames: Record<Role, string> = {
    guest: t("زائر", "Guest"),
    unverified: t("غير موثق", "Unverified"),
    user: t("مستخدم", "User"),
    other: t("مستخدم إضافي", "Additional user"),
    finder: t("مستخدم آخر", "Another user"),
    secretariat: t("السكرتارية", "Secretariat staff"),
    maintenance: t("موظف صيانة", "Maintenance staff"),
    department: t("أمن", "Security staff"),
    admin: t("مدير", "Admin"),
  };
  return (
    <>
      <a className="skip-link" href="#main">
        {t("انتقل للمحتوى", "Skip to content")}
      </a>
      <div className="demo-bar">
        <details>
          <summary>{t("أدوات التجربة", "Demo controls")}</summary>
          <div className="demo-tools">
            <label>
              {t("محاكاة الحساب", "Simulate account")}
              <select
                value={actorKey}
                onChange={(e) => {
                  if (e.target.value !== "registered")
                    setActor(e.target.value as Role);
                }}
              >
                {actorKey === "registered" && (
                  <option value="registered">
                    {t("حساب تجريبي مسجل", "Registered demo account")}
                  </option>
                )}
                {Object.keys(actors).map((role) => (
                  <option value={role} key={role}>
                    {roleNames[role as Role]}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t("محاكاة الاستجابة", "Simulate response")}
              <select
                value={scenario}
                onChange={(e) => setScenario(e.target.value as Scenario)}
              >
                {(
                  [
                    "normal",
                    "empty",
                    "error",
                    "expired",
                    "conflict",
                  ] as Scenario[]
                ).map((s, i) => (
                  <option value={s} key={s}>
                    {
                      [
                        t("عادية", "Normal"),
                        t("فارغة", "Empty"),
                        t("فشل الخدمة", "Service error"),
                        t("انتهاء الجلسة", "Expired session"),
                        t("تعارض", "Conflict"),
                      ][i]
                    }
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t("محاكاة نتيجة الإجراء", "Simulate action response")}
              <select
                value={actionScenario}
                onChange={(e) => setActionScenario(e.target.value as Scenario)}
              >
                {(["normal", "error", "expired", "conflict"] as Scenario[]).map(
                  (value, index) => (
                    <option key={value} value={value}>
                      {
                        [
                          t("عادية", "Normal"),
                          t("فشل الخدمة", "Service error"),
                          t("انتهاء الجلسة", "Expired session"),
                          t("تعارض", "Conflict"),
                        ][index]
                      }
                    </option>
                  ),
                )}
              </select>
            </label>
          </div>
        </details>
      </div>
      <header className="header">
        <Link className="brand" to="/">
          <BrandMark />
          <bdi>Smart Campus</bdi>
        </Link>
        <nav aria-label={t("التنقل الرئيسي", "Main navigation")}>
          <NavLink to="/" end>
            {t("الرئيسية", "Home")}
          </NavLink>
          <NavLink to="/requests">{t("طلباتي", "My requests")}</NavLink>
          {["maintenance", "department", "secretariat", "admin"].includes(
            account.role,
          ) && <NavLink to="/staff">{t("لوحة العمل", "Work queue")}</NavLink>}
          {account.role === "admin" && (
            <NavLink to="/admin">{t("الإدارة", "Admin")}</NavLink>
          )}
        </nav>
        <div className="header-actions">
          <button className="language" onClick={changeLanguage}>
            {language === "ar" ? "English" : "العربية"}
          </button>
          <Link
            to="/notifications"
            aria-label={t("الإشعارات", "Notifications")}
            className="icon-link"
          >
            <Icon name="bell" />
          </Link>
          <Link
            to={account.role === "guest" ? "/login" : "/settings"}
            className="account-link"
          >
            {account.role === "guest"
              ? t("دخول", "Sign in")
              : t("حسابي", "Account")}
          </Link>
        </div>
      </header>
      <main id="main">
        {sessionExpired && (
          <Feedback kind="error">
            {t(
              "انتهت الجلسة. سجل الدخول مجدداً.",
              "Session expired. Sign in again.",
            )}
          </Feedback>
        )}
        <Routes>
          <Route path="/" element={<Home />} />
          <Route
            path="/maintenance/new"
            element={
              <Access>
                <NewMaintenance />
              </Access>
            }
          />
          <Route
            path="/maintenance"
            element={
              <Access>
                <MaintenanceList />
              </Access>
            }
          />
          <Route
            path="/maintenance/:id"
            element={
              <Access>
                <MaintenanceDetail />
              </Access>
            }
          />
          <Route
            path="/staff"
            element={
              <Access
                roles={["maintenance", "department", "secretariat", "admin"]}
              >
                {["department", "secretariat"].includes(account.role) ? (
                  <FoundList department />
                ) : (
                  <MaintenanceList staff />
                )}
              </Access>
            }
          />
          <Route
            path="/requests"
            element={
              <Access>
                <Requests />
              </Access>
            }
          />
          <Route
            path="/admin"
            element={
              <Access roles={["admin"]}>
                <AdminOverview />
              </Access>
            }
          />
          <Route
            path="/admin/items"
            element={
              <Access roles={["admin"]}>
                <FoundList all />
              </Access>
            }
          />
          <Route
            path="/admin/accounts"
            element={
              <Access roles={["admin"]}>
                <AdminAccounts key={account.id} />
              </Access>
            }
          />
          <Route
            path="/search"
            element={
              <Access>
                <SearchItems key={account.id} />
              </Access>
            }
          />
          <Route
            path="/found/new"
            element={
              <Access>
                <RegisterFound key={account.id} />
              </Access>
            }
          />
          <Route
            path="/found"
            element={
              <Access>
                <FoundList />
              </Access>
            }
          />
          <Route
            path="/found/:id"
            element={
              <Access>
                <FoundDetail />
              </Access>
            }
          />
          <Route
            path="/claims"
            element={
              <Access>
                <ClaimsList />
              </Access>
            }
          />
          <Route
            path="/claims/:id"
            element={
              <Access>
                <ClaimDetail />
              </Access>
            }
          />
          <Route path="/login" element={<Auth />} />
          <Route path="/register" element={<Auth register />} />
          <Route path="/verify" element={<Verification />} />
          <Route path="/recover" element={<Recovery />} />
          <Route path="/reset/:token" element={<ResetPassword />} />
          <Route
            path="/settings"
            element={
              <Access>
                <Settings key={account.id} />
              </Access>
            }
          />
          <Route
            path="/notifications"
            element={
              <Access>
                <Notifications />
              </Access>
            }
          />
          <Route
            path="*"
            element={
              <Page title={t("الصفحة غير متاحة", "Page unavailable")}>
                <Link to="/">{t("الرئيسية", "Home")}</Link>
              </Page>
            }
          />
        </Routes>
      </main>
      <footer>
        <Link className="brand" to="/">
          <BrandMark />
          <bdi>Smart Campus</bdi>
        </Link>
        <span>
          {t("الصيانة والمفقودات والمعثورات", "Maintenance and lost and found")}
        </span>
      </footer>
    </>
  );
}
export function App() {
  return (
    <AppProvider>
      <Shell />
    </AppProvider>
  );
}
