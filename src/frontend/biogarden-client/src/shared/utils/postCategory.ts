export function getPostCategoryLabel(category: 0 | 1): string {
  switch (category) {
    case 0:
      return "Tudástár";
    case 1:
      return "Receptajánló";
    default:
      return "Ismeretlen";
  }
}