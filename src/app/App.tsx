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
import { AdminCampusDirectory } from "../features/admin/CampusDirectory";
import { Link, NavLink, Route, Routes } from "react-router-dom";
import { AppProvider, useApp } from "./context";
import { BrandMark, Icon } from "../components/Icon";
import { Home } from "./Home";
import { Feedback, Page } from "../components/ui";
import { useState } from "react";
function StaffQueues() {
  const { account, t } = useApp();
  const maintenanceAccess = account.role === "admin" || account.role === "maintenance" || account.roles?.includes("maintenance");
  const custodyAccess = account.role === "department" || account.roles?.includes("department");
  const [view, setView] = useState(maintenanceAccess ? "maintenance" : "custody");
  return <><div className="actions">
    {maintenanceAccess && <button className="button" onClick={() => setView("maintenance")}>{t("الصيانة", "Maintenance")}</button>}
    {custodyAccess && <button className="button" onClick={() => setView("custody")}>{t("العهدة والمعثورات", "Custody and found items")}</button>}
  </div>{view === "maintenance" ? <MaintenanceList staff /> : <FoundList department />}</>;
}
function Shell() {
  const {
    t,
    language,
    changeLanguage,
    account,
    sessionExpired,
  } = useApp();
  return (
    <>
      <a className="skip-link" href="#main">
        {t("انتقل للمحتوى", "Skip to content")}
      </a>
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
              ? t("تسجيل الدخول", "Sign in")
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
                <StaffQueues />
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
            path="/admin/directory"
            element={
              <Access roles={["admin"]}>
                <AdminCampusDirectory />
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
