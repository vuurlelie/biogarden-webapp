export type AdminRecentOrder = {
  id: string;
  orderNumber: string;
  customerName: string;
  createdAtUtc: string;
  totalPrice: number;
  itemCount: number;
  status: number;
  fulfillmentMethod: number;
};

export type AdminWithdrawalRequestPreview = {
  id: number;
  orderId: string;
  orderNumber: string;
  customerName: string;
  requestedAtUtc: string;
};

export type AdminLowStockProduct = {
  id: number;
  name: string;
  stockQuantity: number;
  available: boolean;
};

export type AdminDashboard = {
  productCount: number;
  availableProductCount: number;
  lowStockProductCount: number;
  pendingOrderCount: number;
  pendingWithdrawalRequestCount: number;
  todayOrderCount: number;
  userCount: number;
  publishedPostCount: number;
  activeOrderValue: number;
  recentOrders: AdminRecentOrder[];
  pendingWithdrawalRequests: AdminWithdrawalRequestPreview[];
  lowStockProducts: AdminLowStockProduct[];
};
