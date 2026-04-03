export const ROUTES = {
  home: "/",
  about: "/rolunk",

  products: "/termekek",
  productDetails: "/termekek/:productId",

  posts: "/tudastar",
  postDetails: "/tudastar/:postId",

  login: "/bejelentkezes",
  register: "/regisztracio",

  cart: "/kosar",
  checkout: "/penztar",

  account: "/fiokom",
  accountProfile: "/fiokom/adatok",
  accountAddresses: "/fiokom/cimek",
  accountOrders: "/fiokom/rendeleseim",
  accountDelete: "/fiokom/fiok-torles",

  adminLogin: "/admin/bejelentkezes",
  admin: "/admin",
  adminProducts: "/admin/termekek",
  adminOrders: "/admin/rendelesek",
  adminInventory: "/admin/raktarkeszlet",
  adminPosts: "/admin/bejegyzesek",
  adminAccounts: "/admin/fiok",

  notFound: "*",
} as const;