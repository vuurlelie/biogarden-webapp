import { createBrowserRouter } from "react-router-dom";
import { ROUTES } from "../../shared/constants/routes";

import MainLayout from "../layouts/MainLayout";
import AccountLayout from "../layouts/AccountLayout";
import AdminLayout from "../layouts/AdminLayout";

import ProtectedRoute from "./ProtectedRoute";
import AdminRoute from "./AdminRoute";

// pages
import HomePage from "../../pages/public/HomePage";
import AboutPage from "../../pages/public/AboutPage";
import ProductsPage from "../../pages/public/ProductsPage";
import ProductDetailsPage from "../../pages/public/ProductDetailsPage";
import PostsPage from "../../pages/public/PostsPage";
import PostDetailsPage from "../../pages/public/PostDetailsPage";
import LoginPage from "../../pages/public/LoginPage";
import RegisterPage from "../../pages/public/RegisterPage";
import CartPage from "../../pages/public/CartPage";
import CheckoutPage from "../../pages/public/CheckoutPage";
import OrderSuccessPage from "../../pages/public/OrderSuccessPage";
import NotFoundPage from "../../pages/public/NotFoundPage";

import AccountDashboardPage from "../../pages/account/AccountDashboardPage";
import AccountProfilePage from "../../pages/account/AccountProfilePage";
import AccountAddressesPage from "../../pages/account/AccountAddressesPage";
import AccountOrdersPage from "../../pages/account/AccountOrdersPage";
import AccountOrderDetailsPage from "../../pages/account/AccountOrderDetailsPage";

import AdminDashboardPage from "../../pages/admin/AdminDashboardPage";
import AdminLoginPage from "../../pages/admin/AdminLoginPage";
import AdminProductsPage from "../../pages/admin/AdminProductsPage";
import AdminOrdersPage from "../../pages/admin/AdminOrdersPage";
import AdminInventoryPage from "../../pages/admin/AdminInventoryPage";
import AdminPostsPage from "../../pages/admin/AdminPostsPage";
import AdminAccountsPage from "../../pages/admin/AdminAccountsPage";

export const router = createBrowserRouter([
  {
    path: ROUTES.home,
    element: <MainLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: ROUTES.about.slice(1), element: <AboutPage /> },
      { path: ROUTES.products.slice(1), element: <ProductsPage /> },
      { path: "termekek/:productId", element: <ProductDetailsPage /> },
      { path: "tudastar", element: <PostsPage /> },
      { path: "tudastar/:postId", element: <PostDetailsPage /> },
      { path: ROUTES.login.slice(1), element: <LoginPage /> },
      { path: ROUTES.register.slice(1), element: <RegisterPage /> },
      { path: ROUTES.cart.slice(1), element: <CartPage /> },
      { path: ROUTES.checkout.slice(1), element: <CheckoutPage /> },
      { path: ROUTES.orderSuccess.slice(1), element: <OrderSuccessPage /> },
    ],
  },

  {
    element: <ProtectedRoute />,
    children: [
      {
        path: ROUTES.account,
        element: <AccountLayout />,
        children: [
          { index: true, element: <AccountDashboardPage /> },
          { path: "adatok", element: <AccountProfilePage /> },
          { path: "cimek", element: <AccountAddressesPage /> },
          { path: "rendeleseim", element: <AccountOrdersPage /> },
          { path: "rendeleseim/:orderId", element: <AccountOrderDetailsPage /> },
        ],
      },
    ],
  },

  {
    path: ROUTES.admin,
    element: <AdminRoute />,
    children: [
      {
        element: <AdminLayout />,
        children: [
          { index: true, element: <AdminDashboardPage /> },
          { path: "termekek", element: <AdminProductsPage /> },
          { path: "rendelesek", element: <AdminOrdersPage /> },
          { path: "raktarkeszlet", element: <AdminInventoryPage /> },
          { path: "bejegyzesek", element: <AdminPostsPage /> },
          { path: "fiok", element: <AdminAccountsPage /> },
        ],
      },
    ],
  },

  {
    path: ROUTES.adminLogin,
    element: <AdminLoginPage />,
  },

  {
    path: "*",
    element: <NotFoundPage />,
  },
]);
