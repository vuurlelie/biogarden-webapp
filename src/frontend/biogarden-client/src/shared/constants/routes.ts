export const ROUTES = {
  home: "/",
  about: "/rolunk",
  privacyPolicy: "/adatkezelesi-tajekoztato",
  terms: "/altalanos-szerzodesi-feltetelek",

  products: "/termekek",
  productDetails: "/termekek/:productId",

  posts: "/tudastar",
  postDetails: "/tudastar/:postId",

  login: "/bejelentkezes",
  register: "/regisztracio",
  emailConfirmation: "/email-megerosites",

  cart: "/kosar",
  checkout: "/penztar",
  orderSuccess: "/rendeles-sikeres",

  account: "/fiokom",
  accountProfile: "/fiokom/adatok",
  accountAddresses: "/fiokom/cimek",
  accountOrders: "/fiokom/rendeleseim",
  accountOrderDetails: "/fiokom/rendeleseim/:orderId",
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
