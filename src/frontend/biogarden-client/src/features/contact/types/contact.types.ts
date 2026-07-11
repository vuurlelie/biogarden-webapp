export type ContactMessageRequest = {
  name: string;
  email: string;
  phone?: string;
  subject?: string;
  message: string;
};
