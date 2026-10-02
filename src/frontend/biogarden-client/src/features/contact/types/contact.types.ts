export type ContactMessageRequest = {
  submissionId: string;
  name: string;
  email: string;
  phone?: string;
  subject?: string;
  message: string;
};
