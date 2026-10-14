import { lazy, Suspense, type ReactNode } from "react";
import { createBrowserRouter } from "react-router";
import RouteMetadata from "../../shared/seo/RouteMetadata";
import { ROUTES } from "../../shared/constants/routes";

import MainLayout from "../layouts/MainLayout";
import AccountLayout from "../layouts/AccountLayout";
import AdminLayout from "../layouts/AdminLayout";

import ProtectedRoute from "./ProtectedRoute";
import AdminRoute from "./AdminRoute";
import GuestOnlyRoute from "./GuestOnlyRoute";
import RouteErrorPage from "./RouteErrorPage";

const HomePage = lazy(() => import("../../pages/public/HomePage"));
const AboutPage = lazy(() => import("../../pages/public/AboutPage"));
const PrivacyPolicyPage = lazy(() => import("../../pages/public/PrivacyPolicyPage"));
const TermsPage = lazy(() => import("../../pages/public/TermsPage"));
const ProductsPage = lazy(() => import("../../pages/public/ProductsPage"));
const ProductDetailsPage = lazy(() => import("../../pages/public/ProductDetailsPage"));
const PostsPage = lazy(() => import("../../pages/public/PostsPage"));
const PostDetailsPage = lazy(() => import("../../pages/public/PostDetailsPage"));
const LoginPage = lazy(() => import("../../pages/public/LoginPage"));
const RegisterPage = lazy(() => import("../../pages/public/RegisterPage"));
const EmailConfirmationPage = lazy(() => import("../../pages/public/EmailConfirmationPage"));
const ForgotPasswordPage = lazy(() => import("../../pages/public/ForgotPasswordPage"));
const ResetPasswordPage = lazy(() => import("../../pages/public/ResetPasswordPage"));
const CartPage = lazy(() => import("../../pages/public/CartPage"));
const CheckoutPage = lazy(() => import("../../pages/public/CheckoutPage"));
const OrderSuccessPage = lazy(() => import("../../pages/public/OrderSuccessPage"));
const OrderWithdrawalRequestPage = lazy(() => import("../../pages/public/OrderWithdrawalRequestPage"));
const NotFoundPage = lazy(() => import("../../pages/public/NotFoundPage"));

const AccountDashboardPage = lazy(() => import("../../pages/account/AccountDashboardPage"));
const AccountProfilePage = lazy(() => import("../../pages/account/AccountProfilePage"));
const AccountAddressesPage = lazy(() => import("../../pages/account/AccountAddressesPage"));
const AccountOrdersPage = lazy(() => import("../../pages/account/AccountOrdersPage"));
const AccountOrderDetailsPage = lazy(() => import("../../pages/account/AccountOrderDetailsPage"));
const AccountDeletePage = lazy(() => import("../../pages/account/AccountDeletePage"));

const AdminDashboardPage = lazy(() => import("../../pages/admin/AdminDashboardPage"));
const AdminLoginPage = lazy(() => import("../../pages/admin/AdminLoginPage"));
const AdminProductsPage = lazy(() => import("../../pages/admin/AdminProductsPage"));
const AdminOrdersPage = lazy(() => import("../../pages/admin/AdminOrdersPage"));
const AdminWithdrawalRequestsPage = lazy(() => import("../../pages/admin/AdminWithdrawalRequestsPage"));
const AdminInventoryPage = lazy(() => import("../../pages/admin/AdminInventoryPage"));
const AdminPostsPage = lazy(() => import("../../pages/admin/AdminPostsPage"));
const AdminAccountsPage = lazy(() => import("../../pages/admin/AdminAccountsPage"));

const pageLoader = (
  <div className="grid min-h-[14rem] w-full place-items-center bg-site-surface-soft px-4 py-10 text-center text-site-muted">
    Betöltés...
  </div>
);

function routeElement(page: ReactNode) {
  return <><RouteMetadata /><Suspense fallback={pageLoader}>{page}</Suspense></>;
}

export const router = createBrowserRouter([
  {
    path: ROUTES.home,
    errorElement: <RouteErrorPage />,
    element: <MainLayout />,
    children: [
      { index: true, element: routeElement(<HomePage />) },
      { path: "*", element: routeElement(<NotFoundPage />) },
      { path: ROUTES.about.slice(1), element: routeElement(<AboutPage />) },
      { path: ROUTES.privacyPolicy.slice(1), element: routeElement(<PrivacyPolicyPage />) },
      { path: ROUTES.terms.slice(1), element: routeElement(<TermsPage />) },
      { path: ROUTES.products.slice(1), element: routeElement(<ProductsPage />) },
      { path: "termekek/:productId", element: routeElement(<ProductDetailsPage />) },
      { path: "tudastar", element: routeElement(<PostsPage />) },
      { path: "tudastar/:postId", element: routeElement(<PostDetailsPage />) },
      {
        element: <GuestOnlyRoute />,
        children: [
          { path: ROUTES.login.slice(1), element: routeElement(<LoginPage />) },
          { path: ROUTES.register.slice(1), element: routeElement(<RegisterPage />) },
        ],
      },
      { path: ROUTES.emailConfirmation.slice(1), element: routeElement(<EmailConfirmationPage />) },
      { path: ROUTES.forgotPassword.slice(1), element: routeElement(<ForgotPasswordPage />) },
      { path: ROUTES.resetPassword.slice(1), element: routeElement(<ResetPasswordPage />) },
      { path: ROUTES.cart.slice(1), element: routeElement(<CartPage />) },
      { path: ROUTES.checkout.slice(1), element: routeElement(<CheckoutPage />) },
      { path: ROUTES.orderSuccess.slice(1), element: routeElement(<OrderSuccessPage />) },
      { path: ROUTES.orderWithdrawalRequest.slice(1), element: routeElement(<OrderWithdrawalRequestPage />) },
    ],
  },

  {
    element: <ProtectedRoute />,
    errorElement: <RouteErrorPage />,
    children: [
      {
        path: ROUTES.account,
        element: <AccountLayout />,
        children: [
          { index: true, element: routeElement(<AccountDashboardPage />) },
          { path: "adatok", element: routeElement(<AccountProfilePage />) },
          { path: "cimek", element: routeElement(<AccountAddressesPage />) },
          { path: "rendeleseim", element: routeElement(<AccountOrdersPage />) },
          { path: "rendeleseim/:orderId", element: routeElement(<AccountOrderDetailsPage />) },
          { path: "fiok-torles", element: routeElement(<AccountDeletePage />) },
        ],
      },
    ],
  },

  {
    path: ROUTES.admin,
    errorElement: <RouteErrorPage />,
    element: <><RouteMetadata /><AdminRoute /></>,
    children: [
      {
        element: <AdminLayout />,
        children: [
          { index: true, element: routeElement(<AdminDashboardPage />) },
          { path: "termekek", element: routeElement(<AdminProductsPage />) },
          { path: "rendelesek", element: routeElement(<AdminOrdersPage />) },
          { path: "elallasi-keresek", element: routeElement(<AdminWithdrawalRequestsPage />) },
          { path: "raktarkeszlet", element: routeElement(<AdminInventoryPage />) },
          { path: "bejegyzesek", element: routeElement(<AdminPostsPage />) },
          { path: "fiok", element: routeElement(<AdminAccountsPage />) },
        ],
      },
    ],
  },

  {
    path: ROUTES.adminLogin,
    errorElement: <RouteErrorPage />,
    element: routeElement(<AdminLoginPage />),
  },

]);
